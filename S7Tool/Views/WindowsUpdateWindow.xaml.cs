using S7Tool.ViewModels;
using System.Windows;

namespace S7Tool.Views;

public partial class WindowsUpdateWindow : Window
{
    public WindowsUpdateWindow(WindowsUpdateViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
