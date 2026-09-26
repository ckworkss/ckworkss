<#
.SYNOPSIS
    Checks a password against a hash and salt using .NET, with no database.

.DESCRIPTION
    Isolates one question: does .NET's PBKDF2 agree with the hash that was
    generated for this password? If it prints MATCH, the credential pair is
    sound and any remaining sign-in failure is elsewhere - what the form posts,
    or what actually landed in the column.

.EXAMPLE
    .\Verify-HashOffline.ps1
#>
param(
    [string] $Password = 'Punch@2026',
    [string] $HashHex  = 'A0E3A2458F4008E453404C8330358CE88B0D0B6E8000122B07427E7F0ED268FF',
    [string] $SaltHex  = '80AE5DB3C3BC6ADFC2AE66C7F782F482CA93136D5E9E8FF4DB54AF806CAD4AEA',
    [int]    $Iterations = 120000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function FromHex([string] $hex) {
    $hex = $hex -replace '^0[xX]', ''
    $bytes = New-Object byte[] ($hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16)
    }
    return ,$bytes
}

$salt     = FromHex $SaltHex
$expected = FromHex $HashHex

$pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
    $Password, $salt, $Iterations,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256)
try { $derived = $pbkdf2.GetBytes($expected.Length) } finally { $pbkdf2.Dispose() }

$expectedHex = [System.BitConverter]::ToString($expected).Replace('-', '')
$derivedHex  = [System.BitConverter]::ToString($derived).Replace('-', '')

Write-Host ""
Write-Host "  password   : $Password"
Write-Host "  iterations : $Iterations"
Write-Host "  salt bytes : $($salt.Length)   (must be 32)"
Write-Host "  hash bytes : $($expected.Length)   (must be 32)"
Write-Host "  expected   : $expectedHex"
Write-Host "  .NET gives : $derivedHex"
Write-Host ""

if ($expectedHex -eq $derivedHex) {
    Write-Host "  MATCH - .NET agrees. This password and hash are a valid pair," -ForegroundColor Green
    Write-Host "  so the application will accept them if they are what is stored.`n" -ForegroundColor Green
} else {
    Write-Host "  NO MATCH - .NET derives something different. Send me both lines above.`n" -ForegroundColor Red
}
