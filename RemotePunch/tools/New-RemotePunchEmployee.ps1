<#
.SYNOPSIS
    Generates the SQL that creates one RemotePunch employee, password and all.

.DESCRIPTION
    The employee screen in the admin area is the normal way to add people. This
    script exists for the first account, or for recovery when no administrator
    can sign in - the cases where that screen is out of reach.

    The password hash is produced with the same .NET API Core/PasswordHasher.cs
    verifies against (PBKDF2-HMAC-SHA256, random 32-byte salt, 120000
    iterations), so the account works the moment the SQL is applied.

.EXAMPLE
    .\New-RemotePunchEmployee.ps1 -EmployeeCode CKJHA -FullName 'CK Jha' `
        -Email 'ck@example.com' -Password 'Chosen-Strong-Password' -Role Admin

.EXAMPLE
    .\New-RemotePunchEmployee.ps1 -EmployeeCode CKJHA -FullName 'CK Jha' `
        -Email 'ck@example.com' -Password 'Chosen-Strong-Password' -Role Admin |
        sqlcmd -S .\SQLEXPRESS -E -d RemotePunch
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $EmployeeCode,
    [Parameter(Mandatory = $true)] [string] $FullName,
    [Parameter(Mandatory = $true)] [string] $Email,
    [Parameter(Mandatory = $true)] [string] $Password,

    [ValidateSet('Employee', 'Manager', 'Admin')]
    [string] $Role = 'Employee',

    [string] $Phone,

    # Local shift window, 24-hour. Leave empty for no shift window, which turns
    # off the late-arrival and outside-shift reporting for this person.
    [string] $ShiftStart = '09:30',
    [string] $ShiftEnd   = '18:30',

    # Let this person punch away from their sites. Such punches are recorded,
    # flagged and queued for review rather than refused.
    [switch] $AllowRemotePunch,

    # Require a photo with every punch.
    [switch] $RequireSelfie,

    # Site names exactly as they appear in dbo.Sites. Omit to allow every
    # active site, which is what the application does when nothing is assigned.
    [string[]] $SiteNames,

    # Make the person choose a new password at first sign-in.
    [bool] $MustChangePassword = $true,

    [int] $Iterations = 120000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Password.Length -lt 10) {
    throw "The application requires at least 10 characters, and three of: upper case, lower case, digits, symbols."
}

# Escapes a value for a T-SQL literal, or returns NULL for an empty one.
function ToSqlText([string] $value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return 'NULL' }
    return "N'" + $value.Replace("'", "''") + "'"
}

# ---- hash, exactly as PasswordHasher.CreateHash does ---------------------
$salt = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($salt) } finally { $rng.Dispose() }

$pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
    $Password, $salt, $Iterations,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256)
try { $hash = $pbkdf2.GetBytes(32) } finally { $pbkdf2.Dispose() }

$hashHex = '0x' + [System.BitConverter]::ToString($hash).Replace('-', '')
$saltHex = '0x' + [System.BitConverter]::ToString($salt).Replace('-', '')

# ---- literals ------------------------------------------------------------
$codeSql  = ToSqlText $EmployeeCode
$nameSql  = ToSqlText $FullName
$emailSql = ToSqlText $Email
$phoneSql = ToSqlText $Phone
$roleSql  = ToSqlText $Role
$startSql = ToSqlText $ShiftStart
$endSql   = ToSqlText $ShiftEnd
$mustSql   = if ($MustChangePassword) { 1 } else { 0 }
$remoteSql = if ($AllowRemotePunch)   { 1 } else { 0 }
$selfieSql = if ($RequireSelfie)      { 1 } else { 0 }

$siteBlock = ''
if ($SiteNames -and $SiteNames.Count -gt 0) {
    $list = ($SiteNames | ForEach-Object { ToSqlText $_ }) -join ', '
    $siteBlock = @"

    -- Sites this employee may punch from.
    INSERT INTO dbo.EmployeeSites (EmployeeId, SiteId)
    SELECT  e.EmployeeId, s.SiteId
    FROM    dbo.Employees e
    CROSS   JOIN dbo.Sites s
    WHERE   e.EmployeeCode = $codeSql
      AND   s.Name IN ($list)
      AND   NOT EXISTS (SELECT 1 FROM dbo.EmployeeSites es
                        WHERE es.EmployeeId = e.EmployeeId AND es.SiteId = s.SiteId);

    IF @@ROWCOUNT = 0
        PRINT 'Warning: none of the named sites exist, so this employee may punch from any active site.';
"@
}

# ---- the statement -------------------------------------------------------
@"
-- RemotePunch: create employee $EmployeeCode ($FullName), role $Role
-- Generated $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') on $env:COMPUTERNAME
-- Run against the RemotePunch database.
SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeCode = $codeSql OR Email = $emailSql)
BEGIN
    PRINT 'An employee already uses that code or email - nothing was created.';
    SELECT EmployeeId, EmployeeCode, FullName, Email, Role, IsActive
    FROM   dbo.Employees
    WHERE  EmployeeCode = $codeSql OR Email = $emailSql;
END
ELSE
BEGIN
    INSERT INTO dbo.Employees
        (EmployeeCode, FullName, Email, Phone,
         PasswordHash, PasswordSalt, PasswordIterations, MustChangePassword,
         Role, IsActive, ShiftStartLocal, ShiftEndLocal, AllowRemotePunch, RequireSelfie)
    VALUES
        ($codeSql, $nameSql, $emailSql, $phoneSql,
         $hashHex, $saltHex, $Iterations, $mustSql,
         $roleSql, 1, $startSql, $endSql, $remoteSql, $selfieSql);
$siteBlock

    PRINT 'Created $EmployeeCode.';
    SELECT EmployeeId, EmployeeCode, FullName, Email, Role, IsActive, MustChangePassword
    FROM   dbo.Employees
    WHERE  EmployeeCode = $codeSql;
END
GO
"@
