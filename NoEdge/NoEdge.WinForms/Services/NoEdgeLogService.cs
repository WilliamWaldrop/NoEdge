using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NoEdge.WinForms.Services;

public sealed class NoEdgeLogService
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false
    };

    public string DataDirectory { get; }

    public string LogDirectory { get; }

    public string BackupDirectory { get; }

    public string SessionLogFilePath { get; }

    public string SessionId { get; } = Guid.NewGuid().ToString();

    public NoEdgeLogService()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            ),
            "NoEdge"
        );

        LogDirectory = Path.Combine(DataDirectory, "Logs");

        BackupDirectory = Path.Combine(DataDirectory, "Backups");

        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(BackupDirectory);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        SessionLogFilePath = Path.Combine(
            LogDirectory,
            $"NoEdge_{timestamp}.jsonl"
        );
    }

    public async Task WriteAsync(
        string message,
        NoEdgeLogLevel level = NoEdgeLogLevel.Info,
        string eventName = "General",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "A log message is required.",
                nameof(message)
            );
        }

        var entry = new NoEdgeLogEntry(
            DateTimeOffset.Now,
            SessionId,
            "NoEdge",
            "0.1.0-dev",
            level,
            eventName,
            message
        );

        var json = JsonSerializer.Serialize(entry, _jsonOptions);

        await WriteLock.WaitAsync(cancellationToken);

        try
        {
            await File.AppendAllTextAsync(
                SessionLogFilePath,
                json + Environment.NewLine,
                cancellationToken
            );
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public IReadOnlyList<NoEdgeLogEntry> ReadCurrentSession()
    {
        if (!File.Exists(SessionLogFilePath))
        {
            return Array.Empty<NoEdgeLogEntry>();
        }

        var entries = new List<NoEdgeLogEntry>();

        foreach (var line in File.ReadLines(SessionLogFilePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<NoEdgeLogEntry>(
                    line,
                    _jsonOptions
                );

                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
                // Ignore malformed log entries instead of breaking the GUI.
            }
        }

        return entries
            .OrderBy(entry => entry.Timestamp)
            .ToList();
    }

    public void OpenLogFolder()
    {
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{LogDirectory}\"",
                UseShellExecute = true
            }
        );
    }
}

public enum NoEdgeLogLevel
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record NoEdgeLogEntry(
    DateTimeOffset Timestamp,
    string SessionId,
    string Tool,
    string Version,
    NoEdgeLogLevel Level,
    string EventName,
    string Message
);