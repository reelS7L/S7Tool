using S7Tool.ViewModels;
using System.Windows;

namespace S7Tool.Views;

public partial class ProgressWindow : Window
{
    public ProgressWindow(ProgressViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
