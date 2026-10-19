# ASCII-only for Windows PowerShell 5.1 compatibility.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$WindowsRoot = Split-Path -Parent $PSScriptRoot
function Assert-WindowsHost {
    if ([Environment]::OSVersion.Platform -ne 'Win32NT') { throw 'WinUI/MSI builds require Windows 11. Run BUILD-WINDOWS.cmd on Windows.' }
    if (-not [Environment]::Is64BitProcess) { throw 'Use 64-bit PowerShell and the x64 .NET SDK.' }
}
function Get-DotnetPath {
    if ($env:PTA_DOTNET_EXE -and [IO.File]::Exists($env:PTA_DOTNET_EXE)) { return $env:PTA_DOTNET_EXE }
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $command) { throw 'SDK not found. Use BUILD-WINDOWS.cmd to download the official SDK locally.' }
    return $command.Source
}
function ConvertTo-NativeArgument {
    param([AllowEmptyString()][string]$Value)
    if ($Value.Length -eq 0) { return '""' }
    if ($Value -notmatch '[\s"]') { return $Value }
    $escaped = $Value -replace '(\\*)"', '$1$1\"'
    $escaped = $escaped -replace '(\\+)$', '$1$1'
    return '"' + $escaped + '"'
}
function Invoke-Dotnet {
    param([string[]]$Arguments)
    $executable = Get-DotnetPath
    $logDirectory = $env:PTA_BUILD_LOG_DIR
    if (-not $logDirectory) { $logDirectory = [IO.Path]::Combine($WindowsRoot, 'build-logs') }
    [void][IO.Directory]::CreateDirectory($logDirectory)
    $stage = [regex]::Replace($Arguments[0], '[^A-Za-z0-9_-]', '_')
    $prefix = [IO.Path]::Combine($logDirectory, ([DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff') + '-' + $stage))
    if ($stage -in @('build','test','publish')) { $Arguments += '-tl:off' }
    if ($stage -eq 'publish') {
        $Arguments += @("-bl:$prefix.binlog", "-flp:logfile=$prefix.msbuild.log;verbosity=diagnostic;encoding=UTF-8")
    }
    $encoding = [Text.UTF8Encoding]::new($false)
    $stdoutWriter = [IO.StreamWriter]::new("$prefix.stdout.log", $false, $encoding)
    $stderrWriter = [IO.StreamWriter]::new("$prefix.stderr.log", $false, $encoding)
    $stdoutWriter.AutoFlush = $true
    $stderrWriter.AutoFlush = $true
    $process = [Diagnostics.Process]::new()
    try {
        $process.StartInfo.FileName = $executable
        $quoted = foreach ($argument in $Arguments) { ConvertTo-NativeArgument $argument }
        $process.StartInfo.Arguments = $quoted -join ' '
        $process.StartInfo.WorkingDirectory = $WindowsRoot
        $process.StartInfo.UseShellExecute = $false
        $process.StartInfo.CreateNoWindow = $true
        $process.StartInfo.RedirectStandardOutput = $true
        $process.StartInfo.RedirectStandardError = $true
        $process.StartInfo.StandardOutputEncoding = $encoding
        $process.StartInfo.StandardErrorEncoding = $encoding
        $process.StartInfo.EnvironmentVariables['DOTNET_CLI_UI_LANGUAGE'] = 'en-US'
        $process.StartInfo.EnvironmentVariables['DOTNET_NOLOGO'] = '1'
        Write-Host "Stage: $stage; native logs: $prefix.*.log"
        if (-not $process.Start()) { throw 'Failed to start dotnet.' }
        # Read both streams concurrently; PowerShell transcripts alone can lose native output.
        $stdoutRead = $process.StandardOutput.ReadLineAsync()
        $stderrRead = $process.StandardError.ReadLineAsync()
        while ($null -ne $stdoutRead -or $null -ne $stderrRead) {
            $progressed = $false
            if ($null -ne $stdoutRead -and $stdoutRead.IsCompleted) {
                $line = $stdoutRead.GetAwaiter().GetResult()
                if ($null -eq $line) { $stdoutRead = $null }
                else {
                    $stdoutWriter.WriteLine($line)
                    Write-Host $line
                    $stdoutRead = $process.StandardOutput.ReadLineAsync()
                }
                $progressed = $true
            }
            if ($null -ne $stderrRead -and $stderrRead.IsCompleted) {
                $line = $stderrRead.GetAwaiter().GetResult()
                if ($null -eq $line) { $stderrRead = $null }
                else {
                    $stderrWriter.WriteLine($line)
                    Write-Host $line -ForegroundColor Yellow
                    $stderrRead = $process.StandardError.ReadLineAsync()
                }
                $progressed = $true
            }
            if (-not $progressed) { [Threading.Thread]::Sleep(20) }
        }
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "dotnet $stage failed (exit $($process.ExitCode)). Full output: $prefix.stdout.log and $prefix.stderr.log" }
    } finally {
        $stdoutWriter.Dispose()
        $stderrWriter.Dispose()
        $process.Dispose()
    }
}
function Get-PublishDirectory { return (Join-Path $WindowsRoot 'publish\win-x64') }
