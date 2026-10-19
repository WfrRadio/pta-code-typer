param([ValidateSet('Debug','Release')][string]$Configuration = 'Release', [ValidateSet('x64')][string]$Architecture = 'x64')
. "$PSScriptRoot\common.ps1"
Assert-WindowsHost
$publishDirectory = Get-PublishDirectory
Push-Location $WindowsRoot
try {
    if (Test-Path -LiteralPath $publishDirectory) { Remove-Item -LiteralPath $publishDirectory -Recurse -Force }
    Invoke-Dotnet -Arguments @('publish','src\PTA.CodeTyper.App\PTA.CodeTyper.App.csproj','-c',$Configuration,'-r','win-x64','-p:Platform=x64','--self-contained','true','-o',$publishDirectory,'-p:WindowsPackageType=None','-p:WindowsAppSDKSelfContained=true','-p:PublishSingleFile=false','-p:PublishTrimmed=false','-v:minimal')
    $license = Join-Path (Split-Path -Parent $WindowsRoot) 'LICENSE'
    if (Test-Path -LiteralPath $license) { Copy-Item -LiteralPath $license -Destination (Join-Path $publishDirectory 'LICENSE.txt') }
    Copy-Item -LiteralPath (Join-Path $WindowsRoot 'docs\THIRD_PARTY_NOTICES_WINDOWS.md') -Destination $publishDirectory
    & "$PSScriptRoot\verify-package.ps1"
    Write-Host "Published EXE and dependencies: $publishDirectory" -ForegroundColor Green
} finally { Pop-Location }
