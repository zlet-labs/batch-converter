using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App.Localization;

public static class OperationMessageLocalizer
{
    private static readonly IReadOnlyDictionary<string, string> ErrorCodeKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["unsafe_source"] = "OperationUnsafeSource",
            ["unsafe_target"] = "OperationInvalidTarget",
            ["target_directory_missing"] = "OperationInvalidTarget",
            ["unsafe_target_after_create"] = "OperationInvalidTarget",
            ["target_conflict"] = "OperationTargetExists",
            ["source_unreadable"] = "OperationSourceUnreadable",
            ["output_missing"] = "OperationOutputMissing",
            ["output_empty"] = "OperationOutputInvalid",
            ["output_extension_invalid"] = "OperationOutputExtensionInvalid",
            ["ooxml_structure_invalid"] = "OperationOutputInvalid",
            ["pdf_signature_invalid"] = "OperationOutputInvalid",
            ["unsupported_output_validation"] = "OperationOutputInvalid",
            ["output_unreadable"] = "OperationOutputInvalid",
            ["source_changed"] = "OperationSourceChanged",
            ["io_failure"] = "OperationProcessingFailed",
            ["unexpected_adapter_failure"] = "OperationProcessingFailed",
            ["invalid_json"] = "OperationInvalidJson",
            ["copy_mapping_unsupported"] = "OperationCopyUnsupported",
            ["office_mapping_unsupported"] = "OperationUnsupported",
            ["office_application_missing"] = "OperationOfficeMissing",
            ["worker_missing"] = "OperationOfficeComponentUnavailable",
            ["worker_start_failure"] = "OperationOfficeComponentUnavailable",
            ["worker_timeout"] = "OperationTimeout",
            ["worker_protocol_failure"] = "OperationOfficeFailure",
            ["worker_protocol_invalid"] = "OperationOfficeFailure",
            ["worker_result_missing"] = "OperationOfficeFailure",
            ["powerpoint_already_running"] = "OperationPowerPointRunning",
            ["powerpoint_session_ownership_lost"] = "OperationPowerPointProtected",
            ["office_com_failure"] = "OperationOfficeFailure"
        };

    private static readonly IReadOnlyDictionary<string, string> KnownMessageKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Файл не будет изменён."] = "OperationSkipped",
            ["Выбранное преобразование не поддерживается."] = "OperationUnsupported",
            ["Недопустимый путь результата."] = "OperationInvalidTarget",
            ["Файл результата уже существует."] = "OperationTargetExists",
            ["Будет скопирован без изменений."] = "OperationReadyCopy",
            ["Готово к преобразованию."] = "OperationReady",
            ["Преобразование недоступно."] = "OperationUnavailable",
            ["Отменено пользователем."] = "OperationCancelled",
            ["Не удалось обработать файл."] = "OperationProcessingFailed",
            ["Копирование этого формата не поддерживается."] = "OperationCopyUnsupported",
            ["Исходный файл небезопасен или находится вне выбранной папки."] = "OperationUnsafeSource",
            ["Не удалось открыть исходный файл."] = "OperationSourceUnreadable",
            ["Приложение не создало ожидаемый результат."] = "OperationOutputMissing",
            ["Расширение результата не прошло проверку."] = "OperationOutputExtensionInvalid",
            ["Формат результата не прошёл проверку."] = "OperationOutputInvalid",
            ["Исходный файл изменился во время обработки."] = "OperationSourceChanged",
            ["Преобразование превысило допустимое время."] = "OperationTimeout",
            ["Не удалось преобразовать файл в Microsoft Office."] = "OperationOfficeFailure",
            ["Компонент преобразования Microsoft Office недоступен."] = "OperationOfficeComponentUnavailable",
            ["PowerPoint уже запущен. Закройте его и повторите преобразование."] = "OperationPowerPointRunning",
            ["PowerPoint не запустился."] = "OperationPowerPointStartFailed",
            ["PowerPoint не запустился. Откройте PowerPoint вручную и повторите."] = "OperationPowerPointStartFailedAdvice"
        };

    public static string Localize(
        OperationStatus status,
        ConversionTarget target,
        string? message,
        string? errorCode = null,
        LocalizationService? localization = null)
    {
        localization ??= LocalizationService.Current;
        if (!string.IsNullOrWhiteSpace(message) && KnownMessageKeys.TryGetValue(message, out var messageKey))
            return localization.Get(messageKey);
        if (!string.IsNullOrWhiteSpace(errorCode) && ErrorCodeKeys.TryGetValue(errorCode, out var errorKey))
            return localization.Get(errorKey);

        if (string.IsNullOrWhiteSpace(message))
        {
            return status switch
            {
                OperationStatus.Ready when target == ConversionTarget.Copy => localization.Get("OperationReadyCopy"),
                OperationStatus.Ready => localization.Get("OperationReady"),
                OperationStatus.Converting => localization.Get("OperationExecuting"),
                OperationStatus.Skipped => localization.Get("OperationSkipped"),
                OperationStatus.Cancelled => localization.Get("OperationCancelled"),
                OperationStatus.NotProcessed => localization.Get("OperationNotProcessed"),
                _ => string.Empty
            };
        }

        // Unknown diagnostics remain verbatim so troubleshooting meaning is never hidden.
        return message;
    }
}
