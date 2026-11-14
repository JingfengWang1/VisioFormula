$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /utf8output /r:System.Web.Extensions.dll ('/r:'+(Join-Path $PSScriptRoot 'VisioFormula.exe')) ('/out:'+(Join-Path $PSScriptRoot 'FontTests.exe')) (Join-Path $PSScriptRoot 'src\FontTests.cs')
if($LASTEXITCODE -ne 0){throw 'Font test build failed'}
& (Join-Path $PSScriptRoot 'FontTests.exe')
if($LASTEXITCODE -ne 0){throw 'Font tests failed'}
