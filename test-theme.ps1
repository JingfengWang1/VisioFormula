$ErrorActionPreference='Stop'
$env:VISIOFORMULA_CACHE=Join-Path $PSScriptRoot 'verification\runtime-cache'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /utf8output ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')) /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ('/r:'+(Join-Path $PSScriptRoot 'VisioFormula.exe')) ('/out:'+(Join-Path $PSScriptRoot 'ThemeTests.exe')) (Join-Path $PSScriptRoot 'src\ThemeTests.cs')
if($LASTEXITCODE -ne 0){throw 'Theme test build failed'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VisioFormula.exe.config') -Destination (Join-Path $PSScriptRoot 'ThemeTests.exe.config') -Force
& (Join-Path $PSScriptRoot 'ThemeTests.exe')
if($LASTEXITCODE -ne 0){throw 'Native DPI theme tests failed'}
& (Join-Path $PSScriptRoot 'ThemeTests.exe') --96
if($LASTEXITCODE -ne 0){throw '96 DPI theme tests failed'}
