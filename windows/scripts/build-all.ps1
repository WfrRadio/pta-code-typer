# Downloads tools only; does not install the app or change system settings.
param()
. "$PSScriptRoot\common.ps1"
Assert-WindowsHost
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$logs = Join-Path $WindowsRoot 'build-logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$log = Join-Path $logs ('build-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
Start-Transcript -Path $log | Out-Null
$env:PTA_BUILD_LOG_DIR = Join-Path $logs ('session-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path $env:PTA_BUILD_LOG_DIR | Out-Null
$exitCode = 0
try {
    $tools = Join-Path $WindowsRoot '.tools'
    $dotnetDirectory = Join-Path $tools 'dotnet'
    $localDotnet = Join-Path $dotnetDirectory 'dotnet.exe'
    $candidate = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($candidate) {
        $sdks = & $candidate.Source --list-sdks
        if ($LASTEXITCODE -eq 0 -and ($sdks -match '^8\.0\.(4\d\d) ')) { $env:PTA_DOTNET_EXE = $candidate.Source }
    }
    if (-not $env:PTA_DOTNET_EXE) {
        if (-not (Test-Path -LiteralPath $localDotnet)) {
            New-Item -ItemType Directory -Force -Path $dotnetDirectory | Out-Null
            $installScript = Join-Path $tools 'dotnet-install.ps1'
            Write-Host 'Downloading the official Microsoft .NET 8 SDK (x64)...'
            Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installScript
            & $installScript -Version '8.0.426' -Architecture x64 -InstallDir $dotnetDirectory -NoPath
            if (-not (Test-Path -LiteralPath $localDotnet)) { throw 'Local SDK installation failed.' }
        }
        $env:PTA_DOTNET_EXE = $localDotnet
    }
    $env:DOTNET_ROOT = Split-Path -Parent $env:PTA_DOTNET_EXE
    $env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    Write-Host "Using SDK: $env:PTA_DOTNET_EXE"
    & "$PSScriptRoot\test.ps1" -Configuration Release
    & "$PSScriptRoot\publish.ps1" -Configuration Release
    & "$PSScriptRoot\package.ps1" -Version '0.1.2'
    Write-Host 'SUCCESS. artifacts\ contains MSI + portable ZIP; publish\win-x64\ contains EXE + dependencies.' -ForegroundColor Green
    Write-Host 'Keep all portable files together. Do not copy the EXE alone.'
} catch {
    $exitCode = 1
    Write-Host ($_ | Out-String) -ForegroundColor Red
    Write-Host 'Build stopped. A failed step is not reported as success.' -ForegroundColor Red
} finally {
    Stop-Transcript | Out-Null
    Write-Host "Build log: $log"
    Write-Host "Native compiler logs: $env:PTA_BUILD_LOG_DIR"
}
exit $exitCode
