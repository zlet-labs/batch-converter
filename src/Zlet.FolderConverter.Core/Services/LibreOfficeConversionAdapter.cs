using System.ComponentModel;
using System.Security.Cryptography;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.Core.Services;

public sealed class LibreOfficeConversionAdapter(
    ILibreOfficeRuntimeLocator runtimeLocator,
    ILibreOfficeProcessRunner processRunner,
    IOutputResultValidator validator,
    LibreOfficeConversionOptions? options = null) : IConversionAdapter
{
    private readonly LibreOfficeConversionOptions _options = options ?? new LibreOfficeConversionOptions();

    public bool IsAvailable => runtimeLocator.Locate().IsAvailable;

    public string AvailabilityMessage => IsAvailable
        ? "LibreOffice доступен локально."
        : "LibreOffice не найден в portable package.";

    public bool CanConvert(SourceFormat sourceFormat, ConversionTarget target) =>
        FormatCapabilityCatalog.RequiresLibreOffice(sourceFormat, target);

    public async Task<ConversionResult> ConvertAsync(
        PlannedOperation operation,
        CancellationToken cancellationToken)
    {
        var runtime = runtimeLocator.Locate();
        if (!runtime.IsAvailable)
        {
            return Result(
                operation,
                OperationStatus.EngineUnavailable,
                "LibreOffice не найден в portable package.",
                "runtime_missing");
        }

        if (!CanConvert(operation.SourceFormat, operation.Target))
        {
            return Result(
                operation,
                OperationStatus.Unsupported,
                "Выбранное преобразование не поддерживается.",
                "mapping_unsupported");
        }

        if (!OutputPathGuard.IsSafeTargetPath(operation.TargetPath, operation.OutputRootPath))
        {
            return Result(
                operation,
                OperationStatus.Failed,
                "Недопустимый путь результата.",
                "unsafe_target");
        }

        if (File.Exists(operation.TargetPath) || Directory.Exists(operation.TargetPath))
        {
            return Result(
                operation,
                OperationStatus.Conflict,
                "Файл результата уже существует.",
                "target_conflict");
        }

        FileSnapshot sourceSnapshot;
        try
        {
            sourceSnapshot = await FileSnapshot.CreateAsync(operation.SourcePath, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result(
                operation,
                OperationStatus.Failed,
                "Не удалось открыть исходный файл.",
                "source_unreadable");
        }

        var temporaryRoot = Path.GetFullPath(
            _options.TemporaryRootPath
            ?? Path.Combine(Path.GetTempPath(), "ZletFolderConverter"));
        var operationRoot = Path.Combine(temporaryRoot, Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(operationRoot, "output");
        var profileDirectory = Path.Combine(operationRoot, "profile");
        string? stagingPath = null;

        try
        {
            Directory.CreateDirectory(outputDirectory);
            Directory.CreateDirectory(profileDirectory);
            var processResult = await processRunner.RunAsync(
                new LibreOfficeProcessRequest(
                    runtime.ExecutablePath,
                    operation.SourcePath,
                    outputDirectory,
                    profileDirectory,
                    operation.Target),
                _options.Timeout,
                cancellationToken);

            if (processResult.TimedOut)
            {
                return Result(
                    operation,
                    OperationStatus.Failed,
                    "Преобразование превысило допустимое время.",
                    "process_timeout",
                    processResult.ExitCode,
                    timedOut: true,
                    hasStandardOutput: !string.IsNullOrWhiteSpace(processResult.StandardOutput),
                    hasStandardError: !string.IsNullOrWhiteSpace(processResult.StandardError));
            }

            if (processResult.ExitCode != 0)
            {
                return Result(
                    operation,
                    OperationStatus.Failed,
                    "LibreOffice не создал ожидаемый результат.",
                    "process_exit_failure",
                    processResult.ExitCode,
                    hasStandardOutput: !string.IsNullOrWhiteSpace(processResult.StandardOutput),
                    hasStandardError: !string.IsNullOrWhiteSpace(processResult.StandardError));
            }

            var temporaryOutput = Path.Combine(
                outputDirectory,
                Path.GetFileNameWithoutExtension(operation.SourcePath) + operation.TargetExtension);
            if (!File.Exists(temporaryOutput))
            {
                return Result(
                    operation,
                    OperationStatus.Failed,
                    "LibreOffice не создал ожидаемый результат.",
                    "output_missing",
                    processResult.ExitCode,
                    hasStandardOutput: !string.IsNullOrWhiteSpace(processResult.StandardOutput),
                    hasStandardError: !string.IsNullOrWhiteSpace(processResult.StandardError));
            }

            var validation = validator.Validate(temporaryOutput, operation.Target);
            if (!validation.IsValid)
            {
                return Result(
                    operation,
                    OperationStatus.Failed,
                    "Формат результата не прошёл проверку.",
                    validation.ErrorCode,
                    processResult.ExitCode,
                    hasStandardOutput: !string.IsNullOrWhiteSpace(processResult.StandardOutput),
                    hasStandardError: !string.IsNullOrWhiteSpace(processResult.StandardError));
            }

            if (!await sourceSnapshot.IsUnchangedAsync(operation.SourcePath, cancellationToken))
            {
                return Result(
                    operation,
                    OperationStatus.Failed,
                    "Исходный файл изменился во время обработки.",
                    "source_changed",
                    processResult.ExitCode,
                    hasStandardOutput: !string.IsNullOrWhiteSpace(processResult.StandardOutput),
                    hasStandardError: !string.IsNullOrWhiteSpace(processResult.StandardError));
            }

            var targetDirectory = Path.GetDirectoryName(operation.TargetPath);
            if (targetDirectory is null)
            {
                return Result(
                    operation,
                    OperationStatus.Failed,
                    "Недопустимый путь результата.",
                    "target_directory_missing");
            }

            Directory.CreateDirectory(targetDirectory);
            if (!OutputPathGuard.IsSafeTargetPath(operation.TargetPath, operation.OutputRootPath))
            {
                return Result(
                    operation,
                    OperationStatus.Failed,
                    "Недопустимый путь результата.",
                    "unsafe_target_after_create");
            }

            if (File.Exists(operation.TargetPath) || Directory.Exists(operation.TargetPath))
            {
                return Result(
                    operation,
                    OperationStatus.Conflict,
                    "Файл результата уже существует.",
                    "target_conflict");
            }

            stagingPath = Path.Combine(
                targetDirectory,
                $".{Path.GetFileName(operation.TargetPath)}.{Guid.NewGuid():N}.tmp");
            File.Copy(temporaryOutput, stagingPath, overwrite: false);
            var stagingValidation = validator.Validate(stagingPath, operation.Target);
            if (!stagingValidation.IsValid)
            {
                return Result(
                    operation,
                    OperationStatus.Failed,
                    "Формат результата не прошёл проверку.",
                    stagingValidation.ErrorCode);
            }

            File.Move(stagingPath, operation.TargetPath, overwrite: false);
            stagingPath = null;
            var finalValidation = validator.Validate(operation.TargetPath, operation.Target);
            if (!finalValidation.IsValid)
            {
                File.Delete(operation.TargetPath);
                return Result(
                    operation,
                    OperationStatus.Failed,
                    "Формат результата не прошёл проверку.",
                    finalValidation.ErrorCode);
            }

            return new ConversionResult(operation, OperationStatus.Succeeded, "Преобразовано.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is Win32Exception
                                           or InvalidOperationException)
        {
            return Result(
                operation,
                OperationStatus.Failed,
                "Не удалось запустить LibreOffice.",
                "process_start_failure");
        }
        catch (IOException) when (File.Exists(operation.TargetPath)
                                  || Directory.Exists(operation.TargetPath))
        {
            return Result(
                operation,
                OperationStatus.Conflict,
                "Файл результата уже существует.",
                "target_conflict");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result(
                operation,
                OperationStatus.Failed,
                "Не удалось обработать файл.",
                "io_failure");
        }
        finally
        {
            TryDeleteFile(stagingPath);
            TryDeleteTemporaryDirectory(operationRoot, temporaryRoot);
        }
    }

    private static ConversionResult Result(
        PlannedOperation operation,
        OperationStatus status,
        string message,
        string errorCode,
        int? exitCode = null,
        bool timedOut = false,
        bool hasStandardOutput = false,
        bool hasStandardError = false) =>
        new(
            operation,
            status,
            message,
            new ConversionDiagnostic(
                errorCode,
                exitCode,
                timedOut,
                HasStandardOutput: hasStandardOutput,
                HasStandardError: hasStandardError));

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteTemporaryDirectory(string operationRoot, string temporaryRoot)
    {
        try
        {
            var root = Path.GetFullPath(temporaryRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var operation = Path.GetFullPath(operationRoot);
            if (operation.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(operation))
            {
                Directory.Delete(operation, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException)
        {
        }
    }

    private sealed record FileSnapshot(long Length, byte[] Hash)
    {
        public static async Task<FileSnapshot> CreateAsync(
            string path,
            CancellationToken cancellationToken)
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return new FileSnapshot(stream.Length, hash);
        }

        public async Task<bool> IsUnchangedAsync(
            string path,
            CancellationToken cancellationToken)
        {
            var current = await CreateAsync(path, cancellationToken);
            return Length == current.Length && Hash.SequenceEqual(current.Hash);
        }
    }
}
