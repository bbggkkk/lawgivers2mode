param(
  [string]$GamePath,
  [switch]$RemoveLoader,
  [switch]$RemoveConfig,
  [switch]$Elevated
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$pathHelper = Join-Path $root 'steam-path.ps1'
if (-not (Test-Path -LiteralPath $pathHelper -PathType Leaf)) { throw "Uninstaller path helper not found: $pathHelper" }
. $pathHelper
$GamePath = Resolve-LawgiversGamePath -GamePath $GamePath

if (-not (Test-IsAdministrator) -and -not (Test-DirectoryWritable -Path $GamePath)) {
  if ($Elevated) { throw "Administrator access is required to uninstall from $GamePath" }
  $arguments = @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Quote-NativeArgument $PSCommandPath),
    '-GamePath', (Quote-NativeArgument $GamePath), '-Elevated'
  )
  if ($RemoveLoader) { $arguments += '-RemoveLoader' }
  if ($RemoveConfig) { $arguments += '-RemoveConfig' }
  $process = Start-Process powershell.exe -Verb RunAs -Wait -PassThru -ArgumentList ($arguments -join ' ')
  if ($process.ExitCode -ne 0) { throw "Elevated uninstaller failed with exit code $($process.ExitCode)." }
  return
}

if (Get-Process -Name 'Lawgivers II' -ErrorAction SilentlyContinue) { throw 'Close Lawgivers II before uninstalling.' }
$mod = Join-Path $GamePath 'Mods\LawgiversControl.dll'
$config = Join-Path $GamePath 'UserData\LawgiversControl'
$marker = Join-Path $GamePath '.lawgivers-control-loader'
$hasRequestedTarget = (Test-Path -LiteralPath $mod -PathType Leaf) -or
  ($RemoveConfig -and (Test-Path -LiteralPath $config -PathType Container)) -or
  ($RemoveLoader -and (Test-Path -LiteralPath $marker -PathType Leaf))
if (-not $hasRequestedTarget) { throw "No requested Lawgivers II Control installation was found at $GamePath" }

if (Test-Path -LiteralPath $mod -PathType Leaf) { Remove-Item -LiteralPath $mod -Force }
if ($RemoveConfig -and (Test-Path -LiteralPath $config -PathType Container)) { Remove-Item -LiteralPath $config -Recurse -Force }
if ($RemoveLoader -and (Test-Path -LiteralPath $marker -PathType Leaf)) {
  $loaderOwned = $true
  try {
    $markerData = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if ($null -ne $markerData.LoaderOwned) { $loaderOwned = [bool]$markerData.LoaderOwned }
  } catch { $loaderOwned = $true }
  $proxy = Join-Path $GamePath 'version.dll'
  $loader = Join-Path $GamePath 'MelonLoader'
  if ($loaderOwned) {
    if (Test-Path -LiteralPath $proxy -PathType Leaf) { Remove-Item -LiteralPath $proxy -Force }
    if (Test-Path -LiteralPath $loader -PathType Container) { Remove-Item -LiteralPath $loader -Recurse -Force }
  } else {
    Write-Output 'Shared pre-existing MelonLoader retained.'
  }
  Remove-Item -LiteralPath $marker -Force
}
Write-Output "Lawgivers II Control removed from $GamePath"
