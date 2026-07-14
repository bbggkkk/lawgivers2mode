param(
  [string]$GamePath,
  [switch]$RequireLiveReport,
  [switch]$RequireRuntimeSelfTest
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$pathHelper = Join-Path $root 'steam-path.ps1'
if (-not (Test-Path -LiteralPath $pathHelper -PathType Leaf)) { throw "Verification path helper not found: $pathHelper" }
. $pathHelper
$GamePath = Resolve-LawgiversGamePath -GamePath $GamePath
$installed = Join-Path $GamePath 'Mods\LawgiversControl.dll'
$config = Join-Path $GamePath 'UserData\LawgiversControl\config.json'
$report = Join-Path $GamePath 'UserData\LawgiversControl\last-apply.json'
$runtimeReport = Join-Path $GamePath 'UserData\LawgiversControl\runtime-self-test.json'
$uiRuntimeReport = Join-Path $GamePath 'UserData\LawgiversControl\ui-runtime.json'

& (Join-Path $root 'tests\InstallerTests.ps1')
if (-not $?) { throw 'Installer tests failed.' }
& (Join-Path $root 'dist\ModLogicTests.exe')
if ($LASTEXITCODE -ne 0) { throw "Logic tests failed with exit code $LASTEXITCODE." }

if (-not (Test-Path -LiteralPath $installed)) { throw "Installed mod not found: $installed" }
$builtHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'dist\LawgiversControl.dll')).Hash
$installedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $installed).Hash
if ($builtHash -ne $installedHash) { throw 'Installed DLL does not match the current build. Run install.ps1.' }

if (-not (Test-Path -LiteralPath $config)) { throw "Configuration not found: $config" }
$null = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json

if ($RequireLiveReport)
{
  if (-not (Test-Path -LiteralPath $report)) { throw 'No live apply report exists. Load a single-player game and try again.' }
  $live = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
  if (-not $live.AppliedUtc -or $null -eq $live.People -or $null -eq $live.Parties -or $null -eq $live.Nations) {
    throw 'Live apply report is incomplete.'
  }
  Write-Output "PASS: live report verified at $($live.AppliedUtc)"
}

if ($RequireRuntimeSelfTest)
{
  if (-not (Test-Path -LiteralPath $runtimeReport)) { throw 'No runtime self-test report exists. Create runtime-self-test.flag and launch the game.' }
  $runtime = Get-Content -LiteralPath $runtimeReport -Raw | ConvertFrom-Json
  $checkProperties = @($runtime.Checks.PSObject.Properties)
  $failedChecks = @($checkProperties | Where-Object { $_.Value -ne $true })
  if ($runtime.Passed -ne $true -or $runtime.Error -or $checkProperties.Count -lt 19 -or $failedChecks.Count -gt 0) {
    throw 'Runtime self-test report contains missing or failed checks.'
  }
  Write-Output "PASS: $($checkProperties.Count) IL2CPP runtime checks verified at $($runtime.GeneratedUtc)"
  if (-not (Test-Path -LiteralPath $uiRuntimeReport)) { throw 'No context UI runtime report exists. Launch the game and try again.' }
  $uiRuntime = Get-Content -LiteralPath $uiRuntimeReport -Raw | ConvertFrom-Json
  if ($uiRuntime.Mode -ne 'ContextIntegrated' -or $uiRuntime.SeparateOverlay -ne $false -or $uiRuntime.CustomInputFields -ne $false) {
    throw 'Context UI runtime report is invalid.'
  }
  Write-Output "PASS: context-integrated UI verified at $($uiRuntime.GeneratedUtc)"
}

Write-Output "PASS: distribution build, logic tests, configuration, and installed SHA-256 verified."
Write-Output "SHA256: $builtHash"
