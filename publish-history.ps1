$ErrorActionPreference = 'Stop'
$repo = $env:GITHUB_REPOSITORY
$items = Get-Content "$PSScriptRoot/release-history.json" -Raw | ConvertFrom-Json
foreach ($item in $items) {
    $tag = 'v' + $item.version
    $existing = & gh release view $tag --repo $repo --json isDraft 2>$null
    if ($LASTEXITCODE -eq 0) { throw "Release already exists: $tag. Inspect before retrying." }
    $work = Join-Path $env:RUNNER_TEMP ('LightDock-' + $item.version)
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    git archive --format=zip --output="$work/source.zip" $item.sha
    if ($LASTEXITCODE -ne 0) { throw 'Source export failed' }
    Expand-Archive -LiteralPath "$work/source.zip" -DestinationPath "$work/source"
    & "$work/source/build.ps1"
    $name = "LightDock-v$($item.version)-windows-x64-unsigned"
    $package = "$work/$name"
    New-Item -ItemType Directory -Path "$package/bin" -Force | Out-Null
    Copy-Item "$work/source/dist/*.exe*" "$package/bin"
    Copy-Item "$PSScriptRoot/release-notes/$tag.md" "$package/README.md"
    if ($item.version -eq '0.3.0') {
        Copy-Item "$PSScriptRoot/README.md","$PSScriptRoot/README.en.md","$PSScriptRoot/QUICKSTART.en.md" $package
    }
    Get-ChildItem $package -File -Recurse | ForEach-Object {
        "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash, $_.FullName.Substring($package.Length+1)
    } | Set-Content "$package/SHA256SUMS.txt" -Encoding ascii
    $zip = "$work/$name.zip"
    Compress-Archive -LiteralPath $package -DestinationPath $zip
    "{0}  {1}.zip" -f (Get-FileHash $zip -Algorithm SHA256).Hash,$name | Set-Content "$zip.sha256" -Encoding ascii
    $flags = @('--latest=false')
    if ($item.version -like '*beta*') { $flags += '--prerelease' }
    if ($item.version -eq '0.3.0') { $flags = @('--latest') }
    gh release create $tag $zip "$zip.sha256" --repo $repo --target $item.sha --title "Light Dock $tag" --notes-file "$PSScriptRoot/release-notes/$tag.md" @flags
    if ($LASTEXITCODE -ne 0) { throw "Release failed: $tag" }
}
