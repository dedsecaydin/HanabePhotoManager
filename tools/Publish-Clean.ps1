[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$')][string]$Version,
    [string]$OutputRoot = "artifacts"
)

$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$outputCandidate = if ([IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot
}
else {
    Join-Path $root $OutputRoot
}
$output = [IO.Path]::GetFullPath($outputCandidate)
$resolvedRoot = $root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar

if ([IO.Path]::GetPathRoot($output) -ne "D:\") {
    throw "Release output must remain on the D drive."
}

if (-not (($output.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar).StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase))) {
    throw "Release output must remain inside the project directory."
}

$releaseRoot = Join-Path $output $Version
$publish = Join-Path $releaseRoot "payload\win-x64"
$manifestPath = Join-Path $releaseRoot "release-manifest.json"
$project = Join-Path $root "src\HanabePhotoManager.App\HanabePhotoManager.App.csproj"
$installerProject = Join-Path $root "installer\HanabePhotoManager.Installer\HanabePhotoManager.Installer.wixproj"
$setupProject = Join-Path $root "installer\HanabePhotoManager.InstallerShell\HanabePhotoManager.InstallerShell.csproj"
$msiBuildOutput = Join-Path $releaseRoot "installer-build"
$setupBuildOutput = Join-Path $releaseRoot "setup-build"
$msiArtifact = Join-Path $releaseRoot "HanabePhotoManager-x64.msi"
$setupArtifact = Join-Path $releaseRoot "HanabePhotoManager-Setup-x64.exe"
$setupChecksumPath = Join-Path $releaseRoot "HanabePhotoManager-Setup-x64.exe.sha256"

if (Test-Path -LiteralPath $releaseRoot) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publish -Force | Out-Null
dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish -p:PublishReadyToRun=true -p:Version=$Version -p:HanabeVersion=$Version
if ($LASTEXITCODE -ne 0) { throw "Publish failed: $LASTEXITCODE" }

# Keep the WebView2 runtime DLLs. Remove only browser user-data accidentally
# created by running the executable inside a previous publish directory.
Get-ChildItem -LiteralPath $publish -Directory -Recurse -Force |
    Where-Object { $_.Name -like "*.exe.WebView2" } |
    Sort-Object FullName -Descending |
    Remove-Item -Recurse -Force

$msiVersion = $Version.Split('-')[0]
& dotnet build $installerProject -c Release "-p:ProductVersion=$msiVersion" "-p:PayloadDir=$publish" "-p:OutputPath=$msiBuildOutput\"
if ($LASTEXITCODE -ne 0) { throw "MSI build failed: $LASTEXITCODE" }

$builtMsi = Join-Path $msiBuildOutput "zh-CN\HanabePhotoManager-x64.msi"
if (-not (Test-Path -LiteralPath $builtMsi -PathType Leaf)) {
    throw "MSI build did not produce the expected artifact: $builtMsi"
}
Copy-Item -LiteralPath $builtMsi -Destination $msiArtifact -Force

& dotnet publish $setupProject -c Release -r win-x64 --self-contained true -o $setupBuildOutput "-p:Version=$Version" "-p:InstallerMsiPath=$msiArtifact" "-p:PublishSingleFile=true"
if ($LASTEXITCODE -ne 0) { throw "Setup build failed: $LASTEXITCODE" }

$builtSetup = Join-Path $setupBuildOutput "HanabePhotoManager-Setup-x64.exe"
if (-not (Test-Path -LiteralPath $builtSetup -PathType Leaf)) {
    throw "Setup build did not produce the expected artifact: $builtSetup"
}
Copy-Item -LiteralPath $builtSetup -Destination $setupArtifact -Force

$setupChecksum = (Get-FileHash -LiteralPath $setupArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
$msiChecksum = (Get-FileHash -LiteralPath $msiArtifact -Algorithm SHA256).Hash.ToLowerInvariant()
"$setupChecksum  HanabePhotoManager-Setup-x64.exe" | Set-Content -LiteralPath $setupChecksumPath -Encoding ASCII

$sourceRevision = (& git -C $root rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceRevision)) {
    $sourceRevision = "unknown"
}

$manifest = [ordered]@{
    version = $Version
    sourceRevision = $sourceRevision.Trim()
    runtime = "win-x64"
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    checksumInputs = @(
        "HanabePhotoManager-Setup-x64.exe"
        "HanabePhotoManager-x64.msi"
    )
    artifacts = @(
        [ordered]@{
            path = "HanabePhotoManager-Setup-x64.exe"
            sha256 = $setupChecksum
        }
        [ordered]@{
            path = "HanabePhotoManager-x64.msi"
            sha256 = $msiChecksum
        }
    )
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host $setupArtifact
