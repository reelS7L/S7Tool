using S7Tool.ViewModels;
using System.Windows;

namespace S7Tool.Views;

public partial class UninstallWindow : Window
{
    public UninstallWindow(UninstallViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
