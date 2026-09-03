using System.Windows;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.App.Settings;

namespace Zlet.FolderConverter.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = new AppSettingsStore();
        var saved = settings.LoadLanguage();
        var explicitLanguage = ReadArgument(e.Args, "--language=");
        var bootstrapLanguage = ReadArgument(e.Args, "--bootstrap-language=");

        if (bootstrapLanguage is not null)
        {
            if (saved is null && AppLanguage.IsSupported(bootstrapLanguage)) settings.SaveLanguage(bootstrapLanguage);
            Shutdown();
            return;
        }

        var decision = StartupLanguageResolver.Resolve(explicitLanguage, saved);
        var language = decision.Language;
        if (decision.ChooserRequired)
        {
            var chooser = new LanguageChooserWindow();
            if (chooser.ShowDialog() != true || chooser.SelectedLanguage is null) { Shutdown(); return; }
            language = chooser.SelectedLanguage;
            settings.SaveLanguage(language);
        }
        else if (decision.PersistExplicit) settings.SaveLanguage(language!);

        LocalizationService.Current.Apply(language!);
        MainWindow = new MainWindow();
        MainWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    internal static string? ReadArgument(IEnumerable<string> args, string prefix) =>
        args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];
}
