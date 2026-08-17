using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using CoreSearch.Models;
using CoreSearch.Services;
using CoreSearch.ViewModels;
using Microsoft.Win32;

namespace CoreSearch;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

public partial class MainWindow : Window
{
    public static readonly IValueConverter BoolToVisibility = new BoolToVisibilityConverter();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new SearchEngine());
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecionar pasta para busca",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && DataContext is MainViewModel vm)
        {
            vm.RootDirectory = dialog.FolderName;
        }
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.OpenFileCommand.CanExecute(null))
        {
            vm.OpenFileCommand.Execute(null);
        }
    }
}