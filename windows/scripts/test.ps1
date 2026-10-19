param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
. "$PSScriptRoot\common.ps1"
Assert-WindowsHost
Push-Location $WindowsRoot
try {
    foreach ($name in @('Core','Windows')) {
        Invoke-Dotnet -Arguments @('test',"tests\PTA.CodeTyper.$name.Tests\PTA.CodeTyper.$name.Tests.csproj",'-c',$Configuration,'-p:Platform=x64','--logger','trx','-v:minimal')
    }
} finally { Pop-Location }
