using System.Globalization;
using System.Windows;
using Zlet.FolderConverter.App.Localization;

namespace Zlet.FolderConverter.App;

public partial class LanguageChooserWindow : Window
{
    public LanguageChooserWindow()
    {
        InitializeComponent();
        if (CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ru") RussianButton.Focus();
        else EnglishButton.Focus();
    }

    public string? SelectedLanguage { get; private set; }
    private void ChooseRussian_Click(object sender, RoutedEventArgs e) => Choose(AppLanguage.Russian);
    private void ChooseEnglish_Click(object sender, RoutedEventArgs e) => Choose(AppLanguage.English);
    private void Choose(string language) { SelectedLanguage = language; DialogResult = true; }
}
