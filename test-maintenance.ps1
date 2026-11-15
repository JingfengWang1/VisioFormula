param([switch]$WithoutVisio)
$ErrorActionPreference='Stop'
$env:VISIOFORMULA_CACHE=Join-Path $PSScriptRoot 'verification\runtime-cache'
$testRoot=Join-Path $PSScriptRoot ('verification\maintenance-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$env:VF_MAINTENANCE_TEST_ROOT=$testRoot
$env:VF_TEST_EXTERNAL=Join-Path $testRoot 'external-fixture'
$env:VF_TEST_JUNCTION=Join-Path $testRoot 'junction-fixture'
New-Item -ItemType Directory -Path $env:VF_TEST_EXTERNAL -Force | Out-Null
New-Item -ItemType Junction -Path $env:VF_TEST_JUNCTION -Target $env:VF_TEST_EXTERNAL | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /utf8output ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')) /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll ('/r:'+(Join-Path $PSScriptRoot 'VisioFormula.exe')) ('/out:'+(Join-Path $PSScriptRoot 'MaintenanceTests.exe')) (Join-Path $PSScriptRoot 'src\MaintenanceTests.cs')
if($LASTEXITCODE -ne 0){throw 'Maintenance test build failed'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VisioFormula.exe.config') -Destination (Join-Path $PSScriptRoot 'MaintenanceTests.exe.config') -Force
& (Join-Path $PSScriptRoot 'MaintenanceTests.exe')
if($LASTEXITCODE -ne 0){throw ('Maintenance tests failed: '+$testRoot)}
& (Join-Path $PSScriptRoot 'MaintenanceTests.exe') --96
if($LASTEXITCODE -ne 0){throw ('Maintenance 96 DPI tests failed: '+$testRoot)}
if(!$WithoutVisio){
    & (Join-Path $PSScriptRoot 'MaintenanceTests.exe') --visio
    if($LASTEXITCODE -ne 0){throw ('Automatic update tests failed: '+$testRoot)}
}
Write-Output ('RESULTS: '+$testRoot)