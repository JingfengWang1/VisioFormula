$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /utf8output /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll ('/r:'+(Join-Path $PSScriptRoot 'VisioFormula.exe')) ('/out:'+(Join-Path $PSScriptRoot 'ConnectionTests.exe')) (Join-Path $PSScriptRoot 'src\ConnectionTests.cs')
if($LASTEXITCODE -ne 0){throw 'Connection test build failed'}
& (Join-Path $PSScriptRoot 'ConnectionTests.exe')
if($LASTEXITCODE -ne 0){throw 'Connection tests failed'}
