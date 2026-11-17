param([string]$OutputDirectory = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs = @('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll','/r:Microsoft.CSharp.dll')
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output @refs ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')) ('/win32icon:'+(Join-Path $PSScriptRoot 'assets\formula.ico')) ('/out:' + (Join-Path $OutputDirectory 'VisioFormula.exe')) (Join-Path $PSScriptRoot 'src\Core.cs') (Join-Path $PSScriptRoot 'src\App.cs') (Join-Path $PSScriptRoot 'src\Theme.cs') (Join-Path $PSScriptRoot 'src\FontCatalog.cs') (Join-Path $PSScriptRoot 'src\Settings.cs') (Join-Path $PSScriptRoot 'src\PreviewDragFilter.cs') (Join-Path $PSScriptRoot 'src\PreviewHtml.cs') (Join-Path $PSScriptRoot 'src\RealtimeController.cs') (Join-Path $PSScriptRoot 'src\QuickInput.cs') (Join-Path $PSScriptRoot 'src\FormulaEditor.cs') (Join-Path $PSScriptRoot 'src\WorkspaceState.cs') (Join-Path $PSScriptRoot 'src\EditorDialogs.cs') (Join-Path $PSScriptRoot 'src\BatchStyle.cs') (Join-Path $PSScriptRoot 'src\CacheStore.cs') (Join-Path $PSScriptRoot 'src\EnvironmentCheck.cs') (Join-Path $PSScriptRoot 'src\UpdateBinding.cs') (Join-Path $PSScriptRoot 'src\MaintenanceDialogs.cs') (Join-Path $PSScriptRoot 'src\AppMaintenance.cs') (Join-Path $PSScriptRoot 'src\FormulaHost.cs') (Join-Path $PSScriptRoot 'src\PowerPointBridge.cs') (Join-Path $PSScriptRoot 'src\AppPowerPoint.cs')
if($LASTEXITCODE -ne 0){ throw 'Build failed' }
if([IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\') -ne [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets') -Destination $OutputDirectory -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VisioFormula.exe.config') -Destination $OutputDirectory -Force
}
