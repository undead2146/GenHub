param(
    [string]$Token
)

if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "No UPLOADTHING_TOKEN provided. Skipping injection. Build will use empty/default token."
    exit 0
}

$constantsPath = "GenHub/GenHub.Core/Constants/ApiConstants.cs"
if (-not (Test-Path $constantsPath)) {
    Write-Error "Could not find $constantsPath"
    exit 1
}

$tokenBytes = [System.Text.Encoding]::UTF8.GetBytes($Token)
$key = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($key)

$obfuscated = New-Object byte[] $tokenBytes.Length
for ($i = 0; $i -lt $tokenBytes.Length; $i++) {
    $obfuscated[$i] = $tokenBytes[$i] -bxor $key[$i % $key.Length]
}

$dataStr = ($obfuscated | ForEach-Object { "0x{0:x2}" -f $_ }) -join ", "
$keyStr = ($key | ForEach-Object { "0x{0:x2}" -f $_ }) -join ", "

$content = Get-Content $constantsPath -Raw
# Replace the placeholders we put in the static property
$content = $content -replace 'byte\[\] data = \[\]; // \[PLACEHOLDER_DATA\]', "byte[] data = [$dataStr];"
$content = $content -replace 'byte\[\] key = \[\];  // \[PLACEHOLDER_KEY\]', "byte[] key = [$keyStr];"

Set-Content $constantsPath $content
Write-Host "Successfully injected and obfuscated UPLOADTHING_TOKEN into ApiConstants.cs"
