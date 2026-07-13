$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

$scripts = @(
  'steam-path.ps1',
  'install.ps1',
  'install-online.ps1',
  'verify.ps1',
  'uninstall.ps1',
  'build-release.ps1',
  'publish-release.ps1'
)
foreach ($script in $scripts) {
  $scriptPath = Join-Path $root $script
  if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) { continue }
  $tokens = $null
  $errors = $null
  [void][Management.Automation.Language.Parser]::ParseFile(
    $scriptPath,
    [ref]$tokens,
    [ref]$errors
  )
  if ($errors.Count -gt 0) { throw "PowerShell syntax error in $script`: $($errors[0].Message)" }
}

. (Join-Path $root 'steam-path.ps1')
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('LawgiversControl-path-test-' + [Guid]::NewGuid().ToString('N'))
try {
  $steamApps = Join-Path $testRoot 'steamapps'
  $gamePath = Join-Path $steamApps 'common\Lawgivers II Custom'
  New-Item -ItemType Directory -Force -Path $gamePath | Out-Null
  New-Item -ItemType File -Force -Path (Join-Path $gamePath 'Lawgivers II.exe') | Out-Null
  Set-Content -LiteralPath (Join-Path $steamApps 'appmanifest_1407180.acf') -Encoding ASCII -Value @'
"AppState"
{
  "appid" "1407180"
  "installdir" "Lawgivers II Custom"
}
'@

  $detected = Resolve-LawgiversGamePath -SteamRoots @($testRoot)
  if ($detected -ne [IO.Path]::GetFullPath($gamePath)) { throw "Unexpected detected path: $detected" }

  $explicit = Resolve-LawgiversGamePath -GamePath $gamePath -SteamRoots @()
  if ($explicit -ne [IO.Path]::GetFullPath($gamePath)) { throw "Unexpected explicit path: $explicit" }

  $invalidFailed = $false
  try { Resolve-LawgiversGamePath -GamePath (Join-Path $testRoot 'missing') -SteamRoots @() } catch {
    $invalidFailed = $_.Exception.Message -match 'supplied GamePath'
  }
  if (-not $invalidFailed) { throw 'An invalid explicit path did not produce the expected error.' }

  $missingFailed = $false
  try { Resolve-LawgiversGamePath -SteamRoots @((Join-Path $testRoot 'empty')) } catch {
    $missingFailed = $_.Exception.Message -match 'App ID 1407180'
  }
  if (-not $missingFailed) { throw 'A missing Steam installation did not produce the expected error.' }

  $onlineInstaller = Join-Path $root 'install-online.ps1'
  if (Test-Path -LiteralPath $onlineInstaller -PathType Leaf) {
    $bootstrapSource = Join-Path $testRoot 'bootstrap-source'
    New-Item -ItemType Directory -Force -Path $bootstrapSource | Out-Null
    Set-Content -LiteralPath (Join-Path $bootstrapSource 'install.ps1') -Encoding UTF8 -Value @'
param([string]$GamePath)
if ([string]::IsNullOrWhiteSpace($GamePath)) { throw 'GamePath was not forwarded.' }
Set-Content -LiteralPath (Join-Path $GamePath 'installer-test.flag') -Value 'installed'
'@
    Set-Content -LiteralPath (Join-Path $bootstrapSource 'verify.ps1') -Encoding UTF8 -Value @'
param([string]$GamePath)
if (-not (Test-Path -LiteralPath (Join-Path $GamePath 'installer-test.flag'))) { throw 'Bootstrap verification failed.' }
'@
    $package = Join-Path $testRoot 'Lawgivers-II-Control-0.0.0-test.zip'
    Compress-Archive -Path (Join-Path $bootstrapSource '*') -DestinationPath $package
    $checksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $package).Hash
    $checksumPath = Join-Path $testRoot 'SHA256SUMS.txt'
    Set-Content -LiteralPath $checksumPath -Encoding ASCII -Value "$checksum  $(Split-Path -Leaf $package)"
    & $onlineInstaller -PackagePath $package -ChecksumPath $checksumPath -GamePath $gamePath
    if (-not (Test-Path -LiteralPath (Join-Path $gamePath 'installer-test.flag'))) {
      throw 'The local-package bootstrap did not run the installer.'
    }
  }
}
finally {
  Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output 'PASS: installer syntax, Steam discovery, override, failure handling, and local bootstrap verified.'
