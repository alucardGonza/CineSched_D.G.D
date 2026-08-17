param(
    [Parameter(Mandatory)] [string] $Rid,
    [Parameter(Mandatory)] [string] $PublishDirectory,
    [Parameter(Mandatory)] [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$publish = (Resolve-Path $PublishDirectory).Path
$output = New-Item -ItemType Directory -Force -Path $OutputDirectory
$stage = Join-Path $output "CineSched-$Rid.AppDir"
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'usr/bin') | Out-Null
Copy-Item -Path (Join-Path $publish '*') -Destination (Join-Path $stage 'usr/bin') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AppDir/AppRun') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AppDir/cinesched.desktop') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'src/CineSched.App/Assets/Icons/icon.svg') -Destination (Join-Path $stage 'cinesched.svg')
& chmod +x (Join-Path $stage 'AppRun') (Join-Path $stage 'usr/bin/CineSched.App')

$tarPath = Join-Path $output "CineSched-$Rid.tar.gz"
& tar -C $output -czf $tarPath (Split-Path $stage -Leaf)

$architecture = if ($Rid -eq 'linux-arm64') { 'aarch64' } else { 'x86_64' }
$tool = Join-Path $output "appimagetool-$architecture.AppImage"
Invoke-WebRequest "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-$architecture.AppImage" -OutFile $tool
& chmod +x $tool
$env:ARCH = $architecture
$env:APPIMAGE_EXTRACT_AND_RUN = '1'
& $tool $stage (Join-Path $output "CineSched-$Rid.AppImage")
