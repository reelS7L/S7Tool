using S7Tool.ViewModels;
using System.Windows;

namespace S7Tool.Views;

public partial class DiskSpaceAnalyzerWindow : Window
{
    public DiskSpaceAnalyzerWindow(DiskSpaceAnalyzerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        TreemapView.ItemClicked += node => viewModel.NavigateIntoCommand.Execute(node);
        PieView.ItemClicked += node => viewModel.NavigateIntoCommand.Execute(node);

        TreemapView.ItemRightClicked += node => viewModel.SelectedItem = node;
        PieView.ItemRightClicked += node => viewModel.SelectedItem = node;
    }
}
