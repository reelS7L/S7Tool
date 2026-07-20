using S7Tool.ViewModels;
using System.Windows;

namespace S7Tool.Views;

public partial class DiskHealthWindow : Window
{
    private readonly DiskHealthViewModel _viewModel;

    public DiskHealthWindow(DiskHealthViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Closed += (_, _) => _viewModel.StopPolling();
    }
}
