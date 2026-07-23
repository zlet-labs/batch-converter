using System.Diagnostics;

namespace Zlet.FolderConverter.Core.Services;

public sealed class LibreOfficeProcessRunner : ILibreOfficeProcessRunner
{
    public async Task<LibreOfficeProcessResult> RunAsync(
        LibreOfficeProcessRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var startInfo = CreateStartInfo(request);
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("LibreOffice process did not start.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var timeoutCancellation = new CancellationTokenSource(timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token);

        try
        {
            await process.WaitForExitAsync(linkedCancellation.Token);
        }
        catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested
                                                 && !cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            await process.WaitForExitAsync(CancellationToken.None);
            return new LibreOfficeProcessResult(
                process.HasExited ? process.ExitCode : null,
                await stdoutTask,
                await stderrTask,
                TimedOut: true);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        return new LibreOfficeProcessResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private static ProcessStartInfo CreateStartInfo(LibreOfficeProcessRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.OutputDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--nodefault");
        startInfo.ArgumentList.Add("--nofirststartwizard");
        startInfo.ArgumentList.Add("--nolockcheck");
        startInfo.ArgumentList.Add(
            $"-env:UserInstallation={new Uri(Path.GetFullPath(request.UserProfileDirectory)).AbsoluteUri}");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add(GetLibreOfficeTarget(request.Target));
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(request.OutputDirectory);
        startInfo.ArgumentList.Add(request.SourcePath);
        return startInfo;
    }

    private static string GetLibreOfficeTarget(Models.ConversionTarget target) => target switch
    {
        Models.ConversionTarget.Docx => "docx",
        Models.ConversionTarget.Xlsx => "xlsx",
        Models.ConversionTarget.Pptx => "pptx",
        Models.ConversionTarget.Pdf => "pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported LibreOffice target.")
    };

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
