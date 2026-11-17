$ErrorActionPreference='Stop'
$env:VISIOFORMULA_CACHE=Join-Path $PSScriptRoot 'verification\powerpoint-cache'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /utf8output ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')) /r:Microsoft.CSharp.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ('/r:'+(Join-Path $PSScriptRoot 'VisioFormula.exe')) ('/out:'+(Join-Path $PSScriptRoot 'PowerPointTests.exe')) (Join-Path $PSScriptRoot 'src\PowerPointTests.cs')
if($LASTEXITCODE -ne 0){throw 'PowerPoint API test build failed'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VisioFormula.exe.config') -Destination (Join-Path $PSScriptRoot 'PowerPointTests.exe.config') -Force
& (Join-Path $PSScriptRoot 'PowerPointTests.exe')
if($LASTEXITCODE -ne 0){throw 'PowerPoint API tests failed'}