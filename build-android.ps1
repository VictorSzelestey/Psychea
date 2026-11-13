param([string]$EnvFile = (Join-Path $PSScriptRoot '.env'))

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $EnvFile -PathType Leaf)) {
    throw 'Create .env from .env.example and fill in the APK build settings.'
}

# Read data only: never execute the contents of .env as PowerShell.
$settings = @{}
foreach ($line in Get-Content -LiteralPath $EnvFile -Encoding UTF8) {
    if ($line -match '^\s*(#|$)') { continue }
    if ($line -notmatch '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=(.*)$') {
        throw 'Invalid .env line; expected NAME=value.'
    }
    $name = $matches[1]
    $value = $matches[2].Trim()
    if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or
        ($value.StartsWith("'") -and $value.EndsWith("'")))) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    $settings[$name] = $value
}

$required = @('DotnetExecutable', 'AndroidSdkDirectory', 'JavaSdkDirectory', 'AndroidOutputDirectory',
    'AndroidSigningKeyStore', 'AndroidSigningKeyAlias', 'AndroidSigningKeyPass', 'AndroidSigningStorePass')
foreach ($name in $required) {
    if ([string]::IsNullOrWhiteSpace($settings[$name])) { throw "Missing $name in .env." }
}
foreach ($name in @('DotnetExecutable', 'AndroidSdkDirectory', 'JavaSdkDirectory', 'AndroidSigningKeyStore')) {
    $value = $settings[$name]
    if (-not [IO.Path]::IsPathRooted($value)) { $value = Join-Path $PSScriptRoot $value }
    if (-not (Test-Path -LiteralPath $value)) { throw "Path for $name does not exist." }
    $settings[$name] = [IO.Path]::GetFullPath($value)
}
$outputDirectory = $settings.AndroidOutputDirectory
if (-not [IO.Path]::IsPathRooted($outputDirectory)) { $outputDirectory = Join-Path $PSScriptRoot $outputDirectory }
$outputDirectory = [IO.Path]::GetFullPath($outputDirectory).TrimEnd('\', '/') + '/'
$publishDirectory = $outputDirectory + 'publish/'

$previousEnvironment = @{}
foreach ($name in @('AndroidSigningKeyPass', 'AndroidSigningStorePass', 'JAVA_HOME', 'ANDROID_HOME', 'DOTNET_ROOT')) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:AndroidSigningKeyPass = $settings.AndroidSigningKeyPass
    $env:AndroidSigningStorePass = $settings.AndroidSigningStorePass
    $env:JAVA_HOME = $settings.JavaSdkDirectory
    $env:ANDROID_HOME = $settings.AndroidSdkDirectory
    $env:DOTNET_ROOT = Split-Path $settings.DotnetExecutable
    $arguments = @('publish', (Join-Path $PSScriptRoot 'PsyPath/PsyPath.csproj'),
        '-f', 'net9.0-android', '-c', 'Release', '-p:AndroidPackageFormats=apk',
        '-p:AndroidKeyStore=true', "-p:AndroidSigningKeyStore=$($settings.AndroidSigningKeyStore)",
        "-p:AndroidSigningKeyAlias=$($settings.AndroidSigningKeyAlias)",
        '-p:AndroidSigningKeyPass=env:AndroidSigningKeyPass',
        '-p:AndroidSigningStorePass=env:AndroidSigningStorePass',
        "-p:OutputPath=$outputDirectory", "-p:PublishDir=$publishDirectory",
        "-p:AndroidSdkDirectory=$($settings.AndroidSdkDirectory)",
        "-p:JavaSdkDirectory=$($settings.JavaSdkDirectory)")
    & $settings.DotnetExecutable @arguments
    if ($LASTEXITCODE -ne 0) { throw "Android publish failed (exit $LASTEXITCODE)." }
    $apk = Join-Path $publishDirectory 'com.psypath.app-Signed.apk'
    if (-not (Test-Path -LiteralPath $apk)) { throw 'Signed APK was not produced.' }
    Write-Output "APK: $apk"
} finally {
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
}
