param(
  [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Lawgivers II',
  [switch]$RemoveLoader,
  [switch]$RemoveConfig
)

$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'Lawgivers II' -ErrorAction SilentlyContinue) { throw 'Close Lawgivers II before uninstalling.' }
Remove-Item -LiteralPath (Join-Path $GamePath 'Mods\LawgiversControl.dll') -Force -ErrorAction SilentlyContinue
if ($RemoveConfig) { Remove-Item -LiteralPath (Join-Path $GamePath 'UserData\LawgiversControl') -Recurse -Force -ErrorAction SilentlyContinue }
if ($RemoveLoader -and (Test-Path -LiteralPath (Join-Path $GamePath '.lawgivers-control-loader'))) {
  Remove-Item -LiteralPath (Join-Path $GamePath 'version.dll') -Force -ErrorAction SilentlyContinue
  Remove-Item -LiteralPath (Join-Path $GamePath 'MelonLoader') -Recurse -Force -ErrorAction SilentlyContinue
  Remove-Item -LiteralPath (Join-Path $GamePath '.lawgivers-control-loader') -Force -ErrorAction SilentlyContinue
}
Write-Output 'Lawgivers II Control mod removed.'
