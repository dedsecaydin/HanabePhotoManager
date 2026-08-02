[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$packageProjectPath = Join-Path $repositoryRoot "installer\HanabePhotoManager.Installer\HanabePhotoManager.Installer.wixproj"
$packageSourcePath = Join-Path $repositoryRoot "installer\HanabePhotoManager.Installer\Package.wxs"
$packageLocalizationPath = Join-Path $repositoryRoot "installer\HanabePhotoManager.Installer\Package.zh-CN.wxl"
$failures = [Collections.Generic.List[string]]::new()

function Read-RequiredSource {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        $failures.Add("Required installer source is missing: $Path")
        return ""
    }

    return Get-Content -LiteralPath $Path -Raw
}

function Assert-Matches {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][AllowEmptyString()][string]$Actual,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($Actual -notmatch $Pattern) {
        $failures.Add($Message)
    }
}

function Assert-DoesNotMatch {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][AllowEmptyString()][string]$Actual,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($Actual -match $Pattern) {
        $failures.Add($Message)
    }
}

$packageProject = Read-RequiredSource $packageProjectPath
$packageSource = Read-RequiredSource $packageSourcePath
$packageLocalization = Read-RequiredSource $packageLocalizationPath

Assert-Matches $packageProject '<Project\s+Sdk="WixToolset\.Sdk/5\.[^"]+"' `
    "The MSI must use the SDK-style WiX 5 project restored by MSBuild/NuGet."
Assert-Matches $packageProject '<InstallerPlatform>x64</InstallerPlatform>' `
    "The MSI project must target x64."
Assert-Matches $packageProject '<SuppressSpecificWarnings>1101</SuppressSpecificWarnings>' `
    "The MSI project must document the neutral-language override for the SQLite native DLL."
Assert-Matches $packageProject '<BindPath\s+Include="\$\(PayloadDir\)"\s+BindName="Payload"\s*/>' `
    "The MSI project must consume the clean payload through a named bind path."
Assert-Matches $packageSource 'UpgradeCode="\{4F4DC23B-3E1D-4C72-92A1-3A5D6F277F54\}"' `
    "The MSI must retain the stable Hanabe upgrade identity."
Assert-Matches $packageSource 'Scope="perMachine"' `
    "The MSI must install per machine."
Assert-Matches $packageSource '<StandardDirectory\s+Id="ProgramFiles64Folder"' `
    "The MSI must install under 64-bit Program Files."
Assert-Matches $packageSource '<Files\s+Include="!\(bindpath\.Payload\)\\\*\*"' `
    "The MSI must harvest the clean publish payload."
Assert-Matches $packageSource '<File\s+Id="HanabePhotoManagerExe"[^>]+Source="!\(bindpath\.Payload\)\\HanabePhotoManager\.App\.exe"' `
    "The MSI must explicitly author the application executable."
Assert-Matches $packageSource '<File\s+Id="HanabePhotoManagerExe"[\s\S]+<Shortcut\s+Id="DesktopShortcut"[^>]+Directory="DesktopFolder"[^>]+Advertise="yes"' `
    "The MSI must create an advertised desktop shortcut owned by the application executable."
Assert-Matches $packageSource '<File\s+Id="HanabePhotoManagerExe"[\s\S]+<Shortcut\s+Id="StartMenuShortcut"[^>]+Directory="ApplicationProgramsFolder"[^>]+Advertise="yes"' `
    "The MSI must create an advertised Start menu shortcut owned by the application executable."
Assert-DoesNotMatch $packageSource '<Shortcut[^>]+Target=' `
    "Advertised shortcuts must use their parent File as the target."
Assert-Matches $packageSource '<RemoveFolder\s+Id="RemoveApplicationProgramsFolder"[^>]+Directory="ApplicationProgramsFolder"[^>]+On="uninstall"' `
    "The MSI must remove its empty Start menu folder during uninstall."
Assert-Matches $packageSource '<File[^>]+Source="!\(bindpath\.Payload\)\\e_sqlite3\.dll"[^>]+DefaultLanguage="0"' `
    "The SQLite native DLL must declare a neutral language for MSI validation."
Assert-Matches $packageSource '<MajorUpgrade[^>]+DowngradeErrorMessage="!\(loc\.DowngradeErrorMessage\)"' `
    "The MSI must block downgrades with a localized message."
Assert-DoesNotMatch $packageSource 'RemoveFolderEx|AppDataFolder|LocalAppDataFolder|CommonAppDataFolder' `
    "The installer must never author removal of application data directories."
Assert-Matches $packageLocalization 'Culture="zh-CN"' `
    "The package must include Simplified Chinese localization."
Assert-Matches $packageLocalization 'String\s+Id="DowngradeErrorMessage"' `
    "The package must localize its downgrade prevention message."

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Error $failure -ErrorAction Continue
    }

    throw "Installer authoring source assertions failed: $($failures.Count)"
}

Write-Host "Installer authoring source assertions passed."
