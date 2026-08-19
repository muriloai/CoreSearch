using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CoreSearch.ViewModels;

namespace CoreSearch;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetWindowIcon();
    }

    private void SetWindowIcon()
    {
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
            }
        }
        catch
        {
            // Ignora falha de carregamento de ícone se o arquivo estiver corrompido ou inacessível
        }
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.OpenFileCommand.CanExecute(null))
        {
            viewModel.OpenFileCommand.Execute(null);
        }
    }
}