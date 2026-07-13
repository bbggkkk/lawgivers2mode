$script:LawgiversSteamAppId = '1407180'
$script:LawgiversExecutable = 'Lawgivers II.exe'

function Get-LawgiversSteamRoots {
  $roots = New-Object System.Collections.Generic.List[string]

  foreach ($registryPath in @(
    'HKCU:\Software\Valve\Steam',
    'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam',
    'HKLM:\SOFTWARE\Valve\Steam'
  )) {
    $steam = Get-ItemProperty -Path $registryPath -ErrorAction SilentlyContinue
    foreach ($property in @('SteamPath', 'InstallPath')) {
      if ($steam -and $steam.$property) { $roots.Add([string]$steam.$property) }
    }
  }

  foreach ($programFiles in @(
    [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86),
    [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFiles)
  )) {
    if (-not [string]::IsNullOrWhiteSpace($programFiles)) {
      $roots.Add((Join-Path $programFiles 'Steam'))
    }
  }

  $resolved = New-Object System.Collections.Generic.List[string]
  foreach ($root in @($roots)) {
    if ([string]::IsNullOrWhiteSpace($root)) { continue }
    try { $fullRoot = [IO.Path]::GetFullPath($root) } catch { continue }
    if (-not (Test-Path -LiteralPath $fullRoot -PathType Container)) { continue }
    if (-not $resolved.Contains($fullRoot)) { $resolved.Add($fullRoot) }

    $libraryFile = Join-Path $fullRoot 'steamapps\libraryfolders.vdf'
    if (-not (Test-Path -LiteralPath $libraryFile -PathType Leaf)) { continue }
    $libraryText = Get-Content -LiteralPath $libraryFile -Raw
    foreach ($match in [regex]::Matches($libraryText, '(?m)^\s*"path"\s+"([^"]+)"')) {
      $libraryRoot = $match.Groups[1].Value -replace '\\\\', '\'
      try { $libraryRoot = [IO.Path]::GetFullPath($libraryRoot) } catch { continue }
      if ((Test-Path -LiteralPath $libraryRoot -PathType Container) -and -not $resolved.Contains($libraryRoot)) {
        $resolved.Add($libraryRoot)
      }
    }
  }

  return @($resolved)
}

function Resolve-LawgiversGamePath {
  param(
    [string]$GamePath,
    [string[]]$SteamRoots
  )

  if (-not [string]::IsNullOrWhiteSpace($GamePath)) {
    $resolvedPath = [IO.Path]::GetFullPath($GamePath)
    $exe = Join-Path $resolvedPath $script:LawgiversExecutable
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
      throw "Lawgivers II.exe not found at the supplied GamePath: $resolvedPath"
    }
    return $resolvedPath
  }

  $searched = New-Object System.Collections.Generic.List[string]
  if ($null -eq $SteamRoots) { $SteamRoots = @(Get-LawgiversSteamRoots) }
  foreach ($steamRoot in @($SteamRoots)) {
    $steamApps = Join-Path $steamRoot 'steamapps'
    $manifest = Join-Path $steamApps ("appmanifest_{0}.acf" -f $script:LawgiversSteamAppId)
    $searched.Add($manifest)
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { continue }

    $manifestText = Get-Content -LiteralPath $manifest -Raw
    $installMatch = [regex]::Match($manifestText, '(?m)^\s*"installdir"\s+"([^"]+)"')
    if (-not $installMatch.Success) { continue }

    $candidate = Join-Path (Join-Path $steamApps 'common') $installMatch.Groups[1].Value
    $exe = Join-Path $candidate $script:LawgiversExecutable
    if (Test-Path -LiteralPath $exe -PathType Leaf) {
      return [IO.Path]::GetFullPath($candidate)
    }
  }

  $details = if ($searched.Count -gt 0) { " Searched manifests: " + ($searched -join '; ') } else { '' }
  throw "Lawgivers II Steam installation was not found (App ID $script:LawgiversSteamAppId). Supply -GamePath with the directory containing Lawgivers II.exe.$details"
}

function Test-IsAdministrator {
  $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($identity)
  return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-DirectoryWritable {
  param([Parameter(Mandatory = $true)][string]$Path)

  $probe = Join-Path $Path ('.lawgivers-control-write-test-' + [Guid]::NewGuid().ToString('N'))
  try {
    [IO.File]::WriteAllText($probe, '')
    return $true
  }
  catch {
    return $false
  }
  finally {
    Remove-Item -LiteralPath $probe -Force -ErrorAction SilentlyContinue
  }
}

function Quote-NativeArgument {
  param([Parameter(Mandatory = $true)][string]$Value)
  if ($Value.Contains('"')) { throw 'A command argument contains an unsupported quote character.' }
  $escaped = $Value -replace '(\\+)$', '$1$1'
  return '"' + $escaped + '"'
}
