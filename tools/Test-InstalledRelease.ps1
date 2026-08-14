[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$')][string]$Version,
    [string]$SetupPath,
    [string]$PreviousSetupPath,
    [ValidateRange(30, 3600)][int]$ProcessTimeoutSeconds = 900,
    [ValidateRange(3, 60)][int]$LaunchObservationSeconds = 8,
    [switch]$Execute
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$defaultSetupPath = Join-Path $repositoryRoot "artifacts\$Version\HanabePhotoManager-Setup-x64.exe"
$resolvedSetupPath = [IO.Path]::GetFullPath($(if ([string]::IsNullOrWhiteSpace($SetupPath)) { $defaultSetupPath } else { $SetupPath }))
$resolvedPreviousSetupPath = if ([string]::IsNullOrWhiteSpace($PreviousSetupPath)) {
    $null
}
else {
    [IO.Path]::GetFullPath($PreviousSetupPath)
}
$expectedInstalledExecutable = Join-Path $env:ProgramFiles "Hanabe Photo Manager\HanabePhotoManager.App.exe"
$desktopDirectory = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonDesktopDirectory)
$desktopShortcutPath = Join-Path $desktopDirectory "Hanabe Photo Manager.lnk"
$userDataRoot = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) "HanabePhotoManager"
$probeDirectory = Join-Path $userDataRoot ".installer-verification"
$probePath = Join-Path $probeDirectory "uninstall-boundary-$Version.txt"

function Assert-RepositorySetupPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $repositoryPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ([IO.Path]::GetPathRoot($Path) -ne "D:\") {
        throw "Setup verification only accepts artifacts on the D drive: $Path"
    }
    if (-not $Path.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Setup verification only accepts artifacts inside the repository: $Path"
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Setup artifact does not exist: $Path"
    }
}

function Invoke-CheckedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $quotedArguments = $Arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = $quotedArguments -join " "
    $startInfo.UseShellExecute = $false
    $process = [Diagnostics.Process]::Start($startInfo)
    if (-not $process.WaitForExit($ProcessTimeoutSeconds * 1000)) {
        $process.Kill()
        throw "Process exceeded the $ProcessTimeoutSeconds second timeout: $FilePath"
    }
    if ($process.ExitCode -notin @(0, 1641, 3010)) {
        throw "Process failed with exit code $($process.ExitCode): $FilePath"
    }

    return $process.ExitCode
}

function Get-DesktopShortcutTarget {
    if (-not (Test-Path -LiteralPath $desktopShortcutPath -PathType Leaf)) {
        throw "Desktop shortcut was not created: $desktopShortcutPath"
    }

    return [IO.Path]::GetFullPath($desktopShortcutPath)
}

function Assert-InstalledEntryPoints {
    if (-not (Test-Path -LiteralPath $expectedInstalledExecutable -PathType Leaf)) {
        throw "Installed executable was not found at the required path: $expectedInstalledExecutable"
    }

    return Get-DesktopShortcutTarget
}

function Test-InstalledApplicationLaunch {
    $existingIds = @(Get-Process -Name "HanabePhotoManager.App" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    Start-Process -FilePath $desktopShortcutPath
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    $process = $null
    do {
        Start-Sleep -Milliseconds 250
        $process = Get-Process -Name "HanabePhotoManager.App" -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Id -notin $existingIds -and
                $_.Path -and
                $_.Path.Equals($expectedInstalledExecutable, [StringComparison]::OrdinalIgnoreCase)
            } |
            Select-Object -First 1
    } while ($null -eq $process -and [DateTime]::UtcNow -lt $deadline)

    if ($null -eq $process) {
        throw "Desktop shortcut did not launch the expected Program Files executable: $expectedInstalledExecutable"
    }
    if (-not $process.Path.Equals($expectedInstalledExecutable, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Desktop shortcut launched an unexpected executable: $($process.Path)"
    }
    if ($process.WaitForExit($LaunchObservationSeconds * 1000)) {
        throw "Installed application exited during the $LaunchObservationSeconds second startup observation window."
    }

    $null = $process.CloseMainWindow()
    if (-not $process.WaitForExit(5000)) {
        $process.Kill()
        $process.WaitForExit()
    }
}

Assert-RepositorySetupPath $resolvedSetupPath
if ($null -ne $resolvedPreviousSetupPath) {
    Assert-RepositorySetupPath $resolvedPreviousSetupPath
}

$plan = [ordered]@{
    mode = if ($Execute) { "execute" } else { "dry-run" }
    version = $Version
    currentSetup = $resolvedSetupPath
    previousSetup = $resolvedPreviousSetupPath
    expectedInstalledExecutable = $expectedInstalledExecutable
    desktopShortcut = $desktopShortcutPath
    protectedUserDataRoot = $userDataRoot
    leavesCurrentVersionInstalled = $true
}

if (-not $Execute) {
    $plan | ConvertTo-Json -Depth 3
    return
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this verification from an elevated PowerShell session. Dry-run mode does not require elevation."
}

$evidenceRoot = Join-Path $repositoryRoot ".artifacts\installed-release\$Version\$([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'))"
New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null
$probeContent = "Hanabe installer uninstall boundary probe $([Guid]::NewGuid())"
$verificationCompleted = $false

try {
    if ($null -ne $resolvedPreviousSetupPath) {
        Invoke-CheckedProcess $resolvedPreviousSetupPath @(
            "/install", "/quiet", "/norestart", "/log", (Join-Path $evidenceRoot "install-previous.log")
        ) | Out-Null
    }

    Invoke-CheckedProcess $resolvedSetupPath @(
        "/install", "/quiet", "/norestart", "/log", (Join-Path $evidenceRoot "install-current.log")
    ) | Out-Null
    $shortcutTarget = Assert-InstalledEntryPoints
    Test-InstalledApplicationLaunch

    New-Item -ItemType Directory -Path $probeDirectory -Force | Out-Null
    Set-Content -LiteralPath $probePath -Value $probeContent -Encoding UTF8

    Invoke-CheckedProcess $resolvedSetupPath @(
        "/uninstall", "/quiet", "/norestart", "/log", (Join-Path $evidenceRoot "uninstall-current.log")
    ) | Out-Null
    if (-not (Test-Path -LiteralPath $probePath -PathType Leaf)) {
        throw "Uninstall removed the application-data probe: $probePath"
    }
    $probeContentAfterUninstall = (Get-Content -LiteralPath $probePath -Raw).Trim()
    if ($probeContentAfterUninstall -ne $probeContent) {
        throw "Uninstall changed the application-data probe: $probePath"
    }

    Invoke-CheckedProcess $resolvedSetupPath @(
        "/install", "/quiet", "/norestart", "/log", (Join-Path $evidenceRoot "reinstall-current.log")
    ) | Out-Null
    $shortcutTargetAfterReinstall = Assert-InstalledEntryPoints

    $evidence = [ordered]@{
        verifiedAtUtc = [DateTime]::UtcNow.ToString("o")
        version = $Version
        setupPath = $resolvedSetupPath
        setupSha256 = (Get-FileHash -LiteralPath $resolvedSetupPath -Algorithm SHA256).Hash.ToLowerInvariant()
        installedExecutable = [IO.Path]::GetFullPath($expectedInstalledExecutable)
        desktopShortcut = [IO.Path]::GetFullPath($desktopShortcutPath)
        shortcutTarget = $shortcutTarget
        shortcutTargetAfterReinstall = $shortcutTargetAfterReinstall
        userDataProbeSurvivedUninstall = $true
        currentVersionLeftInstalled = $true
    }
    $evidence | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $evidenceRoot "verification.json") -Encoding UTF8
    $verificationCompleted = $true
}
finally {
    if ($verificationCompleted -and (Test-Path -LiteralPath $probePath -PathType Leaf)) {
        Remove-Item -LiteralPath $probePath -Force
        if ((Test-Path -LiteralPath $probeDirectory -PathType Container) -and (Get-ChildItem -LiteralPath $probeDirectory -Force).Count -eq 0) {
            Remove-Item -LiteralPath $probeDirectory -Force
        }
    }
}

Write-Host "Installed release verification passed. Evidence: $evidenceRoot"
