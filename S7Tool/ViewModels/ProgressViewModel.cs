using CommunityToolkit.Mvvm.ComponentModel;
using S7Tool.Services;

namespace S7Tool.ViewModels;

public partial class ProgressViewModel : ObservableObject, IProgress<(int Percent, string Status)>
{
    [ObservableProperty]
    private string title = LocalizationManager.T("Str_Progress_Processing");

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private string statusText = LocalizationManager.T("Str_Progress_Initializing");

    [ObservableProperty]
    private string logText = "";

    public void Report((int Percent, string Status) value)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ProgressValue = Math.Max(0, Math.Min(100, value.Percent));
            StatusText = value.Status;
            AddLog(value.Status);
        });
    }

    private void AddLog(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        LogText += text + "\n";

        if (LogText.Length > 5000)
            LogText = LogText[^4000..];
    }
}
