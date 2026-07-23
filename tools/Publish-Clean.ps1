param([string]$OutputRoot = "artifacts")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root $OutputRoot
$publish = Join-Path $output "HanabePhotoManager-v1.0-full-optimized"
$archive = Join-Path $output "HanabePhotoManager-v1.0-full-optimized.zip"
$project = Join-Path $root "src\HanabePhotoManager.App\HanabePhotoManager.App.csproj"

if (Test-Path -LiteralPath $publish) {
    $resolvedRoot = [IO.Path]::GetFullPath($root)
    $resolvedPublish = [IO.Path]::GetFullPath($publish)
    if (-not $resolvedPublish.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean outside the project directory."
    }
    Remove-Item -LiteralPath $publish -Recurse -Force
}

New-Item -ItemType Directory -Path $publish -Force | Out-Null
dotnet publish $project -c Release -r win-x64 --self-contained true -o $publish -p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) { throw "Publish failed: $LASTEXITCODE" }

# Keep the WebView2 runtime DLLs. Remove only browser user-data accidentally
# created by running the executable inside a previous publish directory.
Get-ChildItem -LiteralPath $publish -Directory -Recurse -Force |
    Where-Object { $_.Name -like "*.exe.WebView2" } |
    Sort-Object FullName -Descending |
    Remove-Item -Recurse -Force

if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $archive -CompressionLevel Optimal
Write-Host $archive
