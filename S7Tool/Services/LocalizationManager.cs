using System.Windows;

namespace S7Tool.Services;

public static class LocalizationManager
{
    private static string _language = "fr";

    public static string Language => _language;
    public static bool IsFrench => _language == "fr";

    public static event Action? LanguageChanged;

    public static void Initialize() => Apply();

    public static void SetLanguage(string language)
    {
        if (_language == language) return;
        _language = language;
        Apply();
    }

    public static void Toggle() => SetLanguage(_language == "fr" ? "en" : "fr");

    public static string T(string key) => Strings.Get(key, _language);

    private static void Apply()
    {
        foreach (var key in Strings.Keys)
            Application.Current.Resources[key] = Strings.Get(key, _language);

        LanguageChanged?.Invoke();
    }
}
