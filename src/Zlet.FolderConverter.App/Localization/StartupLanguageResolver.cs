namespace Zlet.FolderConverter.App.Localization;

public readonly record struct StartupLanguageDecision(string? Language, bool PersistExplicit, bool ChooserRequired);

public static class StartupLanguageResolver
{
    public static StartupLanguageDecision Resolve(string? explicitLanguage, string? savedLanguage)
    {
        if (AppLanguage.IsSupported(explicitLanguage))
            return new(AppLanguage.Normalize(explicitLanguage!), true, false);
        if (AppLanguage.IsSupported(savedLanguage))
            return new(AppLanguage.Normalize(savedLanguage!), false, false);
        return new(null, false, true);
    }
}
