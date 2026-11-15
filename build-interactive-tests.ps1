$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /utf8output ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')) /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll ('/r:'+(Join-Path $PSScriptRoot 'VisioFormula.exe')) ('/out:'+(Join-Path $PSScriptRoot 'InteractiveTests.exe')) (Join-Path $PSScriptRoot 'src\InteractiveTests.cs')
if($LASTEXITCODE -ne 0){throw 'Interactive harness build failed'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VisioFormula.exe.config') -Destination (Join-Path $PSScriptRoot 'InteractiveTests.exe.config') -Force
Write-Output 'Built InteractiveTests.exe. Run it for isolated physical drag/drop verification; close the formula test window to save/close its owned drawing.'