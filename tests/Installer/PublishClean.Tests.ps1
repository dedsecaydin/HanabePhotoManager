[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$publishScriptPath = Join-Path $repositoryRoot "tools\Publish-Clean.ps1"
$projectPath = Join-Path $repositoryRoot "src\HanabePhotoManager.App\HanabePhotoManager.App.csproj"
$publishScript = Get-Content -LiteralPath $publishScriptPath -Raw
$project = Get-Content -LiteralPath $projectPath -Raw
$failures = [Collections.Generic.List[string]]::new()

function Assert-Matches {
    param(
        [Parameter(Mandatory = $true)][string]$Actual,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($Actual -notmatch $Pattern) {
        $failures.Add($Message)
    }
}

function Assert-DoesNotMatch {
    param(
        [Parameter(Mandatory = $true)][string]$Actual,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if ($Actual -match $Pattern) {
        $failures.Add($Message)
    }
}

Assert-Matches $publishScript '\[Parameter\(Mandatory\s*=\s*\$true\)\][^\r\n]*\[ValidatePattern\([^\r\n]+\)\][^\r\n]*\[string\]\$Version' `
    "Publish-Clean.ps1 must require a normalized semantic Version parameter."
Assert-Matches $publishScript '\[IO\.Path\]::GetFullPath\(' `
    "Publish-Clean.ps1 must canonicalize output paths before using them."
Assert-Matches $publishScript 'GetPathRoot\(' `
    "Publish-Clean.ps1 must validate that release output stays on the D drive."
Assert-Matches $publishScript 'StartsWith\(\$resolvedRoot[^\r\n]+OrdinalIgnoreCase' `
    "Publish-Clean.ps1 must reject output outside the repository root."
Assert-DoesNotMatch $publishScript 'HanabePhotoManager-v1\.0' `
    "Publish-Clean.ps1 must not contain the fixed v1.0 artifact name."
Assert-Matches $publishScript '-p:Version=\$Version' `
    "Publish-Clean.ps1 must propagate Version into dotnet publish."
Assert-Matches $project '<Version>\$\(HanabeVersion\)</Version>' `
    "The App project must accept the release version through HanabeVersion."
Assert-Matches $publishScript 'release-manifest\.json' `
    "Publish-Clean.ps1 must create a release manifest."
Assert-Matches $publishScript 'sourceRevision' `
    "The release manifest must include the source revision."
Assert-Matches $publishScript 'checksumInputs' `
    "The release manifest must include checksum inputs."

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Error $failure -ErrorAction Continue
    }

    throw "Publish-Clean source assertions failed: $($failures.Count)"
}

Write-Host "Publish-Clean source assertions passed."
