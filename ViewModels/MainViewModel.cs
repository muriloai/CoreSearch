using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CoreSearch.Models;
using CoreSearch.Services;

namespace CoreSearch.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ISearchEngine _searchEngine;
    private CancellationTokenSource? _cts;

    private string _rootDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private string _searchTerm = string.Empty;
    private string _extensionFilter = "*.*";
    private bool _matchCase;
    private bool _matchWholeWord;
    private bool _includeSubdirectories = true;

    private bool _isSearching;
    private string _statusMessage = "Pronto";
    private SearchResult? _selectedResult;

    public MainViewModel(ISearchEngine searchEngine)
    {
        _searchEngine = searchEngine;

        SearchCommand = new RelayCommand(async () => await ExecuteSearchAsync(), () => !IsSearching && !string.IsNullOrWhiteSpace(SearchTerm) && !string.IsNullOrWhiteSpace(RootDirectory));
        CancelCommand = new RelayCommand(CancelSearch, () => IsSearching);
        OpenFileCommand = new RelayCommand(OpenFile, () => SelectedResult != null);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => SelectedResult != null);
    }

    public ObservableCollection<SearchResult> Results { get; } = new();

    public string RootDirectory
    {
        get => _rootDirectory;
        set
        {
            if (SetField(ref _rootDirectory, value))
                ((RelayCommand)SearchCommand).RaiseCanExecuteChanged();
        }
    }

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetField(ref _searchTerm, value))
                ((RelayCommand)SearchCommand).RaiseCanExecuteChanged();
        }
    }

    public string ExtensionFilter
    {
        get => _extensionFilter;
        set => SetField(ref _extensionFilter, value);
    }

    public bool MatchCase
    {
        get => _matchCase;
        set => SetField(ref _matchCase, value);
    }

    public bool MatchWholeWord
    {
        get => _matchWholeWord;
        set => SetField(ref _matchWholeWord, value);
    }

    public bool IncludeSubdirectories
    {
        get => _includeSubdirectories;
        set => SetField(ref _includeSubdirectories, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        set
        {
            if (SetField(ref _isSearching, value))
            {
                ((RelayCommand)SearchCommand).RaiseCanExecuteChanged();
                ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public SearchResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetField(ref _selectedResult, value))
            {
                ((RelayCommand)OpenFileCommand).RaiseCanExecuteChanged();
                ((RelayCommand)OpenFolderCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand SearchCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand OpenFolderCommand { get; }

    private async Task ExecuteSearchAsync()
    {
        if (IsSearching) return;

        Results.Clear();
        IsSearching = true;
        StatusMessage = "Buscando...";
        _cts = new CancellationTokenSource();

        var options = new SearchOptions
        {
            RootDirectory = RootDirectory,
            SearchTerm = SearchTerm,
            ExtensionFilter = ExtensionFilter,
            MatchCase = MatchCase,
            MatchWholeWord = MatchWholeWord,
            IncludeSubdirectories = IncludeSubdirectories
        };

        var progress = new Progress<SearchResult>(result =>
        {
            Results.Add(result);
            StatusMessage = $"{Results.Count} ocorrência(s) encontrada(s)...";
        });

        var sw = Stopwatch.StartNew();
        try
        {
            await _searchEngine.SearchAsync(options, progress, _cts.Token);
            sw.Stop();
            StatusMessage = $"Concluído em {sw.ElapsedMilliseconds} ms ({Results.Count} resultado(s))";
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            StatusMessage = $"Cancelado ({Results.Count} resultado(s))";
        }
        catch (Exception ex)
        {
            sw.Stop();
            StatusMessage = $"Erro: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void CancelSearch()
    {
        _cts?.Cancel();
    }

    private void OpenFile()
    {
        if (SelectedResult == null || !File.Exists(SelectedResult.FilePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedResult.FilePath,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private void OpenFolder()
    {
        if (SelectedResult == null || !File.Exists(SelectedResult.FilePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{SelectedResult.FilePath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}
