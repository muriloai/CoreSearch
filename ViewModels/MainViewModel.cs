using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CoreSearch.Models;
using CoreSearch.Services;
using Microsoft.Win32;

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
    private string _statusMessage = "Pronto para buscar.";
    private int _resultCount;
    private SearchResult? _selectedResult;

    public ObservableCollection<SearchResult> Results { get; } = new();

    public string RootDirectory
    {
        get => _rootDirectory;
        set
        {
            var cleaned = value?.Trim('"', ' ', '\t') ?? string.Empty;
            if (SetField(ref _rootDirectory, cleaned))
            {
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetField(ref _searchTerm, value))
            {
                SearchCommand.RaiseCanExecuteChanged();
            }
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
        private set
        {
            if (SetField(ref _isSearching, value))
            {
                SearchCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
                SelectFolderCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(CanSearch));
            }
        }
    }

    public bool CanSearch => !IsSearching;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public int ResultCount
    {
        get => _resultCount;
        set => SetField(ref _resultCount, value);
    }

    public SearchResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetField(ref _selectedResult, value))
            {
                OpenFileCommand.RaiseCanExecuteChanged();
                OpenFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand SearchCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand SelectFolderCommand { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    public MainViewModel(ISearchEngine searchEngine)
    {
        _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));

        SearchCommand = new RelayCommand(async () => await ExecuteSearchAsync(), () => CanExecuteSearch());
        CancelCommand = new RelayCommand(ExecuteCancel, () => IsSearching);
        SelectFolderCommand = new RelayCommand(ExecuteSelectFolder, () => !IsSearching);
        OpenFileCommand = new RelayCommand(ExecuteOpenFile, () => SelectedResult != null);
        OpenFolderCommand = new RelayCommand(ExecuteOpenFolder, () => SelectedResult != null);
    }

    public MainViewModel() : this(new SearchEngine())
    {
    }

    private bool CanExecuteSearch()
    {
        return !IsSearching &&
               !string.IsNullOrWhiteSpace(RootDirectory) &&
               Directory.Exists(RootDirectory) &&
               !string.IsNullOrWhiteSpace(SearchTerm);
    }

    private void ExecuteSelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecionar pasta para pesquisa",
            InitialDirectory = Directory.Exists(RootDirectory) ? RootDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            RootDirectory = dialog.FolderName;
        }
    }

    private async Task ExecuteSearchAsync()
    {
        if (!CanExecuteSearch())
            return;

        IsSearching = true;
        Results.Clear();
        ResultCount = 0;
        StatusMessage = "Buscando ocorrências...";

        _cts = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();

        var options = new SearchOptions
        {
            RootDirectory = RootDirectory,
            SearchTerm = SearchTerm,
            ExtensionFilter = ExtensionFilter,
            MatchCase = MatchCase,
            MatchWholeWord = MatchWholeWord,
            IncludeSubdirectories = IncludeSubdirectories
        };

        var batch = new List<SearchResult>(128);
        var lastFlush = Stopwatch.StartNew();

        // Throttle UI updates in batches to prevent UI stutter on high-frequency matches
        var progress = new Progress<SearchResult>(result =>
        {
            batch.Add(result);
            ResultCount++;

            if (lastFlush.ElapsedMilliseconds >= 50 || batch.Count >= 100)
            {
                foreach (var item in batch)
                {
                    Results.Add(item);
                }
                batch.Clear();
                lastFlush.Restart();
                StatusMessage = $"Buscando... {ResultCount:N0} ocorrência(s) encontrada(s)";
            }
        });

        try
        {
            await _searchEngine.SearchAsync(options, progress, _cts.Token);

            foreach (var item in batch)
            {
                Results.Add(item);
            }
            batch.Clear();

            stopwatch.Stop();
            StatusMessage = $"Concluído em {stopwatch.Elapsed.TotalSeconds:F2}s — {ResultCount:N0} resultado(s) encontrado(s).";
        }
        catch (OperationCanceledException)
        {
            foreach (var item in batch)
            {
                Results.Add(item);
            }
            batch.Clear();

            stopwatch.Stop();
            StatusMessage = $"Busca cancelada em {stopwatch.Elapsed.TotalSeconds:F2}s ({ResultCount:N0} resultado(s)).";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            StatusMessage = $"Erro durante a busca: {ex.Message}";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsSearching = false;
        }
    }

    private void ExecuteCancel()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            StatusMessage = "Cancelando busca...";
            _cts.Cancel();
        }
    }

    private void ExecuteOpenFile()
    {
        if (SelectedResult == null || !File.Exists(SelectedResult.FilePath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedResult.FilePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível abrir o arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteOpenFolder()
    {
        if (SelectedResult == null)
            return;

        try
        {
            if (File.Exists(SelectedResult.FilePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{SelectedResult.FilePath}\"",
                    UseShellExecute = true
                });
            }
            else if (Directory.Exists(SelectedResult.DirectoryPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = SelectedResult.DirectoryPath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível abrir a pasta: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
