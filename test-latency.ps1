param([string]$ApplicationDirectory = $PSScriptRoot,[string]$Report = (Join-Path $PSScriptRoot 'verification/latency-results.json'))
$ErrorActionPreference='Stop'
$app = (Resolve-Path $ApplicationDirectory).Path
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /utf8output /target:exe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ('/r:'+(Join-Path $app 'VisioFormula.exe')) ('/win32manifest:'+(Join-Path $app 'app.manifest')) ('/out:'+(Join-Path $app 'LatencyBenchmark.exe')) (Join-Path $PSScriptRoot 'src\LatencyBenchmark.cs')
if($LASTEXITCODE -ne 0){throw 'Benchmark build failed'}
Copy-Item -LiteralPath (Join-Path $app 'VisioFormula.exe.config') -Destination (Join-Path $app 'LatencyBenchmark.exe.config') -Force
& (Join-Path $app 'LatencyBenchmark.exe') ([IO.Path]::GetFullPath($Report))
if($LASTEXITCODE -ne 0){throw 'Benchmark failed'}
