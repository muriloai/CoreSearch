namespace CoreSearch.Models;

public record SearchResult
{
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public required string DirectoryPath { get; init; }
    public required int LineNumber { get; init; }
    public required string LineContent { get; init; }
}
