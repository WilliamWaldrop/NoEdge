# NoEdge

A Windows utility for removing or disabling unwanted Microsoft browser front ends, installing an alternative browser, and applying optional browser cleanup profiles.

> [!WARNING]
> NoEdge makes system-level changes. It can disable Windows features, change browser settings, remove browser packages where Windows allows it, and create scheduled tasks for selected maintenance actions. Read every option before applying it and use the built-in backup and restore features.

## What NoEdge does

NoEdge is designed for users who want more control over Windows browser software without removing shared components that applications may rely on.

It provides three main areas:

- **Browser removal** — Disable or remove the standalone Microsoft Edge and Internet Explorer browser experiences where supported by Windows.
- **Browser installation** — Install a browser of your choice from trusted official sources or package managers.
- **Browser cleanup** — Apply transparent, opt-in cleanup profiles to selected browsers, including Chrome and Brave.

NoEdge does **not** attempt to remove File Explorer, Windows Explorer, WebView2, or shared rendering components by default.

## Features

### Remove Microsoft browsers

NoEdge can:

- Detect whether Microsoft Edge and Internet Explorer are installed.
- Disable Internet Explorer through supported Windows optional-feature servicing where available.
- Remove or disable Microsoft Edge only when the installed Windows version allows it.
- Remove selected shortcuts, startup entries, scheduled tasks, and browser prompts associated with the selected browser.
- Check whether a selected browser was restored after a Windows update.
- Reapply a chosen, supported configuration after user approval.
- Create a backup and provide a restore option before making changes.

### Install another browser

The **Browser Installer** tab lets users install a replacement browser before removing or disabling another one.

Planned supported browsers:

- Firefox
- LibreWolf
- Brave
- Google Chrome
- Chromium
- Vivaldi
- Opera
- Microsoft Edge

The installer should show:

- Browser name and publisher
- Install source
- Stable, beta, and developer-channel options where available
- Whether the browser is already installed
- A button to make it the default browser after installation

> [!IMPORTANT]
> NoEdge should never remove or disable a browser until the user has confirmed that another browser is installed or that they intentionally want no browser installed.

### Browser cleanup profiles

The **Browser Cleanup** tab offers optional, browser-specific profiles. These profiles should be reversible, clearly documented, and disabled by default.

#### Chrome cleanup profile

Possible options:

- Disable background startup behavior.
- Disable optional promotional notifications and desktop shortcuts.
- Remove unwanted startup entries.
- Clear temporary installer files and cache only when selected.
- Apply user-selected privacy, update, or feature policies where supported.
- Show every policy, task, registry value, and file path before it is changed.

#### Brave cleanup profile

Possible options:

- Disable background startup behavior.
- Remove unwanted shortcuts and startup entries.
- Clear temporary installer files and cache only when selected.
- Configure optional Brave features only after explicit user selection.
- Preserve Shields, sync data, passwords, bookmarks, extensions, and profiles unless the user explicitly chooses otherwise.

> [!CAUTION]
> “Debloating” must never silently remove bookmarks, passwords, profiles, extensions, browser updates, security features, or user data. Every cleanup action must be individually visible and reversible.

## Safety principles

NoEdge follows these rules:

1. **No raw system-file deletion by default.** It uses supported Windows servicing, uninstallation, policies, and settings before considering any advanced action.
2. **Protect shared components.** WebView2 and shared Windows web components remain untouched unless a future advanced option explicitly identifies dependencies and risks.
3. **Show the exact changes.** Users can review commands, registry values, scheduled tasks, policies, packages, and files before applying changes.
4. **Back up first.** NoEdge creates a restore point when supported and exports its own configuration backup.
5. **Make actions reversible.** Each feature should have a corresponding restore or undo action.
6. **Do not race Windows Update.** If an update restores a selected component, NoEdge should notify the user or reapply only the exact configuration the user previously approved.
7. **Keep maintenance opt-in.** Automatic checks and scheduled tasks must be disabled by default and removable from inside the app.

## Tabs

| Tab | Purpose |
|---|---|
| Dashboard | Shows installed browsers, Windows version, selected default browser, and the status of Edge/Internet Explorer. |
| Remove Browsers | Disables or removes supported Microsoft browser front ends and related optional items. |
| Install Browser | Installs a chosen alternative browser from an approved source. |
| Browser Cleanup | Applies opt-in cleanup profiles for Chrome, Brave, and other supported browsers. |
| Maintenance | Manages optional update checks, reapplication rules, logs, backups, and scheduled tasks. |
| Restore | Reverses changes made by NoEdge where possible. |
| Logs | Displays commands run, changes made, errors, and restoration history. |

## What NoEdge will not do

NoEdge will not:

- Remove File Explorer or the Windows desktop shell.
- Delete Windows system files blindly.
- Remove WebView2 or shared web-rendering components by default.
- Disable browser security updates without a clear, separate opt-in setting.
- Collect browsing history, passwords, bookmarks, or personal browser data.
- Send telemetry without explicit consent.
- Install a browser without showing the source and asking for confirmation.

## Example workflow

1. Open **Install Browser**.
2. Select Firefox, Brave, or another supported browser.
3. Install it from the displayed source.
4. Optionally set it as the default browser.
5. Open **Remove Browsers**.
6. Review the detected Microsoft Edge and Internet Explorer components.
7. Select only the browser front end you want to disable or remove.
8. Review the change plan and create a backup.
9. Apply changes and restart if Windows requires it.
10. Use **Maintenance** only if you want NoEdge to check for components restored by future updates.

## Project status

NoEdge is under active development.

Current goals:

- [ ] Detect Microsoft Edge and Internet Explorer safely
- [ ] Disable Internet Explorer through Windows optional features
- [ ] Install alternative browsers
- [ ] Add Chrome cleanup profile
- [ ] Add Brave cleanup profile
- [ ] Create backup and restore support
- [ ] Add full change previews and logs
- [ ] Add optional scheduled maintenance
- [ ] Add automated tests for every removal and restore action

## Development principles

Contributions are welcome, especially for:

- Windows-version detection
- Safe browser detection
- Package-manager integrations
- Browser cleanup profiles
- Restore and rollback testing
- Accessibility
- Localization
- Documentation

Before submitting a pull request:

1. Do not add destructive file deletion without a documented recovery path.
2. Keep browser cleanup profiles opt-in and reversible.
3. Add or update tests for behavior changes.
4. Document every registry value, policy, task, service, package, and file modified.
5. Never commit certificates, API keys, tokens, or local configuration files.

## Disclaimer

NoEdge is an independent project and is not affiliated with, endorsed by, or supported by Microsoft, Google, Brave Software, Mozilla, or any other browser vendor.

Use this software at your own risk. Removing or disabling Windows components may affect Windows features, applications, updates, repair tools, or enterprise-managed devices. Always create a backup and review the planned changes before applying them.

## License

Licensed under MIT license see (LICENSE) for more details :)
