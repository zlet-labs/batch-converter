using System.Globalization;
using System.Windows;

namespace Zlet.FolderConverter.App.Localization;

public sealed class LocalizationService
{
    private const string DictionaryPrefix = "Resources/Strings.";
    private static readonly Lazy<LocalizationService> LazyCurrent = new(() => new());

    private ResourceDictionary? _dictionary;

    private LocalizationService()
    {
        try { _dictionary = LoadDictionary(AppLanguage.Russian); }
        catch { /* Tests and design tools can run without a WPF resource context. */ }
    }

    public static LocalizationService Current => LazyCurrent.Value;
    public string Language { get; private set; } = AppLanguage.Russian;
    public CultureInfo Culture => CultureInfo.GetCultureInfo(Language);
    public event EventHandler? LanguageChanged;

    public void Apply(string language)
    {
        if (!AppLanguage.IsSupported(language)) throw new ArgumentOutOfRangeException(nameof(language));
        language = AppLanguage.Normalize(language);
        var app = System.Windows.Application.Current;
        if (app is not null)
        {
            var dictionaries = app.Resources.MergedDictionaries;
            var old = dictionaries.FirstOrDefault(dictionary =>
                dictionary.Source?.OriginalString.Contains(DictionaryPrefix, StringComparison.OrdinalIgnoreCase) == true);
            var replacement = LoadDictionary(language);
            if (old is null) dictionaries.Insert(0, replacement);
            else dictionaries[dictionaries.IndexOf(old)] = replacement;
            _dictionary = replacement;
        }
        else
        {
            _dictionary = null;
        }
        Language = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key)
    {
        var value = System.Windows.Application.Current?.TryFindResource(key) as string ?? _dictionary?[key] as string;
        return value ?? FallbackStrings.Get(key, Language);
    }

    public string Format(string key, params object[] arguments) =>
        string.Format(Culture, Get(key), arguments);

    public string FileWord(int count)
        => LocalizationFormatting.FileWord(count, Language);

    public string FormatFileSize(long bytes)
    {
        return LocalizationFormatting.FormatFileSize(bytes, Language);
    }

    public string FormatExecutionTime(TimeSpan elapsed)
    {
        return LocalizationFormatting.FormatExecutionTime(elapsed, Language);
    }

    private static ResourceDictionary LoadDictionary(string language) => new()
    {
        Source = new Uri($"/{typeof(LocalizationService).Assembly.GetName().Name};component/Resources/Strings.{language}.xaml", UriKind.Absolute)
    };

    private static class FallbackStrings
    {
        public static string Get(string key, string language) => (language, key) switch
        {
            (AppLanguage.Russian, "FileOne") => "файл",
            (AppLanguage.Russian, "FileFew") => "файла",
            (AppLanguage.Russian, "FileMany") => "файлов",
            (AppLanguage.Russian, "MegabyteUnit") => "МБ",
            (AppLanguage.Russian, "SecondUnit") => "с",
            (AppLanguage.Russian, "TargetSkip") => "Не трогать",
            (AppLanguage.Russian, "TargetCopy") => "Копировать без изменений",
            (AppLanguage.Russian, "StatusNotSelected") => "Не выбрано",
            (AppLanguage.Russian, "StatusReadyCopy") => "Готово к копированию",
            (AppLanguage.Russian, "StatusReadyConvert") => "Готово к преобразованию",
            (AppLanguage.Russian, "StatusSkipped") => "Пропущено",
            (AppLanguage.Russian, "StatusConverting") => "В процессе",
            (AppLanguage.Russian, "StatusCopied") => "Скопировано",
            (AppLanguage.Russian, "StatusConverted") => "Преобразовано",
            (AppLanguage.Russian, "StatusConflict") => "Конфликт",
            (AppLanguage.Russian, "StatusFailed") => "Ошибка",
            (AppLanguage.Russian, "StatusFailedDetail") => "Ошибка: {0}",
            (AppLanguage.Russian, "StatusUnavailable") => "Недоступно",
            (AppLanguage.Russian, "StatusUnavailableDetail") => "Недоступно: {0}",
            (AppLanguage.Russian, "StatusCancelled") => "Отменено",
            (AppLanguage.Russian, "StatusNotProcessed") => "Не обработано",
            (AppLanguage.Russian, "StatusUnknown") => "Неизвестно",
            (AppLanguage.Russian, "CopiedFilesFormat") => "Скопировано {0} {1}",
            (AppLanguage.Russian, "NoSelectedFiles") => "Нет выбранных файлов для преобразования",
            (AppLanguage.Russian, "InitialState") => "Выберите папку и найдите файлы.",
            (AppLanguage.Russian, "InitialEmpty") => "Preview появится после сканирования папки.",
            (AppLanguage.Russian, "ElapsedFormat") => "Прошло: {0}",
            (AppLanguage.Russian, "RemainingCalculating") => "Осталось: рассчитываем…",
            (AppLanguage.Russian, "FinalComplete") => "Обработка завершена",
            (AppLanguage.Russian, "FilterAll") => "Все",
            (AppLanguage.Russian, "FilterConvert") => "К преобразованию",
            (AppLanguage.Russian, "FilterSkip") => "Не трогаем",
            (AppLanguage.Russian, "FilterUnavailable") => "Недоступно",
            (AppLanguage.Russian, "FilterConflicts") => "Конфликты",
            (AppLanguage.Russian, "FilterErrors") => "Ошибки",
            (AppLanguage.Russian, "OutputFolder") => "Папка",
            (AppLanguage.Russian, "OutputZip") => "ZIP-архив",
            (AppLanguage.Russian, "SelectionFormat") => "Выбрано: {0} из {1}",
            (AppLanguage.Russian, "ChooseFiles") => "Выберите файлы",
            (AppLanguage.Russian, "ConvertFilesFormat") => "Преобразовать {0} {1}",
            (AppLanguage.Russian, "ProgressCountFormat") => "{0} из {1}",
            (AppLanguage.Russian, "StoppedByUser") => "Остановлено пользователем",
            (AppLanguage.Russian, "ErrorCodeFormat") => "код: {0}",
            (AppLanguage.Russian, "NoExtension") => "Без расширения",
            (AppLanguage.Russian, "RemainingFormat") => "Осталось: ~{0}",
            (AppLanguage.Russian, "RemainingZero") => "Осталось: 00:00",
            (AppLanguage.Russian, "RuleChanged") => "Правило изменено. Preview обновлён.",
            (AppLanguage.Russian, "FinalDurationFormat") => "Время выполнения: {0}",
            (_, "FileOne") => "file",
            (_, "FileFew") => "files",
            (_, "FileMany") => "files",
            (_, "MegabyteUnit") => "MB",
            (_, "SecondUnit") => "s",
            (_, "TargetSkip") => "Skip",
            (_, "TargetCopy") => "Copy unchanged",
            (_, "StatusNotSelected") => "Not selected",
            (_, "StatusReadyCopy") => "Ready to copy",
            (_, "StatusReadyConvert") => "Ready to convert",
            (_, "StatusSkipped") => "Skipped",
            (_, "StatusConverting") => "In progress",
            (_, "StatusCopied") => "Copied",
            (_, "StatusConverted") => "Converted",
            (_, "StatusConflict") => "Conflict",
            (_, "StatusFailed") => "Failed",
            (_, "StatusFailedDetail") => "Failed: {0}",
            (_, "StatusUnavailable") => "Unavailable",
            (_, "StatusUnavailableDetail") => "Unavailable: {0}",
            (_, "StatusCancelled") => "Cancelled",
            (_, "StatusNotProcessed") => "Not processed",
            (_, "StatusUnknown") => "Unknown",
            (_, "CopiedFilesFormat") => "Copied {0} {1}",
            (_, "NoSelectedFiles") => "No files selected for conversion",
            (_, "StoppedByUser") => "Stopped by user",
            (_, "ErrorCodeFormat") => "code: {0}",
            (_, "NoExtension") => "No extension",
            (_, "RemainingFormat") => "Remaining: ~{0}",
            (_, "RemainingZero") => "Remaining: 00:00",
            (_, "RuleChanged") => "Rule changed. Preview updated.",
            (_, "FinalDurationFormat") => "Elapsed time: {0}",
            _ => key
        };
    }
}
