$ErrorActionPreference='Stop'
$env:VISIOFORMULA_CACHE=Join-Path $PSScriptRoot 'verification\runtime-cache'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /utf8output ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')) /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ('/r:'+(Join-Path $PSScriptRoot 'VisioFormula.exe')) ('/out:'+(Join-Path $PSScriptRoot 'RealtimeTests.exe')) (Join-Path $PSScriptRoot 'src\RealtimeTests.cs')
if($LASTEXITCODE -ne 0){throw 'Test build failed'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VisioFormula.exe.config') -Destination (Join-Path $PSScriptRoot 'RealtimeTests.exe.config') -Force
& (Join-Path $PSScriptRoot 'RealtimeTests.exe')
if($LASTEXITCODE -ne 0){throw 'Realtime tests failed'}
