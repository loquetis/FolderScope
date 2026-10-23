# Build FolderScope tests and a self-contained single-file Windows x64 exe.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Out = if ($args.Count -ge 1) { $args[0] } else { Join-Path $Root "artifacts\win-x64" }

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

if (Test-Path $Out) {
    Remove-Item -Recurse -Force $Out
}
New-Item -ItemType Directory -Path $Out | Out-Null

dotnet test (Join-Path $Root "tests\FolderScope.Core.Tests\FolderScope.Core.Tests.csproj") -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish (Join-Path $Root "src\FolderScope.App\FolderScope.App.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:IncludeSymbols=false `
    -o $Out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $Out "FolderScope.exe"
if (-not (Test-Path $exe)) {
    throw "FolderScope.exe was not produced."
}

$extras = Get-ChildItem -Force $Out | Where-Object { $_.Name -ne "FolderScope.exe" }
if ($extras) {
    $extras | ForEach-Object { Write-Error $_ }
    throw "Publish output is not a single file."
}

$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
$bytes = (Get-Item $exe).Length
Write-Host "FolderScope.exe  $bytes bytes  sha256 $hash"
Write-Host $exe
