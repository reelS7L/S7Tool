using S7Tool.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

    private void ItemsGrid_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var row = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
        if (row?.Item is not null)
            ItemsGrid.SelectedItem = row.Item;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
