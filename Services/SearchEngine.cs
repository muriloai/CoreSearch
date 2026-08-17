using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CoreSearch.Models;

namespace CoreSearch.Services;

public class SearchEngine : ISearchEngine
{
    private const int MaxSnippetLength = 260;

    private static readonly HashSet<string> DefaultIgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".bin", ".iso", ".zip", ".tar", ".gz", ".7z", ".rar",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp", ".mp4", ".mp3", ".avi",
        ".pdf", ".docx", ".xlsx", ".pptx", ".obj", ".class", ".pyc"
    };

    public async Task SearchAsync(
        SearchOptions options,
        IProgress<SearchResult> progress,
        CancellationToken cancellationToken)
    {
        await foreach (var result in SearchStreamAsync(options, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(result);
        }
    }

    public async IAsyncEnumerable<SearchResult> SearchStreamAsync(
        SearchOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rootDir = options.RootDirectory?.Trim('"', ' ', '\t') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir) || string.IsNullOrEmpty(options.SearchTerm))
        {
            yield break;
        }

        var patterns = ParsePatterns(options.ExtensionFilter);
        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var wholeWordRegex = BuildWholeWordRegex(options.SearchTerm, options.MatchCase, options.MatchWholeWord);

        var channel = Channel.CreateBounded<SearchResult>(new BoundedChannelOptions(500)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        var files = SafeEnumerateFiles(rootDir, patterns, options.IncludeSubdirectories, cancellationToken);

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount)
        };

        var producerTask = Task.Run(async () =>
        {
            try
            {
                await Parallel.ForEachAsync(files, parallelOptions, async (filePath, ct) =>
                {
                    var ext = Path.GetExtension(filePath);
                    if (DefaultIgnoredExtensions.Contains(ext) && !IsExplicitlyRequested(ext, patterns))
                    {
                        return;
                    }

                    await SearchInFileAsync(filePath, options.SearchTerm, comparison, wholeWordRegex, channel.Writer, ct);
                });
            }
            catch (OperationCanceledException)
            {
                // Expected when canceled
            }
            catch
            {
                // Suppress non-critical worker exceptions
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        while (await channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (channel.Reader.TryRead(out var result))
            {
                yield return result;
            }
        }

        await producerTask;
    }

    private static async ValueTask SearchInFileAsync(
        string filePath,
        string searchTerm,
        StringComparison comparison,
        Regex? wholeWordRegex,
        ChannelWriter<SearchResult> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 16384,
                useAsync: true);

            if (fileStream.Length == 0 || IsBinaryFile(fileStream))
            {
                return;
            }

            using var reader = new StreamReader(fileStream, detectEncodingFromByteOrderMarks: true);

            string fileName = Path.GetFileName(filePath);
            string directoryPath = Path.GetDirectoryName(filePath) ?? string.Empty;
            int lineNumber = 0;
            string? line;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                lineNumber++;

                bool isMatch = wholeWordRegex != null
                    ? wholeWordRegex.IsMatch(line)
                    : line.Contains(searchTerm, comparison);

                if (isMatch)
                {
                    var result = new SearchResult
                    {
                        FileName = fileName,
                        FilePath = filePath,
                        DirectoryPath = directoryPath,
                        LineNumber = lineNumber,
                        LineContent = FormatLineSnippet(line, searchTerm, comparison)
                    };

                    await writer.WriteAsync(result, cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            // Skip inaccessible or locked files
        }
    }

    private static bool IsBinaryFile(FileStream stream)
    {
        Span<byte> buffer = stackalloc byte[1024];
        int bytesRead = stream.Read(buffer);
        stream.Seek(0, SeekOrigin.Begin);

        for (int i = 0; i < bytesRead; i++)
        {
            if (buffer[i] == 0)
                return true;
        }

        return false;
    }

    private static string FormatLineSnippet(string line, string searchTerm, StringComparison comparison)
    {
        var trimmed = line.Trim();
        if (trimmed.Length <= MaxSnippetLength)
        {
            return trimmed;
        }

        int matchIdx = trimmed.IndexOf(searchTerm, comparison);
        if (matchIdx < 0)
        {
            return trimmed[..MaxSnippetLength] + "...";
        }

        int half = (MaxSnippetLength - searchTerm.Length) / 2;
        int start = Math.Max(0, matchIdx - half);
        int length = Math.Min(trimmed.Length - start, MaxSnippetLength);

        string snippet = trimmed.Substring(start, length);
        if (start > 0)
            snippet = "..." + snippet;
        if (start + length < trimmed.Length)
            snippet += "...";

        return snippet;
    }

    private static Regex? BuildWholeWordRegex(string searchTerm, bool matchCase, bool matchWholeWord)
    {
        if (!matchWholeWord)
            return null;

        var options = RegexOptions.Compiled;
        if (!matchCase)
            options |= RegexOptions.IgnoreCase;

        return new Regex($@"(?<!\w){Regex.Escape(searchTerm)}(?!\w)", options);
    }

    private static IEnumerable<string> SafeEnumerateFiles(
        string rootDirectory,
        List<string> patterns,
        bool recursive,
        CancellationToken cancellationToken)
    {
        var directories = new Stack<string>();
        directories.Push(rootDirectory);

        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentDir = directories.Pop();

            IEnumerable<string>? files = null;
            try
            {
                files = Directory.EnumerateFiles(currentDir);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                // Inaccessible folder
            }

            if (files != null)
            {
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileName = Path.GetFileName(file);
                    if (MatchesAnyPattern(fileName, patterns))
                    {
                        yield return file;
                    }
                }
            }

            if (recursive)
            {
                IEnumerable<string>? subDirs = null;
                try
                {
                    subDirs = Directory.EnumerateDirectories(currentDir);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
                {
                    // Inaccessible subfolder
                }

                if (subDirs != null)
                {
                    foreach (var subDir in subDirs)
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(subDir);
                            // Avoid junction / symlink recursive loops and system volume directories
                            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                continue;
                            }

                            var dirName = Path.GetFileName(subDir);
                            if (dirName.StartsWith("$", StringComparison.Ordinal) || dirName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            directories.Push(subDir);
                        }
                        catch
                        {
                            // Skip directories whose attributes cannot be read
                        }
                    }
                }
            }
        }
    }

    private static List<string> ParsePatterns(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter.Trim() == "*.*" || filter.Trim() == "*")
        {
            return new List<string> { "*" };
        }

        var parts = filter.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<string>();

        foreach (var raw in parts)
        {
            var pattern = raw.Trim();
            if (string.IsNullOrEmpty(pattern))
                continue;

            if (pattern == "*.*" || pattern == "*")
            {
                return new List<string> { "*" };
            }

            if (!pattern.Contains('*') && !pattern.Contains('?'))
            {
                if (pattern.StartsWith('.'))
                    pattern = "*" + pattern;
                else
                    pattern = "*." + pattern;
            }

            result.Add(pattern);
        }

        return result.Count > 0 ? result : new List<string> { "*" };
    }

    private static bool MatchesAnyPattern(string fileName, List<string> patterns)
    {
        if (patterns.Count == 1 && (patterns[0] == "*" || patterns[0] == "*.*"))
            return true;

        foreach (var pattern in patterns)
        {
            if (pattern == "*" || pattern == "*.*")
                return true;

            if (FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true))
                return true;
        }

        return false;
    }

    private static bool IsExplicitlyRequested(string extension, List<string> patterns)
    {
        var ext = extension.TrimStart('.');
        foreach (var pattern in patterns)
        {
            var cleanPattern = pattern.TrimStart('*', '.');
            if (cleanPattern.Equals(ext, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
