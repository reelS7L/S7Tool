using S7Tool.ViewModels;
using System.Windows;

namespace S7Tool.Views;

public partial class AiSettingsWindow : Window
{
    public AiSettingsWindow(AiSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
