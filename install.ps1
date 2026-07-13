param(
  [string]$GamePath,
  [switch]$Elevated
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$pathHelper = Join-Path $root 'steam-path.ps1'
if (-not (Test-Path -LiteralPath $pathHelper -PathType Leaf)) { throw "Installer path helper not found: $pathHelper" }
. $pathHelper
$GamePath = Resolve-LawgiversGamePath -GamePath $GamePath

if (-not (Test-IsAdministrator) -and -not (Test-DirectoryWritable -Path $GamePath)) {
  if ($Elevated) { throw "Administrator access is required to install to $GamePath" }
  $arguments = @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Quote-NativeArgument $PSCommandPath),
    '-GamePath', (Quote-NativeArgument $GamePath), '-Elevated'
  )
  $process = Start-Process powershell.exe -Verb RunAs -Wait -PassThru -ArgumentList ($arguments -join ' ')
  if ($process.ExitCode -ne 0) { throw "Elevated installer failed with exit code $($process.ExitCode)." }
  return
}

$exe = Join-Path $GamePath 'Lawgivers II.exe'
if (Get-Process -Name 'Lawgivers II' -ErrorAction SilentlyContinue) { throw 'Close Lawgivers II before installing.' }

$existingProxy = Join-Path $GamePath 'version.dll'
$marker = Join-Path $GamePath '.lawgivers-control-loader'
$expectedProxy = Join-Path $root 'vendor\MelonLoader\version.dll'
$loaderOwned = $true
$reusedCompatibleLoader = $false
if (Test-Path -LiteralPath $marker -PathType Leaf) {
  try {
    $markerData = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if ($null -ne $markerData.LoaderOwned) { $loaderOwned = [bool]$markerData.LoaderOwned }
  } catch { $loaderOwned = $true }
}
elseif (Test-Path -LiteralPath $existingProxy -PathType Leaf) {
  $existingHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $existingProxy).Hash
  $expectedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $expectedProxy).Hash
  $existingLoader = Join-Path $GamePath 'MelonLoader\net472\MelonLoader.dll'
  if ($existingHash -ne $expectedHash -or -not (Test-Path -LiteralPath $existingLoader -PathType Leaf)) {
    throw 'version.dll belongs to an unknown or incompatible loader. The installer will not overwrite it.'
  }
  $loaderOwned = $false
  $reusedCompatibleLoader = $true
  Write-Output 'Compatible existing MelonLoader 0.7.3 x86 detected; reusing it without taking ownership.'
}

Copy-Item -LiteralPath $expectedProxy -Destination $GamePath -Force
Copy-Item -LiteralPath (Join-Path $root 'vendor\MelonLoader\MelonLoader') -Destination $GamePath -Recurse -Force
$generator = Join-Path $GamePath 'MelonLoader\net6\Il2CppInterop.Generator.dll'
if (Test-Path -LiteralPath $generator) {
  & (Join-Path $root 'dist\DisableXrefScan.exe') $generator
  if ($LASTEXITCODE -ne 0) { throw "Failed to disable the incompatible x86 XRef scan (exit $LASTEXITCODE)." }
}
$support = Join-Path $GamePath 'MelonLoader\Dependencies\SupportModules\Il2Cpp.dll'
if (Test-Path -LiteralPath $support) {
  & (Join-Path $root 'dist\DisableCoroutineWrapper.exe') $support
  if ($LASTEXITCODE -ne 0) { throw "Failed to disable incompatible IL2CPP support-class injection (exit $LASTEXITCODE)." }
}
$runtimeSource = Join-Path $root 'vendor\dotnet7-x86'
$runtimeTarget = Join-Path $GamePath 'MelonLoader\Dependencies\DotnetRuntime7'
if (Test-Path -LiteralPath (Join-Path $runtimeSource 'host\fxr\7.0.20\hostfxr.dll')) {
  New-Item -ItemType Directory -Force -Path $runtimeTarget | Out-Null
  Copy-Item -Path (Join-Path $runtimeSource '*') -Destination $runtimeTarget -Recurse -Force
}
New-Item -ItemType Directory -Force -Path (Join-Path $GamePath 'Mods') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $GamePath 'UserData\LawgiversControl') | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'dist\LawgiversControl.dll') -Destination (Join-Path $GamePath 'Mods\LawgiversControl.dll') -Force
if (-not (Test-Path -LiteralPath (Join-Path $GamePath 'UserData\LawgiversControl\config.json'))) {
  Copy-Item -LiteralPath (Join-Path $root 'config.example.json') -Destination (Join-Path $GamePath 'UserData\LawgiversControl\config.json')
}
$loaderConfig = Join-Path $GamePath 'UserData\Loader.cfg'
$runtimeConfig = Join-Path $GamePath 'MelonLoader\net6\MelonLoader.runtimeconfig.json'
if (Test-Path -LiteralPath $runtimeConfig) {
  $runtimeText = Get-Content -LiteralPath $runtimeConfig -Raw
  $runtimeText = $runtimeText -replace '"tfm"\s*:\s*"net6\.0"', '"tfm": "net7.0"'
  $runtimeText = $runtimeText -replace '"version"\s*:\s*"6\.0\.0"', '"version": "7.0.0"'
  Set-Content -LiteralPath $runtimeConfig -Value $runtimeText -Encoding UTF8
}
$hostfxr = Join-Path $runtimeTarget 'host\fxr\7.0.20\hostfxr.dll'
if (Test-Path -LiteralPath $hostfxr) {
  if (Test-Path -LiteralPath $loaderConfig) {
    $loaderText = Get-Content -LiteralPath $loaderConfig -Raw
    $loaderText = [regex]::Replace($loaderText, '(?m)^hostfxr_path_override\s*=.*$', "hostfxr_path_override = '$hostfxr'")
    $loaderText = $loaderText -replace 'disable_start_screen\s*=\s*(true|false)', 'disable_start_screen = true'
    $loaderText = $loaderText -replace 'disable_console_log_cleaner\s*=\s*(true|false)', 'disable_console_log_cleaner = true'
    $loaderText = $loaderText -replace 'force_offline_generation\s*=\s*(true|false)', 'force_offline_generation = true'
    Set-Content -LiteralPath $loaderConfig -Value $loaderText -Encoding UTF8
  } else {
    Set-Content -LiteralPath $loaderConfig -Value "[loader]`r`nhostfxr_path_override = '$hostfxr'`r`n" -Encoding UTF8
  }
}
$markerData = [ordered]@{
  ModVersion = '1.2.0'
  LoaderVersion = '0.7.3-x86'
  LoaderOwned = $loaderOwned
  ReusedCompatibleLoader = $reusedCompatibleLoader
  ProxySha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $expectedProxy).Hash
}
Set-Content -LiteralPath $marker -Encoding UTF8 -Value ($markerData | ConvertTo-Json)
Write-Output "Installed Lawgivers II Control to $GamePath"
