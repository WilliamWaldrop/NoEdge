using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NoEdge.WinForms.Services;

public sealed class WinGetService
{
    public bool IsAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "winget.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("--version");

            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return false;
            }

            process.WaitForExit(5_000);

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<WinGetResult> InstallAsync(
        BrowserCatalogItem browser,
        CancellationToken cancellationToken = default)
    {
        if (browser is null)
        {
            throw new ArgumentNullException(nameof(browser));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "winget.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("install");
        startInfo.ArgumentList.Add("--id");
        startInfo.ArgumentList.Add(browser.PackageId);
        startInfo.ArgumentList.Add("--exact");
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add("winget");
        startInfo.ArgumentList.Add("--accept-package-agreements");
        startInfo.ArgumentList.Add("--accept-source-agreements");
        startInfo.ArgumentList.Add("--disable-interactivity");

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return new WinGetResult(
                    false,
                    null,
                    string.Empty,
                    "WinGet could not be started.",
                    BuildInstallPreview(browser)
                );
            }

            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;

            return new WinGetResult(
                process.ExitCode == 0,
                process.ExitCode,
                standardOutput,
                standardError,
                BuildInstallPreview(browser)
            );
        }
        catch (OperationCanceledException)
        {
            return new WinGetResult(
                false,
                null,
                string.Empty,
                "Browser installation was cancelled.",
                BuildInstallPreview(browser)
            );
        }
        catch (Exception exception)
        {
            return new WinGetResult(
                false,
                null,
                string.Empty,
                exception.Message,
                BuildInstallPreview(browser)
            );
        }
    }

    public string BuildInstallPreview(BrowserCatalogItem browser)
    {
        if (browser is null)
        {
            throw new ArgumentNullException(nameof(browser));
        }

        return string.Join(
            " ",
            new[]
            {
                "winget install",
                $"--id {browser.PackageId}",
                "--exact",
                "--source winget",
                "--accept-package-agreements",
                "--accept-source-agreements",
                "--disable-interactivity"
            }
        );
    }
}

public sealed record WinGetResult(
    bool Success,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    string CommandPreview
);