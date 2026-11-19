# Stages a release of DDLC Plus Access.
#
#   1. Set the version in Properties/AssemblyInfo.cs (MelonInfo) and add a "## X.Y.Z" section
#      to CHANGELOG.md.
#   2. .\tools\release.ps1        (builds Release, checks the strings, restages dist/payload)
#   3. git commit -am "Release X.Y.Z"; git tag -a vX.Y.Z -m vX.Y.Z; git push --follow-tags
#
# GitHub Actions cannot build the mod: it references the game's own assemblies, which are not
# redistributable. So the built mod is committed in dist/payload, laid out like the game folder;
# the release workflow adds the official MelonLoader and zips it.
#
# Optional: -GamePath "<DDLC Plus folder>" when the game is not in a default Steam location.

param([string]$GamePath = "")

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

$info = Get-Content "Properties/AssemblyInfo.cs" -Raw
if ($info -notmatch '"DDLC Plus Access",\s*"([0-9.]+)"') { throw "No MelonInfo version in Properties/AssemblyInfo.cs" }
$version = $Matches[1]
Write-Host "Releasing DDLC Plus Access $version"

$changelog = Get-Content "CHANGELOG.md" -Raw
if ($changelog -notmatch "(?m)^## $([regex]::Escape($version))\s*$") { throw "CHANGELOG.md has no '## $version' section." }

# Every language file must match English key for key.
$en = (Get-Content "Localization/en-us.txt" -Encoding utf8 | Where-Object { $_ -match '^\s*[^#\s][^=]*=' } | ForEach-Object { ($_ -split '=', 2)[0].Trim() }) | Sort-Object
foreach ($file in Get-ChildItem "Localization/*.txt") {
    $keys = (Get-Content $file.FullName -Encoding utf8 | Where-Object { $_ -match '^\s*[^#\s][^=]*=' } | ForEach-Object { ($_ -split '=', 2)[0].Trim() }) | Sort-Object
    if (Compare-Object $en $keys) { throw "$($file.Name) does not have the same keys as en-us.txt" }
}

$buildArgs = @("build", "-c", "Release", "-nologo", "-v", "q")
if ($GamePath) { $buildArgs += "-p:GamePath=$GamePath" }
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$out = Split-Path -Parent (Get-ChildItem "bin/Release" -Recurse -Filter "DDLCPlusAccess.dll" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
$payload = "dist/payload"
if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
New-Item -ItemType Directory -Force "$payload/Mods/DDLCPlusAccess" | Out-Null

Copy-Item "$out/DDLCPlusAccess.dll" "$payload/Mods/"
Copy-Item "$out/UnityAccessibilityLib.dll" "$payload/"
Copy-Item "lib/prism/prism.dll" "$payload/Mods/DDLCPlusAccess/"
Copy-Item "lib/prism/third-party" "$payload/Mods/DDLCPlusAccess/" -Recurse
Copy-Item "README.md", "CHANGELOG.md", "LICENSE" "$payload/Mods/DDLCPlusAccess/"

# The DLL must carry the version being released.
$bytes = [IO.File]::ReadAllBytes((Resolve-Path "$payload/Mods/DDLCPlusAccess.dll"))
if (-not ([Text.Encoding]::Unicode.GetString($bytes) + [Text.Encoding]::UTF8.GetString($bytes)).Contains($version)) {
    throw "Built DLL does not contain version $version"
}

Write-Host "Staged ${payload}:"
Get-ChildItem $payload -Recurse -File | ForEach-Object { "  " + $_.FullName.Substring((Resolve-Path $payload).Path.Length + 1) } | Where-Object { $_ -notmatch "third-party" }
Write-Host "Next: commit, then tag v$version and push with --follow-tags."
