using S7Tool.ViewModels;
using System.Windows;

namespace S7Tool.Views;

public partial class PortScannerWindow : Window
{
    public PortScannerWindow(PortScannerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
