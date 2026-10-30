# Thunderstore package: CustomAchievements.dll + manifest + README + CHANGELOG + icon

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$ModName = 'CustomAchievements'
$Dll = Join-Path $Root "bin\Release\net472\$ModName.dll"
$Zip = Join-Path $Root "bin\Release\$ModName.zip"
$Icon = Join-Path $Root 'icon.png'

if (-not (Test-Path $Icon)) {
    Write-Host "icon.png (256x256) is missing; Thunderstore requires it." -ForegroundColor Red
    exit 1
}

Write-Host "Building $ModName..." -ForegroundColor Cyan
dotnet build (Join-Path $Root "$ModName.csproj") -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$Temp = Join-Path $Root 'thunderstore_package'
if (Test-Path $Temp) { Remove-Item $Temp -Recurse -Force }
New-Item -ItemType Directory -Path $Temp | Out-Null

Copy-Item (Join-Path $Root 'manifest.json') $Temp
Copy-Item (Join-Path $Root 'README.md') $Temp
Copy-Item (Join-Path $Root 'CHANGELOG.md') $Temp
Copy-Item $Icon $Temp
Copy-Item $Dll $Temp

if (Test-Path $Zip) { Remove-Item $Zip -Force }
Compress-Archive -Path (Join-Path $Temp '*') -DestinationPath $Zip -Force
Remove-Item $Temp -Recurse -Force
Write-Host "Package created: $Zip" -ForegroundColor Green
