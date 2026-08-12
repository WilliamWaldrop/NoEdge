using NoEdge.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NoEdge.WinForms;

public sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(32, 32, 32);
    private static readonly Color HeaderBackground = Color.FromArgb(25, 25, 25);
    private static readonly Color Primary = Color.FromArgb(0, 120, 212);
    private static readonly Color Danger = Color.FromArgb(180, 45, 45);
    private static readonly Color Muted = Color.FromArgb(90, 90, 90);

    private readonly EdgeService _edgeService = new();
    private readonly BrowserCatalogService _browserCatalog = new();
    private readonly WinGetService _winGetService = new();
    private readonly NoEdgeLogService _logService = new();
    private readonly BrowserCleanupProfileService _profileCatalog = new();
    private readonly BrowserPolicyService _policyService;
    private readonly EdgeUninstallService _edgeUninstallService;

    private readonly TabControl _tabs = new();
    private readonly ToolStripStatusLabel _status = new("Ready.");
    private readonly HashSet<string> _loadedTabs = new(StringComparer.OrdinalIgnoreCase);

    private RichTextBox? _logBox;
    private PictureBox? _edgeIcon;
    private Label? _edgeInfo;
    private DataGridView? _edgeGrid;
    private EdgeInstallationInfo? _edgeInstallation;

    public MainForm()
    {
        _policyService = new BrowserPolicyService(_logService);
        _edgeUninstallService = new EdgeUninstallService(_logService);

        Text = "NoEdge 0.4.0-dev";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);
        Size = new Size(1100, 740);
        BackColor = Background;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);

        BuildInterface();
        LoadDashboardTab();
        _ = LogAsync("NoEdge WinForms GUI started.", NoEdgeLogLevel.Info, "Startup");
    }

    private void BuildInterface()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            BackColor = HeaderBackground
        };

        header.Controls.Add(new Label
        {
            Text = "NoEdge",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 20f),
            ForeColor = Color.White,
            Location = new Point(20, 9)
        });

        header.Controls.Add(new Label
        {
            Text = "Browser control, Cleanup Profiles, and Edge management",
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.Silver,
            Location = new Point(23, 43)
        });

        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = new Font("Segoe UI", 10f);
        _tabs.SelectedIndexChanged += TabsOnSelectedIndexChanged;

        _tabs.TabPages.Add(CreateTab("Dashboard", "Dashboard"));
        _tabs.TabPages.Add(CreateTab("Edge", "Edge"));
        _tabs.TabPages.Add(CreateTab("Install", "Install Browser"));
        _tabs.TabPages.Add(CreateTab("Cleanup", "Cleanup Profiles"));
        _tabs.TabPages.Add(CreateTab("Logs", "Logs"));

        var statusBar = new StatusStrip
        {
            BackColor = HeaderBackground,
            ForeColor = Color.Gainsboro,
            SizingGrip = false
        };
        statusBar.Items.Add(_status);

        Controls.Add(_tabs);
        Controls.Add(statusBar);
        Controls.Add(header);
    }

    private static TabPage CreateTab(string name, string text) => new(text)
    {
        Name = name,
        BackColor = Background,
        ForeColor = Color.White,
        Padding = new Padding(18)
    };

    private async void TabsOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_tabs.SelectedTab is null)
        {
            return;
        }

        switch (_tabs.SelectedTab.Name)
        {
            case "Dashboard":
                LoadDashboardTab();
                break;
            case "Edge":
                await LoadEdgeTabAsync();
                break;
            case "Install":
                LoadInstallTab();
                break;
            case "Cleanup":
                LoadCleanupTab();
                break;
            case "Logs":
                LoadLogsTab();
                break;
        }
    }

    private void LoadDashboardTab()
    {
        if (!TryBeginTabLoad("Dashboard", out var tab))
        {
            return;
        }

        var panel = CreateVerticalPanel();
        tab.Controls.Add(panel);
        panel.Controls.Add(CreateTitle("NoEdge Dashboard"));

        var summary = CreateText("Loading system status...");
        panel.Controls.Add(summary);

        var refresh = CreateButton("Refresh Dashboard", Primary, (_, _) =>
        {
            summary.Text = BuildDashboardSummary();
            SetStatus("Dashboard refreshed.");
        });

        panel.Controls.Add(refresh);
        panel.Controls.Add(CreateText(
            "The Edge application directory is not scanned at startup. " +
            "NoEdge loads Edge details only when the Edge tab is opened."
        ));

        summary.Text = BuildDashboardSummary();
    }

    private string BuildDashboardSummary()
    {
        return string.Join(Environment.NewLine, new[]
        {
            $"Windows: {Environment.OSVersion.VersionString}",
            $"Administrator: {_policyService.IsAdministrator()}",
            $"WinGet available: {_winGetService.IsAvailable()}",
            "Edge inventory: Not loaded until the Edge tab is selected.",
            $"Logs: {_logService.SessionLogFilePath}"
        });
    }

    private async Task LoadEdgeTabAsync()
    {
        if (!TryBeginTabLoad("Edge", out var tab))
        {
            return;
        }

        var panel = CreateVerticalPanel();
        tab.Controls.Add(panel);
        panel.Controls.Add(CreateTitle("Microsoft Edge"));

        var header = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };

        _edgeIcon = new PictureBox
        {
            Size = new Size(64, 64),
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Background
        };

        _edgeInfo = CreateText("Loading Edge inventory...");
        _edgeInfo.Font = new Font("Segoe UI", 10f);

        header.Controls.Add(_edgeIcon);
        header.Controls.Add(_edgeInfo);
        panel.Controls.Add(header);

        panel.Controls.Add(CreateText(
            "The inventory lists files and folders beneath the detected Edge " +
            "application directory. It is informational: Edge setup.exe decides " +
            "what it can uninstall. NoEdge does not directly delete these files, " +
            "and WebView2 is explicitly excluded."
        ));

        _edgeGrid = new DataGridView
        {
            Width = 950,
            Height = 280,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.FromArgb(45, 45, 48),
            ForeColor = Color.Black,
            Margin = new Padding(0, 8, 0, 8)
        };
        panel.Controls.Add(_edgeGrid);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        actions.Controls.Add(CreateButton(
            "Refresh Edge Inventory",
            Primary,
            async (_, _) => await RefreshEdgeInventoryAsync()
        ));

        actions.Controls.Add(CreateButton(
            "Preview Edge Cleanup",
            Muted,
            (_, _) => ShowProfilePreview("Edge")
        ));

        actions.Controls.Add(CreateButton(
            "Apply Edge Cleanup",
            Primary,
            async (_, _) => await ApplyProfileAsync("Edge")
        ));

        actions.Controls.Add(CreateButton(
            "Restore Edge Cleanup",
            Muted,
            async (_, _) => await RestoreProfileAsync("Edge")
        ));

        actions.Controls.Add(CreateButton(
            "Uninstall Edge",
            Danger,
            async (_, _) => await UninstallEdgeAsync()
        ));

        panel.Controls.Add(actions);
        await RefreshEdgeInventoryAsync();
    }

    private async Task RefreshEdgeInventoryAsync()
    {
        if (_edgeInfo is null || _edgeGrid is null)
        {
            return;
        }

        _edgeInfo.Text = "Lazy-loading Edge inventory...";
        SetStatus("Scanning Edge because the Edge tab is open...");

        var result = await Task.Run(() =>
        {
            var installation = _edgeService.GetInstallationInfo();
            var inventory = _edgeService.GetInventory(installation.EdgeDirectory);
            return (installation, inventory);
        });

        _edgeInstallation = result.installation;
        _edgeGrid.DataSource = result.inventory.ToList();

        if (!string.IsNullOrWhiteSpace(_edgeInstallation.EdgeExecutablePath))
        {
            try
            {
                using var icon = Icon.ExtractAssociatedIcon(
                    _edgeInstallation.EdgeExecutablePath
                );

                _edgeIcon?.Image?.Dispose();
                _edgeIcon!.Image = icon?.ToBitmap();
            }
            catch
            {
                // An icon is cosmetic; failure should not break Edge inventory.
            }
        }

        _edgeInfo.Text = string.Join(Environment.NewLine, new[]
        {
            "Microsoft Edge",
            $"Version: {_edgeInstallation.EdgeVersion ?? "Not detected"}",
            $"Browser: {_edgeInstallation.EdgeExecutablePath ?? "Not detected"}",
            $"Uninstaller: {_edgeInstallation.EdgeInstallerPath ?? "Not found"}",
            $"WebView2 protected: {_edgeInstallation.WebView2Directory ?? "Not detected"}"
        });

        await LogAsync(
            $"Lazy-loaded Edge inventory. Root={_edgeInstallation.EdgeDirectory ?? "not found"}",
            NoEdgeLogLevel.Info,
            "EdgeInventory"
        );

        SetStatus("Edge inventory loaded.");
    }

    private void LoadInstallTab()
    {
        if (!TryBeginTabLoad("Install", out var tab))
        {
            return;
        }

        var panel = CreateVerticalPanel();
        tab.Controls.Add(panel);
        panel.Controls.Add(CreateTitle("Install a Browser"));
        panel.Controls.Add(CreateText(
            "Install a replacement browser before removing Edge. Each button " +
            "shows the exact WinGet package ID and asks for confirmation."
        ));

        foreach (var browser in _browserCatalog.GetAll())
        {
            panel.Controls.Add(CreateButton(
                $"Install {browser.Name} — {browser.PackageId}",
                Primary,
                async (_, _) => await InstallBrowserAsync(browser)
            ));
        }
    }

    private async Task InstallBrowserAsync(BrowserCatalogItem browser)
    {
        var command = _winGetService.BuildInstallPreview(browser);
        var message =
            $"Install {browser.Name}?{Environment.NewLine}{Environment.NewLine}" +
            $"Publisher: {browser.Publisher}{Environment.NewLine}" +
            $"Package ID: {browser.PackageId}{Environment.NewLine}{Environment.NewLine}" +
            $"Command:{Environment.NewLine}{command}";

        if (MessageBox.Show(
                message,
                "Confirm browser installation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        SetStatus($"Installing {browser.Name} with WinGet...");
        await LogAsync($"Starting install: {command}", NoEdgeLogLevel.Info, "BrowserInstall");

        var result = await _winGetService.InstallAsync(browser);

        var resultText = result.Success
            ? $"{browser.Name} installation completed."
            : $"{browser.Name} installation failed.";

        await LogAsync(
            $"{resultText} ExitCode={result.ExitCode}; Error={result.StandardError}",
            result.Success ? NoEdgeLogLevel.Success : NoEdgeLogLevel.Error,
            "BrowserInstall"
        );

        SetStatus(resultText);

        MessageBox.Show(
            BuildProcessResultMessage(resultText, result.StandardOutput, result.StandardError),
            "NoEdge Browser Installer",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error
        );
    }

    private void LoadCleanupTab()
    {
        if (!TryBeginTabLoad("Cleanup", out var tab))
        {
            return;
        }

        var panel = CreateVerticalPanel();
        tab.Controls.Add(panel);
        panel.Controls.Add(CreateTitle("Cleanup Profiles"));
        panel.Controls.Add(CreateText(
            "Cleanup Profiles are policy-based and reversible. They do not delete " +
            "browser profiles, passwords, bookmarks, extensions, browsing data, " +
            "WebView2, or browser updates."
        ));

        foreach (var profile in _profileCatalog.GetAll())
        {
            var group = new GroupBox
            {
                Text = profile.BrowserName,
                ForeColor = Color.White,
                Width = 900,
                Height = 145,
                Padding = new Padding(12),
                Margin = new Padding(0, 8, 0, 8)
            };

            var description = CreateText(profile.Description);
            description.Location = new Point(12, 25);
            description.Size = new Size(850, 42);
            group.Controls.Add(description);

            var preview = CreateButton(
                "Preview",
                Muted,
                (_, _) => ShowProfilePreview(profile.Id)
            );
            preview.Location = new Point(12, 82);

            var apply = CreateButton(
                "Apply",
                Primary,
                async (_, _) => await ApplyProfileAsync(profile.Id)
            );
            apply.Location = new Point(115, 82);

            var restore = CreateButton(
                "Restore",
                Muted,
                async (_, _) => await RestoreProfileAsync(profile.Id)
            );
            restore.Location = new Point(205, 82);

            group.Controls.Add(preview);
            group.Controls.Add(apply);
            group.Controls.Add(restore);
            panel.Controls.Add(group);
        }
    }

    private void ShowProfilePreview(string profileId)
    {
        var profile = _profileCatalog.GetById(profileId);

        if (profile is null)
        {
            return;
        }

        MessageBox.Show(
            $"{profile.Description}{Environment.NewLine}{Environment.NewLine}" +
            $"Registry path:{Environment.NewLine}{profile.RegistryPath}" +
            $"{Environment.NewLine}{Environment.NewLine}" +
            $"Settings:{Environment.NewLine}" +
            _policyService.BuildPreview(profile),
            $"{profile.BrowserName} Cleanup Profile",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    private async Task ApplyProfileAsync(string profileId)
    {
        var profile = _profileCatalog.GetById(profileId);

        if (profile is null)
        {
            return;
        }

        var preview = _policyService.BuildPreview(profile);

        if (MessageBox.Show(
                $"Apply {profile.BrowserName} Cleanup Profile?{Environment.NewLine}" +
                $"{Environment.NewLine}{preview}{Environment.NewLine}" +
                $"{Environment.NewLine}NoEdge will create a backup first.",
                "Confirm Cleanup Profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        SetStatus($"Applying {profile.BrowserName} Cleanup Profile...");
        var result = await Task.Run(() => _policyService.ApplyProfile(profile));

        SetStatus(result.Message);
        ShowOperationResult(result.Success, result.Message, result.BackupPath);
    }

    private async Task RestoreProfileAsync(string profileId)
    {
        var profile = _profileCatalog.GetById(profileId);

        if (profile is null)
        {
            return;
        }

        if (MessageBox.Show(
                $"Restore the saved {profile.BrowserName} Cleanup Profile settings?",
                "Confirm Cleanup Profile Restore",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        SetStatus($"Restoring {profile.BrowserName} Cleanup Profile...");
        var result = await Task.Run(() => _policyService.RestoreProfile(profile));

        SetStatus(result.Message);
        ShowOperationResult(result.Success, result.Message, result.BackupPath);
    }

    private async Task UninstallEdgeAsync()
    {
        if (_edgeInstallation is null)
        {
            await RefreshEdgeInventoryAsync();
        }

        if (_edgeInstallation is null ||
            string.IsNullOrWhiteSpace(_edgeInstallation.EdgeExecutablePath))
        {
            MessageBox.Show(
                "Microsoft Edge was not detected.",
                "NoEdge",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(_edgeInstallation.EdgeInstallerPath))
        {
            MessageBox.Show(
                "Edge was detected, but setup.exe was not found. " +
                "NoEdge will not delete Edge files directly.",
                "NoEdge",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        if (!_edgeUninstallService.IsAdministrator())
        {
            MessageBox.Show(
                "Restart NoEdge as Administrator to uninstall Edge. " +
                "NoEdge opens normally by default so safe tabs do not require elevation.",
                "Administrator rights required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        var command = _edgeUninstallService.BuildCommandPreview(
            _edgeInstallation.EdgeInstallerPath
        );

        if (MessageBox.Show(
                $"NoEdge will invoke Edge's detected uninstaller:{Environment.NewLine}" +
                $"{Environment.NewLine}{command}{Environment.NewLine}" +
                $"{Environment.NewLine}WebView2 is not targeted. Continue?",
                "Confirm Edge uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        var confirmation = PromptForText(
            "Final confirmation",
            "Type UNINSTALL EDGE exactly to continue:"
        );

        if (!string.Equals(
                confirmation,
                "UNINSTALL EDGE",
                StringComparison.Ordinal))
        {
            SetStatus("Edge uninstall cancelled.");
            return;
        }

        SetStatus("Running Edge uninstaller...");

        var result = await _edgeUninstallService.UninstallAsync(
            _edgeInstallation.EdgeInstallerPath
        );

        SetStatus(result.Message);

        MessageBox.Show(
            BuildProcessResultMessage(
                result.Message,
                result.StandardOutput,
                result.StandardError
            ),
            "NoEdge Edge Uninstall",
            MessageBoxButtons.OK,
            result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error
        );
    }

    private void LoadLogsTab()
    {
        if (!TryBeginTabLoad("Logs", out var tab))
        {
            RefreshLogView();
            return;
        }

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Background
        };
        tab.Controls.Add(panel);

        var openFolder = CreateButton(
            "Open Log Folder",
            Primary,
            (_, _) => _logService.OpenLogFolder()
        );
        openFolder.Dock = DockStyle.Top;
        panel.Controls.Add(openFolder);

        var refresh = CreateButton(
            "Refresh Logs",
            Muted,
            (_, _) => RefreshLogView()
        );
        refresh.Dock = DockStyle.Top;
        panel.Controls.Add(refresh);

        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(25, 25, 25),
            ForeColor = Color.Gainsboro,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9f)
        };
        panel.Controls.Add(_logBox);

        RefreshLogView();
    }

    private void RefreshLogView()
    {
        if (_logBox is null)
        {
            return;
        }

        var lines = _logService.ReadCurrentSession()
            .Select(entry =>
                $"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] " +
                $"{entry.EventName}: {entry.Message}");

        _logBox.Text =
            $"NoEdge log file: {_logService.SessionLogFilePath}" +
            Environment.NewLine +
            Environment.NewLine +
            string.Join(Environment.NewLine, lines);
    }

    private async Task LogAsync(
        string message,
        NoEdgeLogLevel level,
        string eventName)
    {
        await _logService.WriteAsync(message, level, eventName);

        if (_logBox is not null && !_logBox.IsDisposed)
        {
            RefreshLogView();
        }
    }

    private bool TryBeginTabLoad(string tabName, out TabPage tab)
    {
        tab = _tabs.TabPages[tabName];

        if (_loadedTabs.Contains(tabName))
        {
            return false;
        }

        _loadedTabs.Add(tabName);
        return true;
    }

    private static FlowLayoutPanel CreateVerticalPanel() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(18),
        BackColor = Background
    };

    private static Label CreateTitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 20f),
        ForeColor = Color.White,
        Margin = new Padding(0, 0, 0, 12)
    };

    private static Label CreateText(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(930, 0),
        Font = new Font("Segoe UI", 10f),
        ForeColor = Color.Gainsboro,
        Margin = new Padding(0, 0, 0, 8)
    };

    private static Button CreateButton(
        string text,
        Color color,
        EventHandler clickHandler)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(0, 4, 8, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            UseVisualStyleBackColor = false
        };

        button.FlatAppearance.BorderSize = 0;
        button.Click += clickHandler;
        return button;
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
    }

    private static string BuildProcessResultMessage(
        string heading,
        string standardOutput,
        string standardError)
    {
        var parts = new List<string> { heading };

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            parts.Add($"Output:{Environment.NewLine}{standardOutput.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            parts.Add($"Errors:{Environment.NewLine}{standardError.Trim()}");
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            parts
        );
    }

    private static string? PromptForText(string title, string prompt)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(440, 145),
            BackColor = Background,
            ForeColor = Color.White
        };

        var label = new Label
        {
            Text = prompt,
            AutoSize = true,
            Location = new Point(15, 15),
            ForeColor = Color.White
        };

        var input = new TextBox
        {
            Location = new Point(15, 45),
            Width = 410
        };

        var confirm = new Button
        {
            Text = "Confirm",
            DialogResult = DialogResult.OK,
            Location = new Point(258, 90),
            Width = 80
        };

        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(345, 90),
            Width = 80
        };

        dialog.Controls.Add(label);
        dialog.Controls.Add(input);
        dialog.Controls.Add(confirm);
        dialog.Controls.Add(cancel);
        dialog.AcceptButton = confirm;
        dialog.CancelButton = cancel;

        return dialog.ShowDialog() == DialogResult.OK
            ? input.Text
            : null;
    }
}
