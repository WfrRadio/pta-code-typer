param([ValidateSet('x64')][string]$Architecture = 'x64')
. "$PSScriptRoot\common.ps1"
$publishDirectory = Get-PublishDirectory
foreach ($name in @('PTA Code Typer.exe','PTA Code Typer.dll','PTA Code Typer.runtimeconfig.json','PTA Code Typer.deps.json','PTA.CodeTyper.Core.dll','PTA.CodeTyper.Windows.dll','Microsoft.UI.Xaml.dll','Microsoft.WindowsAppRuntime.dll','coreclr.dll','hostfxr.dll','System.Private.CoreLib.dll')) {
    $file = Join-Path $publishDirectory $name
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Publish output missing: $name" }
    if ((Get-Item -LiteralPath $file).Length -eq 0) { throw "Empty publish file: $name" }
}
$config = Get-Content -LiteralPath (Join-Path $publishDirectory 'PTA Code Typer.runtimeconfig.json') -Raw | ConvertFrom-Json
if ($config.runtimeOptions.PSObject.Properties.Name -contains 'frameworks' -or $config.runtimeOptions.PSObject.Properties.Name -contains 'framework') { throw 'Publish is not self-contained.' }
foreach ($name in @('data','.venv','.git','reports','tests')) {
    if (Test-Path -LiteralPath (Join-Path $publishDirectory $name)) { throw "Unexpected private/development folder: $name" }
}
Write-Host 'Publish files verified. Browser/UI testing is a separate Windows check.' -ForegroundColor Green
