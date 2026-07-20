using S7Tool.ViewModels;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;

namespace S7Tool.Views;

public partial class AiChatWindow : Window
{
    private readonly AiChatViewModel _viewModel;

    public AiChatWindow(AiChatViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;

        viewModel.Messages.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
                Dispatcher.BeginInvoke(() => ChatScrollViewer.ScrollToEnd());
        };
    }

    private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;

            if (_viewModel.SendCommand.CanExecute(null))
                _viewModel.SendCommand.Execute(null);
        }
    }
}
