<#
.SYNOPSIS
    NoEdge - a WinForms PowerShell browser-control utility.
.DESCRIPTION
    A GUI-first, lazy-loading browser utility. It inventories Edge only when
    its tab is opened, installs alternate browsers through WinGet, and applies
    reversible browser cleanup profiles. It never directly deletes protected
    Windows files or targets WebView2.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    $exe = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh.exe' } else { 'powershell.exe' }
    Start-Process -FilePath $exe -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-STA','-File',("`"{0}`"" -f $PSCommandPath))
    return
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.VisualBasic
[Windows.Forms.Application]::EnableVisualStyles()

$script:AppName = 'NoEdge'
$script:Version = '0.4.0-dev'
$script:DataRoot = Join-Path $env:LOCALAPPDATA 'NoEdge'
$script:LogRoot = Join-Path $script:DataRoot 'Logs'
$script:BackupRoot = Join-Path $script:DataRoot 'Backups'
$script:LogFile = $null
$script:LoadedTabs = @{}
$script:LogBox = $null
$script:Status = $null

function Initialize-Storage {
    foreach ($path in @($script:DataRoot,$script:LogRoot,$script:BackupRoot)) {
        if (-not (Test-Path -LiteralPath $path)) { New-Item -ItemType Directory -Path $path -Force | Out-Null }
    }
    $script:LogFile = Join-Path $script:LogRoot ("NoEdge_{0}.jsonl" -f (Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'))
}
function Write-Log {
    param([string]$Message,[ValidateSet('Info','Success','Warning','Error')][string]$Level='Info',[string]$Event='General')
    $line = [pscustomobject]@{Timestamp=(Get-Date).ToString('o');Level=$Level;Event=$Event;Message=$Message} | ConvertTo-Json -Compress
    $line | Add-Content -LiteralPath $script:LogFile -Encoding UTF8
    if ($script:LogBox) { $script:LogBox.AppendText("[{0:HH:mm:ss}] [{1}] {2}`r`n" -f (Get-Date),$Level,$Message) }
}
function Set-Status { param([string]$Text) if ($script:Status) { $script:Status.Text=$Text }; Write-Log $Text 'Info' 'Status' }
function Test-Admin {
    $principal=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
function Get-PF86 { $path=[Environment]::GetFolderPath('ProgramFilesX86'); if($path){$path}else{$env:ProgramFiles} }
function Get-FirstExistingPath { param([string[]]$Paths) foreach($path in $Paths){if($path -and (Test-Path -LiteralPath $path)){return $path}}; $null }
function New-Panel { $p=[Windows.Forms.FlowLayoutPanel]::new();$p.Dock='Fill';$p.FlowDirection='TopDown';$p.WrapContents=$false;$p.AutoScroll=$true;$p.Padding=[Windows.Forms.Padding]::new(18);$p.BackColor=[Drawing.Color]::FromArgb(32,32,32);$p }
function New-Title { param([string]$Text) $x=[Windows.Forms.Label]::new();$x.Text=$Text;$x.AutoSize=$true;$x.Font=[Drawing.Font]::new('Segoe UI Semibold',18);$x.ForeColor=[Drawing.Color]::White;$x.Margin=[Windows.Forms.Padding]::new(0,0,0,12);$x }
function New-Text { param([string]$Text) $x=[Windows.Forms.Label]::new();$x.Text=$Text;$x.AutoSize=$true;$x.MaximumSize=[Drawing.Size]::new(920,0);$x.ForeColor=[Drawing.Color]::Gainsboro;$x.Font=[Drawing.Font]::new('Segoe UI',10);$x.Margin=[Windows.Forms.Padding]::new(0,0,0,8);$x }
function New-ActionButton { param([string]$Text,[scriptblock]$Action,[Drawing.Color]$Color=[Drawing.Color]::FromArgb(0,120,212)) $b=[Windows.Forms.Button]::new();$b.Text=$Text;$b.AutoSize=$true;$b.Padding=[Windows.Forms.Padding]::new(10,6,10,6);$b.Margin=[Windows.Forms.Padding]::new(0,4,8,4);$b.FlatStyle='Flat';$b.BackColor=$Color;$b.ForeColor=[Drawing.Color]::White;$b.FlatAppearance.BorderSize=0;$b.Add_Click($Action);$b }
function New-Tab { param([string]$Name,[string]$Text) $tab=[Windows.Forms.TabPage]::new($Text);$tab.Name=$Name;$tab.BackColor=[Drawing.Color]::FromArgb(32,32,32);$tab.ForeColor=[Drawing.Color]::White;$tab }

function Get-EdgeInfo {
    $pf86=Get-PF86
    $roots=@((Join-Path $env:ProgramFiles 'Microsoft\Edge\Application'),(Join-Path $pf86 'Microsoft\Edge\Application'))
    $exe=Get-FirstExistingPath ($roots|ForEach-Object {Join-Path $_ 'msedge.exe'})
    $root=if($exe){Split-Path -Parent $exe}else{Get-FirstExistingPath $roots}
    $setup=$null
    if($root){$setup=Get-ChildItem -LiteralPath $root -Filter setup.exe -File -Recurse -ErrorAction SilentlyContinue|Where-Object{$_.FullName -match '\\Installer\\setup\.exe$'}|Sort-Object LastWriteTime -Descending|Select-Object -First 1}
    $version=$null;if($exe){try{$version=(Get-Item -LiteralPath $exe).VersionInfo.ProductVersion}catch{}}
    $webview=Get-FirstExistingPath @((Join-Path $env:ProgramFiles 'Microsoft\EdgeWebView\Application'),(Join-Path $pf86 'Microsoft\EdgeWebView\Application'))
    [pscustomobject]@{Exe=$exe;Root=$root;Setup=if($setup){$setup.FullName}else{$null};Version=$version;WebView2=$webview}
}
function Get-EdgeInventory { param([string]$Root) if(-not $Root -or -not(Test-Path -LiteralPath $Root)){return @()} Get-ChildItem -LiteralPath $Root -Force -Recurse -ErrorAction SilentlyContinue|ForEach-Object{[pscustomobject]@{Type=if($_.PSIsContainer){'Folder'}else{'File'};Path=$_.FullName;Size=if($_.PSIsContainer){''}else{('{0:N2} MB' -f ($_.Length/1MB))}}} }
function Get-Browsers { @(
    [pscustomobject]@{Name='Firefox';Id='Mozilla.Firefox';Description='Firefox stable'},
    [pscustomobject]@{Name='LibreWolf';Id='LibreWolf.LibreWolf';Description='Privacy-focused Firefox-based browser'},
    [pscustomobject]@{Name='Brave';Id='Brave.Brave';Description='Brave stable'},
    [pscustomobject]@{Name='Google Chrome';Id='Google.Chrome';Description='Google Chrome stable'},
    [pscustomobject]@{Name='Chromium';Id='Hibbiki.Chromium';Description='Open-source Chromium browser'},
    [pscustomobject]@{Name='Vivaldi';Id='Vivaldi.Vivaldi';Description='Vivaldi stable'},
    [pscustomobject]@{Name='Opera';Id='Opera.Opera';Description='Opera stable'}
) }
function Install-Browser { param($Browser)
    if(-not(Get-Command winget.exe -ErrorAction SilentlyContinue)){[Windows.Forms.MessageBox]::Show('WinGet was not found. Install or repair App Installer, then try again.','NoEdge','OK','Warning');return}
    if([Windows.Forms.MessageBox]::Show("Install $($Browser.Name) using WinGet package '$($Browser.Id)'?",'Confirm browser installation','YesNo','Question') -ne [Windows.Forms.DialogResult]::Yes){return}
    try {Set-Status "Installing $($Browser.Name)...";& winget.exe install --id $Browser.Id --exact --source winget --accept-package-agreements --accept-source-agreements;if($LASTEXITCODE -ne 0){throw "WinGet returned exit code $LASTEXITCODE."};Set-Status "$($Browser.Name) installation completed."}catch{Write-Log $_.Exception.Message 'Error' 'Install';[Windows.Forms.MessageBox]::Show($_.Exception.Message,'NoEdge install error','OK','Error')}
}

function Get-EdgeProfile { @([pscustomobject]@{Name='BackgroundModeEnabled';Value=0},[pscustomobject]@{Name='StartupBoostEnabled';Value=0},[pscustomobject]@{Name='HubsSidebarEnabled';Value=0},[pscustomobject]@{Name='ShowRecommendationsEnabled';Value=0}) }
function Get-ChromeProfile { @([pscustomobject]@{Name='BackgroundModeEnabled';Value=0},[pscustomobject]@{Name='PromotionalTabsEnabled';Value=0}) }
function Save-PolicyBackup { param([string]$Name,[string]$Path,[object[]]$Policies) $items=foreach($policy in $Policies){$exists=$false;$value=$null;try{$value=(Get-ItemProperty -LiteralPath $Path -Name $policy.Name -ErrorAction Stop).$($policy.Name);$exists=$true}catch{};[pscustomobject]@{Name=$policy.Name;Exists=$exists;Value=$value}};[pscustomobject]@{Path=$Path;Items=@($items)}|Export-Clixml -LiteralPath (Join-Path $script:BackupRoot "$Name.clixml") }
function Apply-Profile { param([string]$Name,[string]$Path,[object[]]$Policies)
    if(-not(Test-Admin)){[Windows.Forms.MessageBox]::Show('Run NoEdge as Administrator to apply cleanup profiles.','Administrator required','OK','Warning');return}
    $changes=($Policies|ForEach-Object{"$($_.Name) = $($_.Value)"}) -join "`r`n"
    if([Windows.Forms.MessageBox]::Show("Apply $Name Cleanup Profile?`r`n`r`n$changes`r`n`r`nA backup is created first.",'Confirm cleanup','YesNo','Warning') -ne [Windows.Forms.DialogResult]::Yes){return}
    try{Save-PolicyBackup $Name $Path $Policies;if(-not(Test-Path -LiteralPath $Path)){New-Item -Path $Path -Force|Out-Null};foreach($policy in $Policies){New-ItemProperty -Path $Path -Name $policy.Name -Value $policy.Value -PropertyType DWord -Force|Out-Null};Set-Status "$Name Cleanup Profile applied. Restart the browser."}catch{Write-Log $_.Exception.Message 'Error' 'Cleanup';[Windows.Forms.MessageBox]::Show($_.Exception.Message,'NoEdge cleanup error','OK','Error')}
}
function Restore-Profile { param([string]$Name)
    $backup=Join-Path $script:BackupRoot "$Name.clixml"
    if(-not(Test-Path -LiteralPath $backup)){[Windows.Forms.MessageBox]::Show("No $Name backup was found.",'NoEdge','OK','Information');return}
    if(-not(Test-Admin)){[Windows.Forms.MessageBox]::Show('Run NoEdge as Administrator to restore cleanup profiles.','Administrator required','OK','Warning');return}
    if([Windows.Forms.MessageBox]::Show("Restore the saved $Name settings?",'Confirm restore','YesNo','Question') -ne [Windows.Forms.DialogResult]::Yes){return}
    try{$data=Import-Clixml -LiteralPath $backup;if(-not(Test-Path $data.Path)){New-Item -Path $data.Path -Force|Out-Null};foreach($item in $data.Items){if($item.Exists){New-ItemProperty -Path $data.Path -Name $item.Name -Value $item.Value -PropertyType DWord -Force|Out-Null}else{Remove-ItemProperty -Path $data.Path -Name $item.Name -ErrorAction SilentlyContinue}};Set-Status "$Name Cleanup Profile restored."}catch{Write-Log $_.Exception.Message 'Error' 'Restore';[Windows.Forms.MessageBox]::Show($_.Exception.Message,'NoEdge restore error','OK','Error')}
}
function Uninstall-Edge {
    if(-not(Test-Admin)){[Windows.Forms.MessageBox]::Show('Run NoEdge as Administrator to uninstall Edge.','Administrator required','OK','Warning');return}
    $edge=Get-EdgeInfo
    if(-not $edge.Exe){[Windows.Forms.MessageBox]::Show('Microsoft Edge was not detected.','NoEdge','OK','Information');return}
    if(-not $edge.Setup){[Windows.Forms.MessageBox]::Show('Edge was detected, but its setup.exe uninstaller was not found. NoEdge will not delete files directly.','NoEdge','OK','Warning');return}
    $msg="NoEdge will invoke Edge's detected uninstaller:`r`n`r`n$($edge.Setup)`r`n`r`nWebView2 is excluded. Some Windows features and applications can still depend on Edge components. Continue?"
    if([Windows.Forms.MessageBox]::Show($msg,'Confirm Edge uninstall','YesNo','Warning') -ne [Windows.Forms.DialogResult]::Yes){return}
    $phrase=[Microsoft.VisualBasic.Interaction]::InputBox('Type UNINSTALL EDGE exactly to continue.','Final confirmation','')
    if($phrase -cne 'UNINSTALL EDGE'){Set-Status 'Edge uninstall cancelled.';return}
    try{Set-Status 'Running Edge uninstaller...';$p=Start-Process -FilePath $edge.Setup -ArgumentList @('--uninstall','--system-level','--verbose-logging','--force-uninstall') -Wait -PassThru;if($p.ExitCode -ne 0){throw "Edge setup.exe returned exit code $($p.ExitCode)."};Set-Status 'Edge uninstaller completed. Use Refresh Edge Inventory to verify.'}catch{Write-Log $_.Exception.Message 'Error' 'EdgeUninstall';[Windows.Forms.MessageBox]::Show($_.Exception.Message,'NoEdge uninstall error','OK','Error')}
}

function Initialize-Dashboard {
    param($Tab)

    if ($script:LoadedTabs[$Tab.Name]) {
        return
    }

    $script:LoadedTabs[$Tab.Name] = $true

    $panel = New-Panel
    $Tab.Controls.Add($panel)

    $panel.Controls.Add((New-Title 'NoEdge'))

    $summary = New-Text 'Loading system status...'
    $panel.Controls.Add($summary)

    $refresh = New-ActionButton 'Refresh Dashboard' {
        $edge = Get-EdgeInfo

        $summary.Text = @"
Windows: $([Environment]::OSVersion.VersionString)
Administrator: $(Test-Admin)
WinGet available: $([bool](Get-Command winget.exe -ErrorAction SilentlyContinue))
Edge detected: $([bool]$edge.Exe)
WebView2 detected: $([bool]$edge.WebView2)
"@
    }

    $panel.Controls.Add($refresh)

    $panel.Controls.Add(
        (
            New-Text `
                'NoEdge intentionally does not scan the Edge application ' +
                'directory until you open the Edge tab.'
        )
    )

    $refresh.PerformClick() | Out-Null
}

function Load-EdgeInventory { param($Picture,$Info,$Grid) $Info.Text='Lazy-loading Edge inventory...';[Windows.Forms.Application]::DoEvents();$edge=Get-EdgeInfo;if($edge.Exe){try{$icon=[Drawing.Icon]::ExtractAssociatedIcon($edge.Exe);$Picture.Image=$icon.ToBitmap()}catch{}};$Info.Text="Microsoft Edge`r`nVersion: $($edge.Version)`r`nBrowser path: $($edge.Exe)`r`nUninstaller: $($edge.Setup)`r`nWebView2 (protected): $($edge.WebView2)";$Grid.DataSource=@(Get-EdgeInventory $edge.Root);Write-Log "Lazy-loaded Edge inventory: $($edge.Root)" 'Info' 'EdgeInventory' }
function Initialize-Edge { param($Tab) if($script:LoadedTabs[$Tab.Name]){return};$script:LoadedTabs[$Tab.Name]=$true;$panel=New-Panel;$Tab.Controls.Add($panel);$panel.Controls.Add((New-Title 'Microsoft Edge'))
    $top=[Windows.Forms.FlowLayoutPanel]::new();$top.AutoSize=$true;$top.FlowDirection='LeftToRight';$pic=[Windows.Forms.PictureBox]::new();$pic.Size=[Drawing.Size]::new(64,64);$pic.SizeMode='CenterImage';$top.Controls.Add($pic);$info=New-Text 'Loading Edge inventory...';$info.Font=[Drawing.Font]::new('Segoe UI',10);$top.Controls.Add($info);$panel.Controls.Add($top)
    $panel.Controls.Add((New-Text 'This inventory shows files under the detected Edge application directory. The Windows/Edge uninstaller determines what it removes. NoEdge does not use direct file deletion, and it never targets WebView2.'))
    $grid=[Windows.Forms.DataGridView]::new();$grid.Width=950;$grid.Height=280;$grid.ReadOnly=$true;$grid.AllowUserToAddRows=$false;$grid.AllowUserToDeleteRows=$false;$grid.AutoSizeColumnsMode='Fill';$grid.BackgroundColor=[Drawing.Color]::FromArgb(45,45,48);$panel.Controls.Add($grid)
    $actions=[Windows.Forms.FlowLayoutPanel]::new();$actions.AutoSize=$true;$actions.FlowDirection='LeftToRight'
    $actions.Controls.Add((New-ActionButton 'Refresh Edge Inventory' {Load-EdgeInventory $pic $info $grid}))
    $actions.Controls.Add((New-ActionButton 'Preview Cleanup Profile' {$p=(Get-EdgeProfile|ForEach-Object{"$($_.Name) = $($_.Value)"}) -join "`r`n";[Windows.Forms.MessageBox]::Show("Reversible Edge Cleanup Profile:`r`n`r`n$p`r`n`r`nProfiles, passwords, bookmarks, extensions, updates, and WebView2 are not removed.",'NoEdge','OK','Information')}))
    $actions.Controls.Add((New-ActionButton 'Apply Edge Cleanup' {Apply-Profile 'Edge' 'HKLM:\SOFTWARE\Policies\Microsoft\Edge' (Get-EdgeProfile)} [Drawing.Color]::FromArgb(0,120,212)))
    $actions.Controls.Add((New-ActionButton 'Restore Edge Cleanup' {Restore-Profile 'Edge'} [Drawing.Color]::FromArgb(90,90,90)))
    $actions.Controls.Add((New-ActionButton 'Uninstall Edge' {Uninstall-Edge} [Drawing.Color]::FromArgb(180,45,45)))
    $panel.Controls.Add($actions);Load-EdgeInventory $pic $info $grid }
function Initialize-Install { param($Tab) if($script:LoadedTabs[$Tab.Name]){return};$script:LoadedTabs[$Tab.Name]=$true;$panel=New-Panel;$Tab.Controls.Add($panel);$panel.Controls.Add((New-Title 'Install a Browser'));$panel.Controls.Add((New-Text 'Install a replacement browser before uninstalling Edge. Each action shows the exact WinGet package ID and asks for confirmation.'));foreach($browser in Get-Browsers){$button=New-ActionButton "Install $($browser.Name) — $($browser.Id)" {param($sender,$eventArgs) Install-Browser $sender.Tag};$button.Tag=$browser;$panel.Controls.Add($button)} }
function Initialize-Cleanup {
    param($Tab)

    if ($script:LoadedTabs[$Tab.Name]) {
        return
    }

    $script:LoadedTabs[$Tab.Name] = $true

    $panel = New-Panel
    $Tab.Controls.Add($panel)

    $panel.Controls.Add((New-Title 'Cleanup Profiles'))

    $panel.Controls.Add(
        (
            New-Text `
                'Profiles are policy-based and reversible. They do not delete ' +
                'browser profiles, browsing data, passwords, bookmarks, ' +
                'extensions, or update mechanisms.'
        )
    )

    $profiles = @(
        [pscustomobject]@{
            Name     = 'Chrome'
            Path     = 'HKLM:\SOFTWARE\Policies\Google\Chrome'
            Policies = Get-ChromeProfile
        },

        [pscustomobject]@{
            Name     = 'Brave'
            Path     = 'HKLM:\SOFTWARE\Policies\BraveSoftware\Brave'
            Policies = Get-ChromeProfile
        }
    )

    foreach ($profile in $profiles) {
        $applyButton = New-ActionButton `
            -Text "Apply $($profile.Name) Cleanup" `
            -Action {
                param($sender, $eventArgs)

                $selectedProfile = $sender.Tag

                Apply-Profile `
                    -Name $selectedProfile.Name `
                    -Path $selectedProfile.Path `
                    -Policies $selectedProfile.Policies
            }

        $applyButton.Tag = $profile
        $panel.Controls.Add($applyButton)

        $restoreButton = New-ActionButton `
            -Text "Restore $($profile.Name) Cleanup" `
            -Action {
                param($sender, $eventArgs)

                Restore-Profile -Name $sender.Tag
            } `
            -Color ([Drawing.Color]::FromArgb(90, 90, 90))

        $restoreButton.Tag = $profile.Name
        $panel.Controls.Add($restoreButton)
    }
}

foreach($item in @([pscustomobject]@{Name='Chrome';Path='HKLM:\SOFTWARE\Policies\Google\Chrome';Policies=(Get-ChromeProfile)},[pscustomobject]@{Name='Brave';Path='HKLM:\SOFTWARE\Policies\BraveSoftware\Brave';Policies=(Get-ChromeProfile)})){$apply=New-ActionButton "Apply $($item.Name) Cleanup" {param($sender,$eventArgs)$x=$sender.Tag;Apply-Profile $x.Name $x.Path $x.Policies};$apply.Tag=$item;$panel.Controls.Add($apply);$panel.Controls.Add((New-ActionButton "Restore $($item.Name) Cleanup" {param($sender,$eventArgs)Restore-Profile $sender.Tag} [Drawing.Color]::FromArgb(90,90,90))).Tag=$item} }
function Initialize-Logs { param($Tab) if($script:LoadedTabs[$Tab.Name]){return};$script:LoadedTabs[$Tab.Name]=$true;$panel=[Windows.Forms.Panel]::new();$panel.Dock='Fill';$panel.BackColor=[Drawing.Color]::FromArgb(32,32,32);$Tab.Controls.Add($panel);$open=New-ActionButton 'Open Log Folder' {Start-Process explorer.exe $script:LogRoot};$open.Dock='Top';$panel.Controls.Add($open);$script:LogBox=[Windows.Forms.TextBox]::new();$script:LogBox.Multiline=$true;$script:LogBox.ReadOnly=$true;$script:LogBox.ScrollBars='Vertical';$script:LogBox.Dock='Fill';$script:LogBox.BackColor=[Drawing.Color]::FromArgb(25,25,25);$script:LogBox.ForeColor=[Drawing.Color]::Gainsboro;$script:LogBox.Font=[Drawing.Font]::new('Consolas',9);$script:LogBox.AppendText("NoEdge log file: $script:LogFile`r`n");$panel.Controls.Add($script:LogBox) }

Initialize-Storage
$form=[Windows.Forms.Form]::new();$form.Text="NoEdge $script:Version";$form.StartPosition='CenterScreen';$form.Size=[Drawing.Size]::new(1080,740);$form.MinimumSize=[Drawing.Size]::new(900,600);$form.BackColor=[Drawing.Color]::FromArgb(32,32,32)
$tabs=[Windows.Forms.TabControl]::new();$tabs.Dock='Fill';$tabs.Font=[Drawing.Font]::new('Segoe UI',10)
$dashboard=New-Tab 'Dashboard' 'Dashboard';$edgeTab=New-Tab 'Edge' 'Edge';$installTab=New-Tab 'Install' 'Install Browser';$cleanupTab=New-Tab 'Cleanup' 'Cleanup Profiles';$logsTab=New-Tab 'Logs' 'Logs';[void]$tabs.TabPages.AddRange(@($dashboard,$edgeTab,$installTab,$cleanupTab,$logsTab))
$bar=[Windows.Forms.StatusStrip]::new();$script:Status=[Windows.Forms.ToolStripStatusLabel]::new('Ready. Edge is not scanned until its tab is opened.');[void]$bar.Items.Add($script:Status);$form.Controls.Add($tabs);$form.Controls.Add($bar)
$tabs.Add_SelectedIndexChanged({switch($tabs.SelectedTab.Name){'Dashboard'{Initialize-Dashboard $tabs.SelectedTab};'Edge'{Initialize-Edge $tabs.SelectedTab};'Install'{Initialize-Install $tabs.SelectedTab};'Cleanup'{Initialize-Cleanup $tabs.SelectedTab};'Logs'{Initialize-Logs $tabs.SelectedTab}}})
Initialize-Dashboard $dashboard
Write-Log 'NoEdge GUI started.' 'Info' 'Startup'
[void]$form.ShowDialog()
