using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CoreSearch.Models;

namespace CoreSearch.Services;

public class SearchEngine : ISearchEngine
{
    public async Task SearchAsync(SearchOptions options, IProgress<SearchResult> progress, CancellationToken cancellationToken)
    {
        await foreach (var result in SearchStreamAsync(options, cancellationToken).ConfigureAwait(false))
        {
            progress.Report(result);
        }
    }

    public async IAsyncEnumerable<SearchResult> SearchStreamAsync(
        SearchOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.RootDirectory) || !Directory.Exists(options.RootDirectory))
            yield break;

        if (string.IsNullOrEmpty(options.SearchTerm))
            yield break;

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = options.IncludeSubdirectories,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(options.RootDirectory, string.IsNullOrWhiteSpace(options.ExtensionFilter) ? "*.*" : options.ExtensionFilter, enumerationOptions);
        }
        catch
        {
            yield break;
        }

        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        Regex? wholeWordRegex = null;
        if (options.MatchWholeWord)
        {
            var pattern = $@"\b{Regex.Escape(options.SearchTerm)}\b";
            var regexOptions = options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            wholeWordRegex = new Regex(pattern, regexOptions | RegexOptions.Compiled);
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var searchResults = await Task.Run(() =>
            {
                var list = new List<SearchResult>();
                try
                {
                    var fileInfo = new FileInfo(file);
                    // Ignora arquivos gigantes para evitar travar a busca
                    if (fileInfo.Length > 100 * 1024 * 1024)
                        return list;

                    using var reader = new StreamReader(file);
                    string? line;
                    int lineNumber = 0;

                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;

                        bool isMatch = wholeWordRegex != null
                            ? wholeWordRegex.IsMatch(line)
                            : line.IndexOf(options.SearchTerm, comparison) >= 0;

                        if (isMatch)
                        {
                            list.Add(new SearchResult
                            {
                                FileName = fileInfo.Name,
                                FilePath = file,
                                DirectoryPath = fileInfo.DirectoryName ?? string.Empty,
                                LineNumber = lineNumber,
                                LineContent = line.Trim()
                            });
                        }
                    }
                }
                catch
                {
                    // Ignora falhas de leitura em arquivos bloqueados/sem permissão
                }

                return list;
            }, cancellationToken).ConfigureAwait(false);

            foreach (var res in searchResults)
            {
                yield return res;
            }
        }
    }
}
