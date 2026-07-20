using S7Tool.ViewModels;
using System.Windows;

namespace S7Tool.Views;

public partial class RenamePCWindow : Window
{
    public RenamePCWindow(RenamePCViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
