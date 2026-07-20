using S7Tool.Models;
using S7Tool.ViewModels;
using System.ComponentModel;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace S7Tool.Views;

public partial class NetworkScannerWindow : Window
{
    private readonly NetworkScannerViewModel _viewModel;

    public NetworkScannerWindow(NetworkScannerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
    }

    private void ResultsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ResultsGrid.SelectedItem is NetworkHost host && host.HasWeb)
            _viewModel.OpenInBrowserCommand.Execute(host);
    }

    private void ResultsGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (e.Column != IpColumn) return;

        e.Handled = true;

        var direction = e.Column.SortDirection != ListSortDirection.Ascending
            ? ListSortDirection.Ascending
            : ListSortDirection.Descending;

        var sorted = _viewModel.Hosts
            .OrderBy(h => IPAddress.Parse(h.IpAddress).GetAddressBytes(), ByteArrayComparer.Instance)
            .ToList();

        if (direction == ListSortDirection.Descending)
            sorted.Reverse();

        _viewModel.Hosts.Clear();
        foreach (var host in sorted)
            _viewModel.Hosts.Add(host);

        foreach (var column in ResultsGrid.Columns)
            column.SortDirection = null;

        e.Column.SortDirection = direction;
    }

    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public static readonly ByteArrayComparer Instance = new();

        public int Compare(byte[]? x, byte[]? y)
        {
            if (x is null || y is null) return 0;

            for (int i = 0; i < x.Length && i < y.Length; i++)
            {
                int cmp = x[i].CompareTo(y[i]);
                if (cmp != 0) return cmp;
            }

            return 0;
        }
    }
}
