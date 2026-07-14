param(
  [string]$Version = '1.3.1',
  [string]$Repository = 'bbggkkk/lawgivers2mode',
  [string]$NotesPath = (Join-Path $PSScriptRoot 'RELEASE_NOTES.md'),
  [switch]$SkipPackageBuild
)

$ErrorActionPreference = 'Stop'
$gh = (Get-Command gh.exe -ErrorAction SilentlyContinue).Source
if (-not $gh -and (Test-Path -LiteralPath 'C:\Program Files\GitHub CLI\gh.exe')) {
  $gh = 'C:\Program Files\GitHub CLI\gh.exe'
}
if (-not $gh) { throw 'GitHub CLI (gh) is required: https://cli.github.com/' }
& $gh auth status
if ($LASTEXITCODE -ne 0) { throw 'Run gh auth login first.' }

$tag = 'v' + $Version.TrimStart('v')
$assetName = 'Lawgivers-II-Control-' + $Version.TrimStart('v') + '.zip'
$asset = Join-Path $PSScriptRoot ('release\' + $assetName)
if (-not $SkipPackageBuild) {
  & (Join-Path $PSScriptRoot 'build-release.ps1') -Version $Version
}
if (-not (Test-Path -LiteralPath $asset)) { throw "Release ZIP not found: $asset" }
if (-not (Test-Path -LiteralPath $NotesPath)) { throw "Release notes not found: $NotesPath" }

$checksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $asset).Hash
$checksumPath = Join-Path $PSScriptRoot 'release\SHA256SUMS.txt'
Set-Content -LiteralPath $checksumPath -Encoding ASCII -Value "$checksum  $assetName"

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'SilentlyContinue'
try {
  & $gh release view $tag --repo $Repository *> $null
  $releaseExists = $LASTEXITCODE -eq 0
}
finally {
  $ErrorActionPreference = $previousErrorActionPreference
}

if ($releaseExists) {
  & $gh release upload $tag $asset $checksumPath --repo $Repository --clobber
}
else {
  & $gh release create $tag $asset $checksumPath --repo $Repository --title "Lawgivers II Control $Version" --notes-file $NotesPath
}
if ($LASTEXITCODE -ne 0) { throw 'GitHub Release publishing failed.' }
Write-Output "Published: https://github.com/$Repository/releases/tag/$tag"
