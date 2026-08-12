using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.Json;

namespace NoEdge.WinForms.Services;

public sealed class BrowserPolicyService
{
    private readonly NoEdgeLogService _logService;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public BrowserPolicyService(NoEdgeLogService logService)
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

    public string BuildPreview(BrowserCleanupProfile profile)
    {
        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        var settings = profile.Settings
            .Select(setting => $"{setting.Name} = {setting.Value}");

        return string.Join(Environment.NewLine, settings);
    }

    public PolicyOperationResult ApplyProfile(
        BrowserCleanupProfile profile)
    {
        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (!IsAdministrator())
        {
            return PolicyOperationResult.Failed(
                "Administrator rights are required to apply Cleanup Profiles."
            );
        }

        try
        {
            var backupPath = GetBackupPath(profile.Id);

            if (!File.Exists(backupPath))
            {
                CreateBackup(profile, backupPath);
            }

            using var key = OpenOrCreatePolicyKey(profile.RegistryPath);

            foreach (var setting in profile.Settings)
            {
                key.SetValue(
                    setting.Name,
                    setting.Value,
                    RegistryValueKind.DWord
                );
            }

            _logService.WriteAsync(
                $"{profile.BrowserName} Cleanup Profile applied.",
                NoEdgeLogLevel.Success,
                "CleanupProfile"
            ).GetAwaiter().GetResult();

            return PolicyOperationResult.Succeeded(
                $"{profile.BrowserName} Cleanup Profile was applied.",
                backupPath
            );
        }
        catch (Exception exception)
        {
            _logService.WriteAsync(
                $"{profile.BrowserName} Cleanup Profile failed: " +
                exception.Message,
                NoEdgeLogLevel.Error,
                "CleanupProfile"
            ).GetAwaiter().GetResult();

            return PolicyOperationResult.Failed(
                exception.Message
            );
        }
    }

    public PolicyOperationResult RestoreProfile(
        BrowserCleanupProfile profile)
    {
        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (!IsAdministrator())
        {
            return PolicyOperationResult.Failed(
                "Administrator rights are required to restore Cleanup Profiles."
            );
        }

        var backupPath = GetBackupPath(profile.Id);

        if (!File.Exists(backupPath))
        {
            return PolicyOperationResult.Failed(
                $"No backup exists for the {profile.BrowserName} Cleanup Profile."
            );
        }

        try
        {
            var backupJson = File.ReadAllText(backupPath);

            var backup = JsonSerializer.Deserialize<PolicyBackup>(
                backupJson,
                _jsonOptions
            );

            if (backup is null)
            {
                return PolicyOperationResult.Failed(
                    "The Cleanup Profile backup could not be read."
                );
            }

            using var key = OpenOrCreatePolicyKey(
                backup.RegistryPath
            );

            foreach (var value in backup.Values)
            {
                if (value.Existed)
                {
                    key.SetValue(
                        value.Name,
                        value.Value ?? 0,
                        RegistryValueKind.DWord
                    );
                }
                else
                {
                    key.DeleteValue(
                        value.Name,
                        throwOnMissingValue: false
                    );
                }
            }

            _logService.WriteAsync(
                $"{profile.BrowserName} Cleanup Profile restored.",
                NoEdgeLogLevel.Success,
                "CleanupProfileRestore"
            ).GetAwaiter().GetResult();

            return PolicyOperationResult.Succeeded(
                $"{profile.BrowserName} Cleanup Profile was restored.",
                backupPath
            );
        }
        catch (Exception exception)
        {
            _logService.WriteAsync(
                $"{profile.BrowserName} Cleanup Profile restore failed: " +
                exception.Message,
                NoEdgeLogLevel.Error,
                "CleanupProfileRestore"
            ).GetAwaiter().GetResult();

            return PolicyOperationResult.Failed(
                exception.Message
            );
        }
    }

    private void CreateBackup(
        BrowserCleanupProfile profile,
        string backupPath)
    {
        using var key = OpenOrCreatePolicyKey(profile.RegistryPath);

        var values = new List<PolicyBackupValue>();

        foreach (var setting in profile.Settings)
        {
            var existingValue = key.GetValue(
                setting.Name,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames
            );

            if (existingValue is null)
            {
                values.Add(
                    new PolicyBackupValue(
                        setting.Name,
                        false,
                        null
                    )
                );

                continue;
            }

            if (existingValue is not int integerValue)
            {
                throw new InvalidOperationException(
                    $"Existing registry value '{setting.Name}' is not a DWORD. " +
                    "NoEdge will not overwrite it."
                );
            }

            values.Add(
                new PolicyBackupValue(
                    setting.Name,
                    true,
                    integerValue
                )
            );
        }

        var backup = new PolicyBackup(
            DateTimeOffset.Now,
            profile.Id,
            profile.RegistryPath,
            values
        );

        var json = JsonSerializer.Serialize(
            backup,
            _jsonOptions
        );

        File.WriteAllText(backupPath, json);
    }

    private string GetBackupPath(string profileId)
    {
        return Path.Combine(
            _logService.BackupDirectory,
            $"{profileId}-cleanup-profile-backup.json"
        );
    }

    private static RegistryKey OpenOrCreatePolicyKey(
        string registryPath)
    {
        const string machinePrefix = @"HKEY_LOCAL_MACHINE\";

        if (!registryPath.StartsWith(
                machinePrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "NoEdge only supports machine-level policy registry paths."
            );
        }

        var subKeyPath = registryPath[machinePrefix.Length..];

        using var localMachine = RegistryKey.OpenBaseKey(
            RegistryHive.LocalMachine,
            RegistryView.Default
        );

        return localMachine.CreateSubKey(
            subKeyPath,
            writable: true
        ) ?? throw new InvalidOperationException(
            $"NoEdge could not create or access: {registryPath}"
        );
    }
}

public sealed record PolicyOperationResult(
    bool Success,
    string Message,
    string? BackupPath
)
{
    public static PolicyOperationResult Succeeded(
        string message,
        string backupPath)
    {
        return new PolicyOperationResult(
            true,
            message,
            backupPath
        );
    }

    public static PolicyOperationResult Failed(string message)
    {
        return new PolicyOperationResult(
            false,
            message,
            null
        );
    }
}

public sealed record PolicyBackup(
    DateTimeOffset CreatedAt,
    string ProfileId,
    string RegistryPath,
    IReadOnlyList<PolicyBackupValue> Values
);

public sealed record PolicyBackupValue(
    string Name,
    bool Existed,
    int? Value
);