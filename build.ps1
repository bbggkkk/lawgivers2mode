$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$refs = Join-Path $root 'vendor\MelonLoader\MelonLoader\net472'
$out = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $out | Out-Null

& dotnet restore "$root\LawgiversControl.csproj" --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { throw "C# restore failed with exit code $LASTEXITCODE" }
& dotnet build "$root\LawgiversControl.csproj" -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed with exit code $LASTEXITCODE" }
Copy-Item -LiteralPath "$root\bin\Release\net6.0\LawgiversControl.dll" -Destination "$out\LawgiversControl.dll" -Force
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' `
  /nologo /target:exe /optimize+ `
  /out:"$out\DisableXrefScan.exe" `
  /reference:"$refs\Mono.Cecil.dll" `
  "$root\tools\DisableXrefScan.cs"
if ($LASTEXITCODE -ne 0) { throw "XRef patcher compilation failed with exit code $LASTEXITCODE" }
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' `
  /nologo /target:exe /optimize+ `
  /out:"$out\DisableCoroutineWrapper.exe" `
  /reference:"$refs\Mono.Cecil.dll" `
  "$root\tools\DisableCoroutineWrapper.cs"
if ($LASTEXITCODE -ne 0) { throw "Coroutine wrapper patcher compilation failed with exit code $LASTEXITCODE" }
Copy-Item -LiteralPath "$refs\Mono.Cecil.dll" -Destination "$out\Mono.Cecil.dll" -Force
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' `
  /nologo /target:exe /optimize+ `
  /out:"$out\ModLogicTests.exe" `
  /reference:"$refs\Newtonsoft.Json.dll" `
  "$root\tests\TestStubs.cs" `
  "$root\src\LawgiversControlMod.cs" `
  "$root\tests\ModLogicTests.cs"
if ($LASTEXITCODE -ne 0) { throw "Logic test compilation failed with exit code $LASTEXITCODE" }
Copy-Item -LiteralPath "$refs\MelonLoader.dll" -Destination "$out\MelonLoader.dll" -Force
Copy-Item -LiteralPath "$refs\0Harmony.dll" -Destination "$out\0Harmony.dll" -Force
Copy-Item -LiteralPath "$refs\Newtonsoft.Json.dll" -Destination "$out\Newtonsoft.Json.dll" -Force
Get-Item -LiteralPath "$out\LawgiversControl.dll"
