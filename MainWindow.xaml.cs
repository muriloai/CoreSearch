using System.Windows;
using System.Windows.Input;
using CoreSearch.ViewModels;

namespace CoreSearch;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.OpenFileCommand.CanExecute(null))
        {
            viewModel.OpenFileCommand.Execute(null);
        }
    }
}