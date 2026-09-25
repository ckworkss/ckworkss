<#
.SYNOPSIS
    Generates a RemotePunch password hash and the UPDATE statement that applies it.

.DESCRIPTION
    Recovery tool for when nobody can sign in - a forgotten admin password, or a
    seeded account that will not accept its documented password.

    It uses the same .NET API as Core/PasswordHasher.cs (PBKDF2-HMAC-SHA256, a
    random 32-byte salt, 120000 iterations, a 32-byte hash), so whatever it
    produces is by construction what the application will verify against.

    Requires Windows PowerShell 5.1 with .NET Framework 4.7.2 or newer, which
    any machine running this application already has.

.EXAMPLE
    .\Reset-RemotePunchPassword.ps1 -EmployeeCode ADMIN001 -Password 'Admin@12345'

    Prints the UPDATE statement. Paste it into SSMS against the RemotePunch database.

.EXAMPLE
    .\Reset-RemotePunchPassword.ps1 -EmployeeCode ADMIN001 -Password 'Admin@12345' |
        sqlcmd -S .\SQLEXPRESS -E -d RemotePunch

    Generates and applies it in one go.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $EmployeeCode,

    [Parameter(Mandatory = $true)]
    [string] $Password,

    # Leave at the default unless Core/PasswordHasher.cs was changed.
    [int] $Iterations = 120000,

    # Whether the employee must choose a new password at their next sign-in.
    [bool] $MustChangePassword = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Password.Length -lt 10) {
    Write-Warning "The application requires at least 10 characters; this password will be refused at the next change."
}

# 32 random bytes of salt, exactly as PasswordHasher.CreateHash does.
$salt = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($salt) } finally { $rng.Dispose() }

# PBKDF2-HMAC-SHA256. The SHA256 overload needs .NET Framework 4.7.2+.
$pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
    $Password,
    $salt,
    $Iterations,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256)
try { $hash = $pbkdf2.GetBytes(32) } finally { $pbkdf2.Dispose() }

$hashHex = '0x' + [System.BitConverter]::ToString($hash).Replace('-', '')
$saltHex = '0x' + [System.BitConverter]::ToString($salt).Replace('-', '')
$mustChange = if ($MustChangePassword) { 1 } else { 0 }

# Single-quote escaping for the employee code, so an odd code cannot break the statement.
$code = $EmployeeCode.Replace("'", "''")

@"
-- RemotePunch password reset for $EmployeeCode
-- Generated $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') on $env:COMPUTERNAME
-- Run against the RemotePunch database.
UPDATE dbo.Employees
SET    PasswordHash       = $hashHex,
       PasswordSalt       = $saltHex,
       PasswordIterations = $Iterations,
       MustChangePassword = $mustChange,
       FailedLoginCount   = 0,
       LockoutUntilUtc    = NULL
WHERE  EmployeeCode = N'$code';

-- Expect "(1 row affected)". Zero rows means no employee has that code.
SELECT EmployeeId, EmployeeCode, Email, Role, IsActive
FROM   dbo.Employees
WHERE  EmployeeCode = N'$code';
GO
"@
