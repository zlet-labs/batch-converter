param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$portableRoot = Join-Path $repoRoot "artifacts\portable\win-x64"
$appFolder = Join-Path $portableRoot "ZletFolderConverter"
$projectPath = Join-Path $repoRoot "src\Zlet.FolderConverter.App\Zlet.FolderConverter.App.csproj"
$readmePath = Join-Path $repoRoot "README_PORTABLE.txt"
$licensePath = Join-Path $repoRoot "LICENSE"

function Fail($Message) {
    Write-Error $Message
    exit 1
}

$sdks = & dotnet --list-sdks
if ($LASTEXITCODE -ne 0) {
    Fail "Unable to query installed .NET SDKs."
}

if (-not ($sdks -match "^8\.")) {
    Fail ".NET 8 SDK is required to publish the portable package."
}

if (-not (Test-Path -LiteralPath $projectPath)) {
    Fail "Project file not found: $projectPath"
}

if (-not (Test-Path -LiteralPath $readmePath)) {
    Fail "README_PORTABLE.txt not found: $readmePath"
}

if (-not (Test-Path -LiteralPath $licensePath)) {
    Fail "LICENSE not found: $licensePath"
}

if (Test-Path -LiteralPath $portableRoot) {
    $resolvedPortableRoot = (Resolve-Path -LiteralPath $portableRoot).Path
    $expectedParent = (Join-Path $repoRoot "artifacts\portable")

    if (-not $resolvedPortableRoot.StartsWith($expectedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail "Refusing to clean unexpected path: $resolvedPortableRoot"
    }

    Remove-Item -LiteralPath $portableRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $appFolder | Out-Null

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $appFolder

if ($LASTEXITCODE -ne 0) {
    Fail "dotnet publish failed."
}

Copy-Item -LiteralPath $readmePath -Destination (Join-Path $appFolder "README_PORTABLE.txt") -Force
Copy-Item -LiteralPath $licensePath -Destination (Join-Path $appFolder "LICENSE.txt") -Force

$zipName = if ([string]::IsNullOrWhiteSpace($Version)) {
    "ZletFolderConverter-win-x64-portable.zip"
} else {
    "ZletFolderConverter-$Version-win-x64-portable.zip"
}

$zipPath = Join-Path $portableRoot $zipName
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -LiteralPath $appFolder -DestinationPath $zipPath -Force

$exePath = Join-Path $appFolder "ZletFolderConverter.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    Fail "Published EXE not found: $exePath"
}

if ((Get-Item -LiteralPath $exePath).Length -le 0) {
    Fail "Published EXE is empty: $exePath"
}

if (-not (Test-Path -LiteralPath $zipPath)) {
    Fail "Portable ZIP was not created."
}

if ((Get-Item -LiteralPath $zipPath).Length -le 0) {
    Fail "Portable ZIP is empty: $zipPath"
}

Write-Output "Portable ZIP created:"
Write-Output (Resolve-Path -LiteralPath $zipPath).Path
