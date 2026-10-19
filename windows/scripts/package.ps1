param([ValidateSet('x64')][string]$Architecture = 'x64', [string]$Version = '0.1.2')
. "$PSScriptRoot\common.ps1"
Assert-WindowsHost
& "$PSScriptRoot\verify-package.ps1"
if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)$') { throw 'MSI version must be X.Y.Z.' }
if ([int]$Matches[1] -gt 255 -or [int]$Matches[2] -gt 255 -or [int]$Matches[3] -gt 65535) { throw 'Version exceeds MSI limits.' }
$publishDirectory = Get-PublishDirectory
$outputDirectory = Join-Path $WindowsRoot 'artifacts'
$generatedDirectory = Join-Path $WindowsRoot 'installer\generated'
New-Item -ItemType Directory -Force -Path $outputDirectory,$generatedDirectory | Out-Null
$toolDirectory = Join-Path $WindowsRoot '.tools\wix'
$wix = Join-Path $toolDirectory 'wix.exe'
$wixVersion = '5.0.2'
Push-Location $WindowsRoot
try {
    if (-not (Test-Path -LiteralPath $wix)) {
        New-Item -ItemType Directory -Force -Path $toolDirectory | Out-Null
        Invoke-Dotnet -Arguments @('tool','install','wix','--version',$wixVersion,'--tool-path',$toolDirectory)
    }
    $actualVersion = & $wix --version
    if ($LASTEXITCODE -ne 0 -or "$actualVersion" -notlike "$wixVersion*") { throw "Expected WiX $wixVersion, got: $actualVersion" }
    # Keep extension and compiler versions matched. Use the project-local extension cache.
    $uiExtension = "WixToolset.UI.wixext/$wixVersion"
    & $wix extension add $uiExtension
    if ($LASTEXITCODE -ne 0) { throw "Could not restore UI extension $uiExtension" }
    $xml = New-Object System.Xml.XmlDocument
    $ns = 'http://wixtoolset.org/schemas/v4/wxs'
    $root = $xml.CreateElement('Wix',$ns); [void]$xml.AppendChild($root)
    $fragment = $xml.CreateElement('Fragment',$ns); [void]$root.AppendChild($fragment)
    $group = $xml.CreateElement('ComponentGroup',$ns); $group.SetAttribute('Id','ProductComponents'); [void]$fragment.AppendChild($group)
    $directories = @{}
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        foreach ($file in Get-ChildItem -LiteralPath $publishDirectory -File -Recurse | Sort-Object FullName) {
            if ($file.Extension -eq '.pdb') { continue }
            $relative = $file.FullName.Substring($publishDirectory.Length).TrimStart([char]'\')
            $parts = $relative -split '\\'
            $directoryId = 'INSTALLFOLDER'
            $subpath = ''
            for ($i = 0; $i -lt $parts.Length - 1; $i++) {
                $subpath += '\' + $parts[$i]
                if (-not $directories.ContainsKey($subpath)) {
                    $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($subpath.ToLowerInvariant()))
                    $id = 'D_' + ([BitConverter]::ToString($bytes).Replace('-','').Substring(0,28))
                    $dir = $xml.CreateElement('Directory',$ns); $dir.SetAttribute('Id',$id); $dir.SetAttribute('Name',$parts[$i])
                    $reference = $xml.CreateElement('DirectoryRef',$ns); $reference.SetAttribute('Id',$directoryId)
                    [void]$reference.AppendChild($dir); [void]$fragment.AppendChild($reference)
                    $directories[$subpath] = $id
                }
                $directoryId = $directories[$subpath]
            }
            $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes('PTA Code Typer|x64|' + $relative.ToLowerInvariant()))
            $suffix = [BitConverter]::ToString($bytes).Replace('-','').Substring(0,28)
            $guidBytes = New-Object byte[] 16; [Array]::Copy($bytes,$guidBytes,16)
            $guid = New-Object System.Guid -ArgumentList (,$guidBytes)
            $component = $xml.CreateElement('Component',$ns)
            $component.SetAttribute('Id','C_' + $suffix); $component.SetAttribute('Guid',$guid.ToString()); $component.SetAttribute('Directory',$directoryId)
            $payloadFile = $xml.CreateElement('File',$ns)
            $payloadFile.SetAttribute('Id','F_' + $suffix); $payloadFile.SetAttribute('Source',$file.FullName)
            [void]$component.AppendChild($payloadFile)
            $key = $xml.CreateElement('RegistryValue',$ns)
            $key.SetAttribute('Root','HKCU'); $key.SetAttribute('Key','Software\PTA Code Typer\Installer\Files')
            $key.SetAttribute('Name',$suffix); $key.SetAttribute('Type','integer'); $key.SetAttribute('Value','1'); $key.SetAttribute('KeyPath','yes')
            [void]$component.AppendChild($key)
            $remove = $xml.CreateElement('RemoveFolder',$ns)
            $remove.SetAttribute('Id','R_' + $suffix); $remove.SetAttribute('On','uninstall')
            [void]$component.AppendChild($remove); [void]$group.AppendChild($component)
        }
    } finally { $sha.Dispose() }
    $generatedFile = Join-Path $generatedDirectory 'Payload.wxs'
    $xml.Save($generatedFile)
    $msi = Join-Path $outputDirectory "PTA-Code-Typer-$Version-windows-x64.msi"
    if (Test-Path -LiteralPath $msi) { Remove-Item -LiteralPath $msi -Force }
    & $wix build (Join-Path $WindowsRoot 'installer\Product.wxs') $generatedFile -arch x64 -d "ProductVersion=$Version" -d "LicenseRtf=$(Join-Path $WindowsRoot 'installer\License.rtf')" -ext $uiExtension -culture zh-CN -o $msi
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $msi)) { throw 'WiX failed to produce the MSI.' }
    if ((Get-Item -LiteralPath $msi).Length -lt 1024) { throw 'MSI output is unexpectedly small.' }
    $portable = Join-Path $outputDirectory "PTA-Code-Typer-$Version-windows-x64-portable.zip"
    if (Test-Path -LiteralPath $portable) { Remove-Item -LiteralPath $portable -Force }
    Compress-Archive -Path "$publishDirectory\*" -DestinationPath $portable -CompressionLevel Optimal
    $sums = foreach ($path in @($msi,$portable)) {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant(),(Split-Path $path -Leaf)
    }
    [IO.File]::WriteAllLines((Join-Path $outputDirectory 'SHA256SUMS.txt'),$sums,[Text.Encoding]::ASCII)
    Write-Host "Created MSI and portable ZIP: $outputDirectory" -ForegroundColor Green
} finally { Pop-Location }
