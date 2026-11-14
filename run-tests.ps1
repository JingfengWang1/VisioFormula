param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'verification'), [string]$ApplicationDirectory = $PSScriptRoot,[switch]$SkipClipboard)
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /utf8output /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll ('/r:'+(Join-Path $ApplicationDirectory 'VisioFormula.exe')) ('/out:'+(Join-Path $ApplicationDirectory 'IntegrationTests.exe')) (Join-Path $PSScriptRoot 'src\IntegrationTests.cs')
if($LASTEXITCODE -ne 0){throw 'Test build failed'}
if($SkipClipboard){& (Join-Path $ApplicationDirectory 'IntegrationTests.exe') $OutputDirectory --without-clipboard}else{& (Join-Path $ApplicationDirectory 'IntegrationTests.exe') $OutputDirectory}
if($LASTEXITCODE -ne 0){throw 'Integration tests failed'}
