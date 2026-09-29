param([string]$OldApplication = 'D:\Vaultline\Vaultline.exe')
$ErrorActionPreference = 'Stop'
$newApplication = Join-Path (Split-Path $PSScriptRoot) 'dist\Vaultline.exe'
$oldAssembly = [Reflection.Assembly]::LoadFile($OldApplication)
$newAssembly = [Reflection.Assembly]::LoadFile($newApplication)
$oldCrypto = $oldAssembly.GetType('Vaultline.Crypto', $true)
$newCrypto = $newAssembly.GetType('Vaultline.Crypto', $true)
$key = [byte[]]::new(64)
$salt = [byte[]]::new(16)
$random = [Security.Cryptography.RandomNumberGenerator]::Create()
$random.GetBytes($key)
$random.GetBytes($salt)
$random.Dispose()
$plain = [Text.Encoding]::UTF8.GetBytes('{"Version":1,"Folders":["account"],"Entries":[]}')
try {
    $oldBlob = $oldCrypto.GetMethod('Seal').Invoke($null, [object[]]@($plain, $key, $salt))
    $newPlain = $newCrypto.GetMethod('Open').Invoke($null, [object[]]@($oldBlob, $key))
    if ([Convert]::ToBase64String($plain) -ne [Convert]::ToBase64String($newPlain)) { throw 'Old vault format failed to decrypt with new code' }
    $newBlob = $newCrypto.GetMethod('Seal').Invoke($null, [object[]]@($plain, $key, $salt))
    $oldPlain = $oldCrypto.GetMethod('Open').Invoke($null, [object[]]@($newBlob, $key))
    if ([Convert]::ToBase64String($plain) -ne [Convert]::ToBase64String($oldPlain)) { throw 'New vault format failed to decrypt with old code' }
    'PASS: synthetic vault ciphertext compatible in both directions, v1.1 and v1.2.' | Set-Content (Join-Path $PSScriptRoot 'compatibility-results.txt')
} finally {
    [Array]::Clear($key, 0, $key.Length)
}
