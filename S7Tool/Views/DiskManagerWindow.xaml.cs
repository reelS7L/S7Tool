using S7Tool.ViewModels;
using System.Windows;

namespace S7Tool.Views;

public partial class DiskManagerWindow : Window
{
    public DiskManagerWindow(DiskManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
