param([string]$PackageRoot = (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)))

$ErrorActionPreference = 'Stop'
$PackageRoot = [IO.Path]::GetFullPath($PackageRoot)
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('LawgiversControl-installer-integration-' + [Guid]::NewGuid().ToString('N'))
$game = Join-Path $testRoot 'Lawgivers II'

try {
  New-Item -ItemType Directory -Force -Path (Join-Path $game 'MelonLoader\net472') | Out-Null
  New-Item -ItemType File -Force -Path (Join-Path $game 'Lawgivers II.exe') | Out-Null
  Copy-Item -LiteralPath (Join-Path $PackageRoot 'vendor\MelonLoader\version.dll') -Destination $game
  Copy-Item -LiteralPath (Join-Path $PackageRoot 'vendor\MelonLoader\MelonLoader\net472\MelonLoader.dll') -Destination (Join-Path $game 'MelonLoader\net472')

  & (Join-Path $PackageRoot 'install.ps1') -GamePath $game
  $markerPath = Join-Path $game '.lawgivers-control-loader'
  $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
  if ($marker.LoaderOwned -ne $false -or $marker.ReusedCompatibleLoader -ne $true) {
    throw 'Compatible pre-existing loader ownership was recorded incorrectly.'
  }
  if (-not (Test-Path -LiteralPath (Join-Path $game 'Mods\LawgiversControl.dll'))) {
    throw 'The mod DLL was not installed in the shared-loader scenario.'
  }

  & (Join-Path $PackageRoot 'uninstall.ps1') -GamePath $game -RemoveLoader -RemoveConfig
  if (-not (Test-Path -LiteralPath (Join-Path $game 'version.dll')) -or
      -not (Test-Path -LiteralPath (Join-Path $game 'MelonLoader'))) {
    throw 'The shared pre-existing loader was removed.'
  }
  if ((Test-Path -LiteralPath $markerPath) -or (Test-Path -LiteralPath (Join-Path $game 'Mods\LawgiversControl.dll'))) {
    throw 'The mod or ownership marker remained after uninstall.'
  }
}
finally {
  $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
  $resolved = [IO.Path]::GetFullPath($testRoot)
  if ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
      (Split-Path -Leaf $resolved).StartsWith('LawgiversControl-installer-integration-', [StringComparison]::Ordinal)) {
    Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
  }
}

Write-Output 'PASS: compatible shared MelonLoader install and ownership-safe uninstall verified.'
