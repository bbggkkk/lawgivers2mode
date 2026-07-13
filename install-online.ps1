param(
  [string]$Repository = 'bbggkkk/lawgivers2mode',
  [string]$Version,
  [string]$GamePath,
  [string]$PackagePath,
  [string]$ChecksumPath
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$work = Join-Path ([IO.Path]::GetTempPath()) ('LawgiversControl-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

try {
  $zip = Join-Path $work 'package.zip'
  $sums = Join-Path $work 'SHA256SUMS.txt'

  if ($PackagePath -or $ChecksumPath) {
    if (-not $PackagePath -or -not $ChecksumPath) { throw 'PackagePath and ChecksumPath must be provided together.' }
    Copy-Item -LiteralPath $PackagePath -Destination $zip
    Copy-Item -LiteralPath $ChecksumPath -Destination $sums
    $assetName = Split-Path -Leaf $PackagePath
  }
  else {
    $apiVersion = if ($Version) { $Version } else { 'latest' }
    $endpoint = if ($Version) {
      'https://api.github.com/repos/{0}/releases/tags/{1}' -f $Repository, $Version
    } else {
      'https://api.github.com/repos/{0}/releases/latest' -f $Repository
    }
    Write-Output "Querying GitHub Release: $Repository ($apiVersion)"
    $headers = @{ Accept = 'application/vnd.github+json'; 'User-Agent' = 'Lawgivers-II-Control-Installer' }
    $release = Invoke-RestMethod -Uri $endpoint -Headers $headers -UseBasicParsing
    $zipAsset = @($release.assets) | Where-Object { $_.name -match '^Lawgivers-II-Control-[0-9].*\.zip$' } | Select-Object -First 1
    $sumAsset = @($release.assets) | Where-Object { $_.name -eq 'SHA256SUMS.txt' } | Select-Object -First 1
    if (-not $zipAsset -or -not $sumAsset) { throw 'The release is missing the installation ZIP or SHA256SUMS.txt.' }
    $assetName = $zipAsset.name
    Invoke-WebRequest -Uri $zipAsset.browser_download_url -Headers $headers -UseBasicParsing -OutFile $zip
    Invoke-WebRequest -Uri $sumAsset.browser_download_url -Headers $headers -UseBasicParsing -OutFile $sums
  }

  $checksumLine = Get-Content -LiteralPath $sums | Where-Object { $_ -match ('\s+\*?' + [Regex]::Escape($assetName) + '$') } | Select-Object -First 1
  if (-not $checksumLine) { throw "The checksum file has no entry for $assetName." }
  $expected = ($checksumLine -split '\s+')[0].ToUpperInvariant()
  $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash.ToUpperInvariant()
  if ($expected -ne $actual) { throw "SHA-256 mismatch: expected=$expected actual=$actual" }
  Write-Output "PASS: download SHA-256 verified ($actual)"

  $extract = Join-Path $work 'extracted'
  Expand-Archive -LiteralPath $zip -DestinationPath $extract
  $installers = @(Get-ChildItem -LiteralPath $extract -Filter install.ps1 -File -Recurse)
  if ($installers.Count -ne 1) { throw "Expected exactly one install.ps1, found $($installers.Count)." }
  $installArguments = @{}
  if (-not [string]::IsNullOrWhiteSpace($GamePath)) { $installArguments.GamePath = $GamePath }
  & $installers[0].FullName @installArguments

  $verify = Join-Path $installers[0].DirectoryName 'verify.ps1'
  if (Test-Path -LiteralPath $verify) {
    $verifyArguments = @{}
    if (-not [string]::IsNullOrWhiteSpace($GamePath)) { $verifyArguments.GamePath = $GamePath }
    & $verify @verifyArguments
  }
  Write-Output 'Lawgivers II Control installation completed.'
}
finally {
  $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
  $resolvedWork = [IO.Path]::GetFullPath($work)
  if ($resolvedWork.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
      (Split-Path -Leaf $resolvedWork).StartsWith('LawgiversControl-', [StringComparison]::Ordinal)) {
    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
  }
}
