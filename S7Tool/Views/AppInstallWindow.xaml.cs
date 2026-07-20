using S7Tool.ViewModels;
using System.Windows;

namespace S7Tool.Views;

public partial class AppInstallWindow : Window
{
    public AppInstallWindow(AppInstallViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
