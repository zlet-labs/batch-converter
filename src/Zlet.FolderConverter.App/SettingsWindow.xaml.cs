using System.Windows;
using System.Windows.Controls;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.App.Settings;

namespace Zlet.FolderConverter.App;

public partial class SettingsWindow : Window
{
    private bool _initialized;
    public SettingsWindow()
    {
        InitializeComponent();
        (LocalizationService.Current.Language == AppLanguage.Russian ? RussianButton : EnglishButton).IsChecked = true;
        _initialized = true;
    }

    private void Language_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initialized || sender is not System.Windows.Controls.RadioButton { Tag: string language }) return;
        LocalizationService.Current.Apply(language);
        new AppSettingsStore().SaveLanguage(language);
    }
}
