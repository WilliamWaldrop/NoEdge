using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace NoEdge.WinForms.Services;

public sealed class EdgeUninstallService
{
    private readonly NoEdgeLogService _logService;

    public EdgeUninstallService(NoEdgeLogService logService)
    {
        _logService = logService ?? throw new ArgumentNullException(
            nameof(logService)
        );
    }

    public bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();

        var principal = new WindowsPrincipal(identity);

        return principal.IsInRole(
            WindowsBuiltInRole.Administrator
        );
    }

    public string BuildCommandPreview(string installerPath)
    {
        if (string.IsNullOrWhiteSpace(installerPath))
        {
            throw new ArgumentException(
                "An Edge installer path is required.",
                nameof(installerPath)
            );
        }

        return
            $"\"{installerPath}\" " +
            "--uninstall " +
            "--system-level " +
            "--verbose-logging " +
            "--force-uninstall";
    }

    public async Task<EdgeUninstallResult> UninstallAsync(
        string installerPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(installerPath))
        {
            return EdgeUninstallResult.Failed(
                "No Edge setup.exe installer path was supplied."
            );
        }

        if (!System.IO.File.Exists(installerPath))
        {
            return EdgeUninstallResult.Failed(
                "The detected Edge setup.exe file no longer exists."
            );
        }

        if (!IsAdministrator())
        {
            return EdgeUninstallResult.Failed(
                "Administrator rights are required to uninstall Edge."
            );
        }

        var commandPreview = BuildCommandPreview(installerPath);

        try
        {
            await _logService.WriteAsync(
                $"Starting Edge uninstall: {commandPreview}",
                NoEdgeLogLevel.Warning,
                "EdgeUninstall"
            );

            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("--uninstall");
            startInfo.ArgumentList.Add("--system-level");
            startInfo.ArgumentList.Add("--verbose-logging");
            startInfo.ArgumentList.Add("--force-uninstall");

            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return EdgeUninstallResult.Failed(
                    "Edge setup.exe could not be started."
                );
            }

            var standardOutputTask =
                process.StandardOutput.ReadToEndAsync();

            var standardErrorTask =
                process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;

            var result = new EdgeUninstallResult(
                process.ExitCode == 0,
                process.ExitCode,
                standardOutput,
                standardError,
                commandPreview,
                process.ExitCode == 0
                    ? "The Edge uninstaller completed successfully."
                    : $"Edge setup.exe exited with code {process.ExitCode}."
            );

            await _logService.WriteAsync(
                result.Message,
                result.Success
                    ? NoEdgeLogLevel.Success
                    : NoEdgeLogLevel.Error,
                "EdgeUninstall"
            );

            return result;
        }
        catch (OperationCanceledException)
        {
            const string message = "Edge uninstall was cancelled.";

            await _logService.WriteAsync(
                message,
                NoEdgeLogLevel.Warning,
                "EdgeUninstall"
            );

            return EdgeUninstallResult.Failed(message);
        }
        catch (Exception exception)
        {
            await _logService.WriteAsync(
                $"Edge uninstall failed: {exception.Message}",
                NoEdgeLogLevel.Error,
                "EdgeUninstall"
            );

            return EdgeUninstallResult.Failed(
                exception.Message
            );
        }
    }
}

public sealed record EdgeUninstallResult(
    bool Success,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    string CommandPreview,
    string Message
)
{
    public static EdgeUninstallResult Failed(string message)
    {
        return new EdgeUninstallResult(
            false,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            message
        );
    }
}