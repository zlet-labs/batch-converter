using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class MicrosoftOfficeConversionAdapter : IConversionAdapter
{
    private readonly OfficeApplicationKind _application;
    private readonly bool _officeAvailable;
    private readonly IMicrosoftOfficeWorkerRunner _workerRunner;
    private readonly SafeFileOperationExecutor _executor;

    public MicrosoftOfficeConversionAdapter(
        OfficeApplicationKind application,
        IMicrosoftOfficeCapabilityDetector capabilityDetector,
        IMicrosoftOfficeWorkerRunner workerRunner,
        IOutputResultValidator validator,
        string? temporaryRoot = null)
    {
        _application = application;
        _officeAvailable = capabilityDetector.Detect()
            .Single(item => item.Application == application)
            .IsAvailable;
        _workerRunner = workerRunner;
        _executor = new SafeFileOperationExecutor(validator, temporaryRoot);
    }

    public bool IsAvailable => _officeAvailable && _workerRunner.IsAvailable;

    public string AvailabilityMessage => !_officeAvailable
        ? _application.ToRequiredMessage()
        : !_workerRunner.IsAvailable
            ? "Компонент преобразования Microsoft Office недоступен."
            : $"{_application.ToDisplayName()} доступен.";

    public bool CanConvert(SourceFormat sourceFormat, ConversionTarget target) =>
        FormatCapabilityCatalog.RequiredOfficeApplication(sourceFormat, target) == _application;

    public Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        CancellationToken cancellationToken)
    {
        if (!CanConvert(operation.SourceFormat, operation.Target))
        {
            return Task.FromResult(new ConversionResult(
                operation,
                OperationStatus.Unsupported,
                "Выбранное преобразование не поддерживается.",
                new ConversionDiagnostic("office_mapping_unsupported")));
        }

        if (!IsAvailable)
        {
            return Task.FromResult(new ConversionResult(
                operation,
                OperationStatus.EngineUnavailable,
                AvailabilityMessage,
                new ConversionDiagnostic(
                    _officeAvailable ? "worker_missing" : "office_application_missing")));
        }

        return _executor.ExecuteAsync(
            operation,
            operation.Target,
            async (temporaryOutput, token) =>
            {
                var workerResult = await _workerRunner.RunAsync(
                    new OfficeWorkerRequest(
                        _application,
                        operation.SourcePath,
                        temporaryOutput),
                    token);
                return workerResult.Success
                    ? new TemporaryOutputProductionResult(true)
                    : new TemporaryOutputProductionResult(
                        false,
                        workerResult.ErrorCode,
                        workerResult.TimedOut
                            ? "Преобразование превысило допустимое время."
                            : "Не удалось преобразовать файл в Microsoft Office.",
                        workerResult.TimedOut,
                        workerResult.ExitCode,
                        workerResult.HasStandardOutput,
                        workerResult.HasStandardError,
                        workerResult.HResult);
            },
            "Преобразовано.",
            cancellationToken);
    }
}
