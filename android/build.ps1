$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$jdk = if ($env:JAVA_HOME) { $env:JAVA_HOME } else { 'D:\Java\jdk-25' }
$tools = Join-Path $PSScriptRoot 'tools\build-tools\android-14'
$platform = Join-Path $PSScriptRoot 'tools\platform\android-34-ext12\android.jar'
$java = Join-Path $jdk 'bin\java.exe'
$javac = Join-Path $jdk 'bin\javac.exe'
$jar = Join-Path $jdk 'bin\jar.exe'
$keytool = Join-Path $jdk 'bin\keytool.exe'
function Check([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)" } }
New-Item -ItemType Directory -Path build\classes,build\dex,build\generated,signing -Force | Out-Null
& (Join-Path $tools 'aapt2.exe') compile --dir res -o build\resources.zip
Check 'Compile resources'
& (Join-Path $tools 'aapt2.exe') link -o build\unsigned.apk -I $platform --manifest AndroidManifest.xml --java build\generated build\resources.zip
Check 'Package resources'
$sources = @(Get-ChildItem src -Recurse -Filter *.java | ForEach-Object FullName)
& $javac --release 8 -encoding UTF-8 -classpath $platform -d build\classes @sources
Check 'Compile Java'
& $jar --create --file build\classes.jar -C build\classes .
Check 'Package classes'
& $java -cp (Join-Path $PSScriptRoot 'tools\r8.jar') com.android.tools.r8.D8 --lib $platform --min-api 26 --output build\dex build\classes.jar
Check 'Dex compilation'
& $jar --update --file build\unsigned.apk --no-manifest -C build\dex classes.dex
Check 'Embed dex'
& (Join-Path $tools 'zipalign.exe') -f 4 build\unsigned.apk build\aligned.apk
Check 'APK alignment'
$keystore = Join-Path $PSScriptRoot 'signing\vaultline-release.p12'
$passwordFile = Join-Path $PSScriptRoot 'signing\.store-password'
try {
    if (-not (Test-Path -LiteralPath $keystore)) {
        $random = [Security.Cryptography.RandomNumberGenerator]::Create()
        $bytes = [byte[]]::new(32)
        $random.GetBytes($bytes)
        $random.Dispose()
        $env:VAULTLINE_STORE_PASSWORD = [Convert]::ToBase64String($bytes)
        [IO.File]::WriteAllText($passwordFile, $env:VAULTLINE_STORE_PASSWORD)
        & $keytool -genkeypair -alias vaultline -keystore $keystore -storetype PKCS12 -storepass:env VAULTLINE_STORE_PASSWORD -keypass:env VAULTLINE_STORE_PASSWORD -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=Vaultline, OU=Personal Development'
        Check 'Create signing identity'
    } else {
        if (-not (Test-Path -LiteralPath $passwordFile)) { throw 'Keep the original signing password; do not replace the signing key.' }
        $env:VAULTLINE_STORE_PASSWORD = [IO.File]::ReadAllText($passwordFile)
    }
    $output = Join-Path (Split-Path $PSScriptRoot) 'release\Vaultline-1.0.0.apk'
    & $java -jar (Join-Path $tools 'lib\apksigner.jar') sign --ks $keystore --ks-key-alias vaultline --ks-pass env:VAULTLINE_STORE_PASSWORD --key-pass env:VAULTLINE_STORE_PASSWORD --min-sdk-version 26 --out $output build\aligned.apk
    Check 'Sign APK'
    & $java -jar (Join-Path $tools 'lib\apksigner.jar') verify --verbose --print-certs $output | Set-Content build\signature-verification.txt
    Check 'Verify signature'
    & (Join-Path $tools 'zipalign.exe') -c 4 $output
    Check 'Verify APK alignment'
    & (Join-Path $tools 'aapt.exe') dump badging $output | Set-Content build\apk-info.txt
    Check 'Inspect APK'
    Get-Item -LiteralPath $output | Select-Object FullName,Length
} finally {
    Remove-Item Env:\VAULTLINE_STORE_PASSWORD -ErrorAction SilentlyContinue
}
