using S7Tool.ViewModels;
using System.Windows;

namespace S7Tool.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
