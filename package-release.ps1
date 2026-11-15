param([string]$Version = '0.3.0')
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'dist\LightDock.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Run build.ps1 first.' }
if ((Get-Item -LiteralPath $exe).VersionInfo.ProductVersion -ne $Version) { throw 'Binary version does not match package version.' }
if ((Get-AuthenticodeSignature -LiteralPath $exe).Status -ne 'NotSigned') { throw 'This packaging script is for the unsigned release only.' }
$releaseRoot = Join-Path $PSScriptRoot 'releases'
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
$name = "LightDock-v$Version-windows-x64-unsigned"
$stage = Join-Path $releaseRoot ('.stage-' + [Guid]::NewGuid().ToString('N'))
$package = Join-Path $stage $name
New-Item -ItemType Directory -Path (Join-Path $package 'bin') -Force | Out-Null
# Explicit allowlist prevents personal settings, recovery journals and test logs from shipping.
Copy-Item -LiteralPath $exe -Destination (Join-Path $package 'bin\LightDock.exe')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'dist\LightDock.exe.config') -Destination (Join-Path $package 'bin\LightDock.exe.config')
foreach ($file in @('README.md','CHANGELOG.md','QUICKSTART.en.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $package
}
Get-ChildItem -LiteralPath $package -Recurse -File | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
    $relative = $_.FullName.Substring($package.Length + 1)
    "$($hash.Hash)  $relative"
} | Set-Content -LiteralPath (Join-Path $package 'SHA256SUMS.txt') -Encoding ascii
$zip = Join-Path $releaseRoot ($name + '.zip')
Compress-Archive -LiteralPath $package -DestinationPath $zip -Force
$zipHash = Get-FileHash -LiteralPath $zip -Algorithm SHA256
"$($zipHash.Hash)  $name.zip" | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ascii
Write-Output $zip
Write-Output $zipHash.Hash
