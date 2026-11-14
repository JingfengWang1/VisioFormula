$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /utf8output ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')) /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll ('/r:'+(Join-Path $PSScriptRoot 'VisioFormula.exe')) ('/out:'+(Join-Path $PSScriptRoot 'BatchTests.exe')) (Join-Path $PSScriptRoot 'src\BatchTests.cs')
if($LASTEXITCODE -ne 0){throw 'Batch test build failed'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VisioFormula.exe.config') -Destination (Join-Path $PSScriptRoot 'BatchTests.exe.config') -Force
& (Join-Path $PSScriptRoot 'BatchTests.exe')
if($LASTEXITCODE -ne 0){throw 'Batch tests failed'}
