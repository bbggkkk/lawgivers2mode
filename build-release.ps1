param(
  [string]$Version = '1.3.0',
  [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$normalizedVersion = $Version.TrimStart('v')
if ($normalizedVersion -notmatch '^\d+\.\d+\.\d+([.-][0-9A-Za-z.-]+)?$') {
  throw "Invalid release version: $Version"
}

$buildInputs = @(
  'vendor\MelonLoader\version.dll',
  'vendor\MelonLoader\MelonLoader\net472\MelonLoader.dll',
  'vendor\dotnet7-x86\host\fxr\7.0.20\hostfxr.dll'
)
$missingBuildInputs = @($buildInputs | Where-Object { -not (Test-Path -LiteralPath (Join-Path $root $_)) })
if ($missingBuildInputs.Count -gt 0) {
  throw "Release build inputs are missing: $($missingBuildInputs -join ', ')"
}

if (-not $SkipBuild) { & (Join-Path $root 'build.ps1') }

$required = @(
  'install.ps1',
  'verify.ps1',
  'uninstall.ps1',
  'steam-path.ps1',
  'tests\InstallerTests.ps1',
  'tests\InstallerIntegrationTests.ps1',
  'config.example.json',
  'README.md',
  'dist\LawgiversControl.dll',
  'dist\DisableXrefScan.exe',
  'dist\DisableCoroutineWrapper.exe',
  'dist\ModLogicTests.exe',
  'dist\Mono.Cecil.dll',
  'dist\Newtonsoft.Json.dll',
  'vendor\MelonLoader\version.dll',
  'vendor\MelonLoader\MelonLoader',
  'vendor\dotnet7-x86\host\fxr\7.0.20\hostfxr.dll'
)
$missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $root $_)) })
if ($missing.Count -gt 0) {
  throw "Release inputs are missing. Restore/build these paths before publishing: $($missing -join ', ')"
}

$releaseDirectory = Join-Path $root 'release'
New-Item -ItemType Directory -Force -Path $releaseDirectory | Out-Null
$stage = Join-Path $releaseDirectory ('.stage-' + [Guid]::NewGuid().ToString('N'))
$packageRoot = Join-Path $stage ('Lawgivers-II-Control-' + $normalizedVersion)
$temporaryZip = Join-Path $stage 'package.zip'
$assetName = 'Lawgivers-II-Control-' + $normalizedVersion + '.zip'
$asset = Join-Path $releaseDirectory $assetName

try {
  New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
  foreach ($file in @('install.ps1', 'verify.ps1', 'uninstall.ps1', 'steam-path.ps1', 'config.example.json', 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $packageRoot
  }
  New-Item -ItemType Directory -Force -Path (Join-Path $packageRoot 'tests') | Out-Null
  Copy-Item -LiteralPath (Join-Path $root 'tests\InstallerTests.ps1') -Destination (Join-Path $packageRoot 'tests')
  Copy-Item -LiteralPath (Join-Path $root 'tests\InstallerIntegrationTests.ps1') -Destination (Join-Path $packageRoot 'tests')
  Copy-Item -LiteralPath (Join-Path $root 'dist') -Destination $packageRoot -Recurse
  New-Item -ItemType Directory -Force -Path (Join-Path $packageRoot 'vendor') | Out-Null
  Copy-Item -LiteralPath (Join-Path $root 'vendor\MelonLoader') -Destination (Join-Path $packageRoot 'vendor') -Recurse
  Copy-Item -LiteralPath (Join-Path $root 'vendor\dotnet7-x86') -Destination (Join-Path $packageRoot 'vendor') -Recurse

  Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $temporaryZip -CompressionLevel Optimal
  Move-Item -LiteralPath $temporaryZip -Destination $asset -Force
}
finally {
  Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
}

Get-Item -LiteralPath $asset
