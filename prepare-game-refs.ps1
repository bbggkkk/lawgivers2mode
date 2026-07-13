$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $root 'steam-path.ps1')
$game = Find-LawgiversGamePath
$source = Join-Path $game '_modding\cpp2il_out'
$destination = Join-Path $root 'vendor\GameRefs'
$assemblies = @(
  'UnityEngine.CoreModule.dll',
  'UnityEngine.UI.dll',
  'UnityEngine.UIModule.dll',
  'UnityEngine.TextRenderingModule.dll'
)
New-Item -ItemType Directory -Force -Path $destination | Out-Null
foreach ($assembly in $assemblies) {
  $path = Join-Path $source $assembly
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    throw "Missing Cpp2IL reference: $path. Generate the dummy assemblies in _modding\cpp2il_out, then retry."
  }
  Copy-Item -LiteralPath $path -Destination (Join-Path $destination $assembly) -Force
}
Write-Host "Prepared generated game references from: $game"
