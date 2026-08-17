param(
    [Parameter(Mandatory)] [string] $Rid,
    [Parameter(Mandatory)] [string] $PublishDirectory,
    [Parameter(Mandatory)] [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$publish = (Resolve-Path $PublishDirectory).Path
$output = New-Item -ItemType Directory -Force -Path $OutputDirectory
$app = Join-Path $output "CineSched-$Rid.app"
if (Test-Path -LiteralPath $app) { Remove-Item -LiteralPath $app -Recurse -Force }
$macOs = New-Item -ItemType Directory -Force -Path (Join-Path $app 'Contents/MacOS')
$resources = New-Item -ItemType Directory -Force -Path (Join-Path $app 'Contents/Resources')
Copy-Item -Path (Join-Path $publish '*') -Destination $macOs -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../src/CineSched.App/Assets/Icons/icon.svg') -Destination $resources
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Info.plist') -Destination (Join-Path $app 'Contents/Info.plist')
& chmod +x (Join-Path $macOs 'CineSched.App')
$zip = Join-Path $output "CineSched-$Rid.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
& ditto -c -k --sequesterRsrc --keepParent $app $zip
