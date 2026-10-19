param([ValidateSet('Debug','Release')][string]$Configuration = 'Release', [ValidateSet('x64')][string]$Architecture = 'x64')
. "$PSScriptRoot\common.ps1"
Assert-WindowsHost
Push-Location $WindowsRoot
try {
    Invoke-Dotnet -Arguments @('build','PTA.CodeTyper.sln','-c',$Configuration,'-p:Platform=x64','-v:minimal')
} finally { Pop-Location }
