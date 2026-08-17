param(
    [Parameter(Mandatory)] [string] $Rid,
    [Parameter(Mandatory)] [string] $PublishDirectory,
    [Parameter(Mandatory)] [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$publish = (Resolve-Path $PublishDirectory).Path
$output = New-Item -ItemType Directory -Force -Path $OutputDirectory
$zip = Join-Path $output "CineSched-$Rid.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal
