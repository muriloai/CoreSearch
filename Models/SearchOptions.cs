namespace CoreSearch.Models;

public record SearchOptions
{
    public required string RootDirectory { get; init; }
    public required string SearchTerm { get; init; }
    public string ExtensionFilter { get; init; } = "*.*";
    public bool MatchCase { get; init; } = false;
    public bool MatchWholeWord { get; init; } = false;
    public bool IncludeSubdirectories { get; init; } = true;
}
