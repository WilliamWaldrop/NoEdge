using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NoEdge.WinForms.Services;

public sealed class EdgeService
{
    public EdgeInstallationInfo GetInstallationInfo()
    {
        var applicationDirectories = GetEdgeApplicationDirectories();

        var edgeExecutable = applicationDirectories
            .Select(directory => Path.Combine(directory, "msedge.exe"))
            .FirstOrDefault(File.Exists);

        var edgeDirectory = edgeExecutable is not null
            ? Path.GetDirectoryName(edgeExecutable)
            : applicationDirectories.FirstOrDefault(Directory.Exists);

        var installerPath = FindEdgeInstaller(edgeDirectory);

        var version = GetFileVersion(edgeExecutable);

        var webView2Directory = GetWebView2Directory();

        return new EdgeInstallationInfo(
            edgeExecutable,
            edgeDirectory,
            installerPath,
            version,
            webView2Directory
        );
    }

    public IReadOnlyList<EdgeInventoryItem> GetInventory(
        string? edgeDirectory)
    {
        if (string.IsNullOrWhiteSpace(edgeDirectory) ||
            !Directory.Exists(edgeDirectory))
        {
            return Array.Empty<EdgeInventoryItem>();
        }

        var inventory = new List<EdgeInventoryItem>();

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var directory in Directory.EnumerateDirectories(
                edgeDirectory,
                "*",
                options))
            {
                inventory.Add(
                    new EdgeInventoryItem(
                        "Folder",
                        directory,
                        null
                    )
                );
            }

            foreach (var file in Directory.EnumerateFiles(
                edgeDirectory,
                "*",
                options))
            {
                long? sizeBytes = null;

                try
                {
                    sizeBytes = new FileInfo(file).Length;
                }
                catch (UnauthorizedAccessException)
                {
                    // Keep the file listed even if its size cannot be read.
                }
                catch (IOException)
                {
                    // Keep the file listed even if its size cannot be read.
                }

                inventory.Add(
                    new EdgeInventoryItem(
                        "File",
                        file,
                        sizeBytes
                    )
                );
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Return everything that could be enumerated.
        }
        catch (IOException)
        {
            // Return everything that could be enumerated.
        }

        return inventory
            .OrderBy(item => item.ItemType)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> GetEdgeApplicationDirectories()
    {
        var programFiles = Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFiles
        );

        var programFilesX86 = Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFilesX86
        );

        return new[]
        {
            Path.Combine(
                programFiles,
                "Microsoft",
                "Edge",
                "Application"
            ),

            Path.Combine(
                programFilesX86,
                "Microsoft",
                "Edge",
                "Application"
            )
        }
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    private static string? FindEdgeInstaller(string? edgeDirectory)
    {
        if (string.IsNullOrWhiteSpace(edgeDirectory) ||
            !Directory.Exists(edgeDirectory))
        {
            return null;
        }

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            return Directory.EnumerateFiles(
                    edgeDirectory,
                    "setup.exe",
                    options
                )
                .Where(path =>
                {
                    var parentDirectory = Directory.GetParent(path);

                    return parentDirectory is not null &&
                           parentDirectory.Name.Equals(
                               "Installer",
                               StringComparison.OrdinalIgnoreCase
                           );
                })
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? GetFileVersion(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) ||
            !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            return FileVersionInfo
                .GetVersionInfo(filePath)
                .ProductVersion;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetWebView2Directory()
    {
        var programFiles = Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFiles
        );

        var programFilesX86 = Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFilesX86
        );

        var webView2Directories = new[]
        {
            Path.Combine(
                programFiles,
                "Microsoft",
                "EdgeWebView",
                "Application"
            ),

            Path.Combine(
                programFilesX86,
                "Microsoft",
                "EdgeWebView",
                "Application"
            )
        };

        return webView2Directories.FirstOrDefault(Directory.Exists);
    }
}

public sealed record EdgeInstallationInfo(
    string? EdgeExecutablePath,
    string? EdgeDirectory,
    string? EdgeInstallerPath,
    string? EdgeVersion,
    string? WebView2Directory
)
{
    public bool IsEdgeInstalled =>
        !string.IsNullOrWhiteSpace(EdgeExecutablePath);

    public bool IsEdgeInstallerAvailable =>
        !string.IsNullOrWhiteSpace(EdgeInstallerPath);

    public bool IsWebView2Detected =>
        !string.IsNullOrWhiteSpace(WebView2Directory);
}

public sealed record EdgeInventoryItem(
    string ItemType,
    string Path,
    long? SizeBytes
)
{
    public string SizeDisplay =>
        SizeBytes is null
            ? string.Empty
            : $"{SizeBytes.Value / 1024d / 1024d:N2} MB";
}