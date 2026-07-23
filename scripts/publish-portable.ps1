param(
    [string]$LibreOfficePath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "src\Zlet.FolderConverter.App\Zlet.FolderConverter.App.csproj"
$readmePath = Join-Path $repoRoot "README_PORTABLE.txt"
$licensePath = Join-Path $repoRoot "LICENSE"
$noticesPath = Join-Path $repoRoot "THIRD_PARTY_NOTICES.md"
$licensesDirectory = Join-Path $repoRoot "licenses"

function Fail([string]$Message) {
    Write-Error $Message
    exit 1
}

function Get-ProjectProperty([string]$Name) {
    $output = & dotnet msbuild $projectPath -nologo "-getProperty:$Name"
    if ($LASTEXITCODE -ne 0) {
        Fail "Unable to read MSBuild property '$Name'."
    }
    $value = $output | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    } | Select-Object -Last 1
    if ([string]::IsNullOrWhiteSpace($value)) {
        Fail "MSBuild property '$Name' is empty."
    }
    return $value.Trim()
}

function Resolve-LibreOfficeRuntime([string]$Candidate) {
    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        Fail "LibreOffice runtime path is required. Pass -LibreOfficePath <path>."
    }

    if (-not (Test-Path -LiteralPath $Candidate)) {
        Fail "LibreOffice runtime was not found at the supplied path."
    }

    $resolved = (Resolve-Path -LiteralPath $Candidate).Path
    if (Test-Path -LiteralPath $resolved -PathType Leaf) {
        if (-not ([System.IO.Path]::GetFileName($resolved).Equals("soffice.exe", [System.StringComparison]::OrdinalIgnoreCase))) {
            Fail "LibreOfficePath must point to soffice.exe or a LibreOffice runtime directory."
        }

        $programDirectory = Split-Path -Parent $resolved
        return [PSCustomObject]@{
            Root = Split-Path -Parent $programDirectory
            Executable = $resolved
        }
    }

    $directExecutable = Join-Path $resolved "soffice.exe"
    $programExecutable = Join-Path $resolved "program\soffice.exe"
    if (Test-Path -LiteralPath $programExecutable -PathType Leaf) {
        return [PSCustomObject]@{
            Root = $resolved
            Executable = $programExecutable
        }
    }

    if (Test-Path -LiteralPath $directExecutable -PathType Leaf) {
        return [PSCustomObject]@{
            Root = Split-Path -Parent $resolved
            Executable = $directExecutable
        }
    }

    Fail "LibreOffice soffice.exe was not found under the supplied runtime path."
}

function Assert-SafeArtifactPath([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $expectedRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts\portable"))
    if (-not $fullPath.StartsWith(
        $expectedRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail "Refusing to modify an unexpected artifact path."
    }
}

function Copy-PackageDocuments(
    [string]$RuntimeRoot,
    [string]$Destination
) {
    $documents = Get-ChildItem -LiteralPath $RuntimeRoot -Recurse -File |
        Where-Object {
            $_.Name -match "^(LICENSE|NOTICE|COPYING|CREDITS|README)(\.|_|-|$)"
        }
    $licenseDocuments = $documents |
        Where-Object { $_.Name -match "^(LICENSE|COPYING)(\.|_|-|$)" }
    if (-not $licenseDocuments) {
        Fail "No license document was found in the selected LibreOffice package. Packaging is blocked."
    }

    foreach ($document in $documents) {
        $relativePath = $document.FullName.Substring($RuntimeRoot.Length).TrimStart(
            [char[]]@("\", "/"))
        $destinationPath = Join-Path $Destination $relativePath
        $destinationDirectory = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
        Copy-Item -LiteralPath $document.FullName -Destination $destinationPath -Force
    }
}

$runtimeIdentifier = Get-ProjectProperty "ZletPortableRuntimeIdentifier"
$packageName = Get-ProjectProperty "ZletPortablePackageName"
$executableName = Get-ProjectProperty "ZletExecutableName"
$portableRoot = Join-Path $repoRoot "artifacts\portable\$runtimeIdentifier"
$appFolder = Join-Path $portableRoot $packageName

$sdks = & dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdks -match "^8\.")) {
    Fail ".NET 8 SDK is required to publish the portable package."
}

foreach ($requiredPath in @(
    $projectPath,
    $readmePath,
    $licensePath,
    $noticesPath,
    $licensesDirectory
)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        Fail "Required packaging input is missing."
    }
}

$runtime = Resolve-LibreOfficeRuntime $LibreOfficePath
$versionCommand = Join-Path (Split-Path -Parent $runtime.Executable) "soffice.com"
if (-not (Test-Path -LiteralPath $versionCommand -PathType Leaf)) {
    $versionCommand = $runtime.Executable
}

$libreOfficeVersion = (& $versionCommand --headless --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($libreOfficeVersion)) {
    Fail "Unable to determine the LibreOffice version from the selected package."
}

Assert-SafeArtifactPath $portableRoot
if (Test-Path -LiteralPath $portableRoot) {
    Remove-Item -LiteralPath $portableRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $appFolder | Out-Null

dotnet publish $projectPath `
    -c Release `
    -r $runtimeIdentifier `
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
Copy-Item -LiteralPath $noticesPath -Destination (Join-Path $appFolder "THIRD_PARTY_NOTICES.md") -Force
$packagedLicenses = Join-Path $appFolder "licenses"
New-Item -ItemType Directory -Force -Path $packagedLicenses | Out-Null
Copy-Item -Path (Join-Path $licensesDirectory "*") -Destination $packagedLicenses -Recurse -Force

$runtimeDestination = Join-Path $appFolder "runtime\libreoffice"
New-Item -ItemType Directory -Force -Path $runtimeDestination | Out-Null
Copy-Item -Path (Join-Path $runtime.Root "*") -Destination $runtimeDestination -Recurse -Force

$packageDocuments = Join-Path $appFolder "licenses\libreoffice\package-documents"
New-Item -ItemType Directory -Force -Path $packageDocuments | Out-Null
Copy-PackageDocuments $runtime.Root $packageDocuments

$versionFile = Join-Path $appFolder "licenses\libreoffice\VERSION.txt"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $versionFile) | Out-Null
Set-Content -LiteralPath $versionFile -Encoding UTF8 -Value @(
    "Bundled component: LibreOffice"
    "Version reported by the packaged runtime: $libreOfficeVersion"
    "Runtime source path is intentionally not recorded."
)

$sourceInfoFile = Join-Path $appFolder "licenses\libreoffice\SOURCE_INFO.txt"
Set-Content -LiteralPath $sourceInfoFile -Encoding UTF8 -Value @(
    "Corresponding LibreOffice source archives:"
    "https://www.libreoffice.org/download-other/"
    "https://download.documentfoundation.org/libreoffice/src/"
    ""
    "Use the exact version recorded in VERSION.txt and the instructions in the"
    "license documents copied from the selected runtime package."
)

$zipName = "$packageName.zip"
$zipPath = Join-Path $portableRoot $zipName

$requiredOutputs = @(
    (Join-Path $appFolder "$executableName.exe"),
    (Join-Path $runtimeDestination "program\soffice.exe"),
    (Join-Path $appFolder "licenses"),
    (Join-Path $appFolder "THIRD_PARTY_NOTICES.md"),
    (Join-Path $appFolder "README_PORTABLE.txt")
)
foreach ($requiredOutput in $requiredOutputs) {
    if (-not (Test-Path -LiteralPath $requiredOutput)) {
        Fail "Portable package validation failed: a required output is missing."
    }
}

$forbiddenDirectories = Get-ChildItem -LiteralPath $appFolder -Recurse -Directory |
    Where-Object {
        $_.Name -in @("bin", "obj", "fixtures", "test-fixtures") -and
        -not $_.FullName.StartsWith(
            $runtimeDestination + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase)
    }
if ($forbiddenDirectories) {
    Fail "Portable package validation failed: build or test directories are present."
}

$forbiddenFiles = Get-ChildItem -LiteralPath $appFolder -Recurse -File |
    Where-Object {
        $isLibreOfficeCertificateBundle =
            $_.Name.Equals("cacert.pem", [System.StringComparison]::OrdinalIgnoreCase) -and
            $_.FullName.StartsWith(
                $runtimeDestination + [System.IO.Path]::DirectorySeparatorChar,
                [System.StringComparison]::OrdinalIgnoreCase)
        -not $isLibreOfficeCertificateBundle -and (
            $_.Extension -in @(".cs", ".csproj", ".sln", ".pfx", ".pem", ".key") -or
            $_.Name -match "\.(user|suo)$"
        )
    }
if ($forbiddenFiles) {
    Fail "Portable package validation failed: source, test, or secret-bearing files are present."
}

$ownedTextFiles = @(
    (Join-Path $appFolder "README_PORTABLE.txt"),
    (Join-Path $appFolder "THIRD_PARTY_NOTICES.md"),
    (Join-Path $appFolder "licenses\README.md"),
    $versionFile,
    $sourceInfoFile
)
foreach ($textFile in $ownedTextFiles) {
    if (Test-Path -LiteralPath $textFile) {
        $content = Get-Content -Raw -LiteralPath $textFile
        if ($content.IndexOf($repoRoot, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $content.IndexOf($runtime.Root, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $content.IndexOf("ZLET_LIBREOFFICE_PATH", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Fail "Portable package validation failed: a local absolute path was recorded."
        }
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $appFolder,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $true
)
if (-not (Test-Path -LiteralPath $zipPath) -or
    (Get-Item -LiteralPath $zipPath).Length -le 0) {
    Fail "Portable ZIP was not created."
}

Write-Output "Portable ZIP created:"
Write-Output (Resolve-Path -LiteralPath $zipPath).Path
Write-Output "LibreOffice version:"
Write-Output $libreOfficeVersion
