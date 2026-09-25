<#
.SYNOPSIS
    Reproduces the application's password check outside the application.

.DESCRIPTION
    Reads the connection string from Web.config - so it tests the exact database
    the app uses - loads the stored hash, salt and iteration count for one
    employee, and runs the same derivation Core/PasswordHasher.cs runs. It then
    says whether the password would be accepted, and if not, why.

    Nothing is written. This only reads.

.EXAMPLE
    .\Test-RemotePunchLogin.ps1 -EmployeeCode ADMIN001 -Password 'Admin@12345'
#>
[CmdletBinding()]
param(
    [string] $EmployeeCode = 'ADMIN001',

    [Parameter(Mandatory = $true)]
    [string] $Password,

    [string] $WebConfigPath = (Join-Path $PSScriptRoot '..\src\RemotePunch.Web\Web.config'),

    # Overrides Web.config, for testing a database the app is not pointed at.
    [string] $ConnectionString
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Result([string] $label, [string] $value, [string] $colour = 'Gray') {
    Write-Host ("  {0,-22} {1}" -f ($label + ':'), $value) -ForegroundColor $colour
}

# ---- 1. connection string ------------------------------------------------
if (-not $ConnectionString) {
    if (-not (Test-Path $WebConfigPath)) {
        throw "Web.config not found at '$WebConfigPath'. Pass -WebConfigPath or -ConnectionString."
    }
    $config = [xml](Get-Content -Path $WebConfigPath -Raw)
    $node = $config.configuration.connectionStrings.add | Where-Object { $_.name -eq 'RemotePunch' }
    if (-not $node) { throw "No connection string named 'RemotePunch' in $WebConfigPath." }
    $ConnectionString = $node.connectionString
}

$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($ConnectionString)
Write-Host "`nConnection" -ForegroundColor Cyan
Write-Result 'Server' $builder.DataSource
Write-Result 'Database' $builder.InitialCatalog
Write-Result 'Auth' $(if ($builder.IntegratedSecurity) { 'Windows (your account, not the app pool)' } else { "SQL login '$($builder.UserID)'" })

# ---- 2. what was typed ---------------------------------------------------
Write-Host "`nPassword as supplied" -ForegroundColor Cyan
Write-Result 'Characters' $Password.Length
Write-Result 'UTF-8 bytes' ([System.Text.Encoding]::UTF8.GetByteCount($Password))
if ($Password -ne $Password.Trim()) {
    Write-Result 'Whitespace' 'LEADING OR TRAILING WHITESPACE PRESENT' 'Yellow'
}
if ($Password -match '[^\x20-\x7E]') {
    Write-Result 'Non-ASCII' 'contains characters outside plain ASCII' 'Yellow'
}

# ---- 3. the stored row ---------------------------------------------------
$connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = @'
SELECT EmployeeId, EmployeeCode, Email, Role, IsActive, MustChangePassword,
       FailedLoginCount, LockoutUntilUtc, PasswordHash, PasswordSalt, PasswordIterations
FROM   dbo.Employees
WHERE  EmployeeCode = @code OR Email = @code
'@
    $null = $command.Parameters.Add('@code', [System.Data.SqlDbType]::NVarChar, 200)
    $command.Parameters['@code'].Value = $EmployeeCode

    $reader = $command.ExecuteReader()
    if (-not $reader.Read()) {
        Write-Host "`nNo employee matches '$EmployeeCode' in this database." -ForegroundColor Red
        Write-Host "Run database\02_SeedData.sql against $($builder.InitialCatalog).`n"
        return
    }

    $storedHash = [byte[]] $reader['PasswordHash']
    $storedSalt = [byte[]] $reader['PasswordSalt']
    $iterations = [int] $reader['PasswordIterations']

    Write-Host "`nStored account" -ForegroundColor Cyan
    Write-Result 'EmployeeId' $reader['EmployeeId']
    Write-Result 'Code / email' "$($reader['EmployeeCode']) / $($reader['Email'])"
    Write-Result 'Role' $reader['Role']
    Write-Result 'IsActive' $reader['IsActive']
    Write-Result 'MustChangePassword' $(if ([bool] $reader['MustChangePassword']) { 'yes (still the seeded password)' } else { 'NO - this password has been changed since seeding' })
    Write-Result 'FailedLoginCount' $reader['FailedLoginCount']
    Write-Result 'LockoutUntilUtc' $(if ($reader['LockoutUntilUtc'] -is [DBNull]) { 'none' } else { $reader['LockoutUntilUtc'] })
    Write-Result 'Hash bytes' $storedHash.Length $(if ($storedHash.Length -eq 32) { 'Green' } else { 'Red' })
    Write-Result 'Salt bytes' $storedSalt.Length $(if ($storedSalt.Length -eq 32) { 'Green' } else { 'Red' })
    Write-Result 'Iterations' $iterations
    $reader.Close()
}
finally {
    $connection.Dispose()
}

# ---- 4. the same derivation the app performs -----------------------------
# PasswordHasher.Verify derives exactly as many bytes as the stored hash holds.
$effectiveIterations = if ($iterations -lt 1000) { 120000 } else { $iterations }

$pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
    $Password, $storedSalt, $effectiveIterations,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256)
try {
    $derivedFull = $pbkdf2.GetBytes($storedHash.Length)
}
finally { $pbkdf2.Dispose() }

$isMatch = $true
for ($i = 0; $i -lt $storedHash.Length; $i++) {
    if ($derivedFull[$i] -ne $storedHash[$i]) { $isMatch = $false; break }
}

Write-Host "`nVerdict" -ForegroundColor Cyan
if ($isMatch) {
    Write-Host "  The application WOULD accept this password." -ForegroundColor Green
    Write-Host "  If signing in still fails, the problem is what reaches the form," -ForegroundColor Green
    Write-Host "  not the stored hash - check browser autofill and keyboard layout.`n"
    return
}

Write-Host "  The application would REJECT this password." -ForegroundColor Red

# Is it only the trailing padding that differs? That is the BINARY(64) trap:
# a padded column makes a correct password fail on the zero bytes at the end.
if ($storedHash.Length -gt 32) {
    $prefixMatches = $true
    for ($i = 0; $i -lt 32; $i++) {
        if ($derivedFull[$i] -ne $storedHash[$i]) { $prefixMatches = $false; break }
    }
    if ($prefixMatches) {
        Write-Host "`n  CAUSE FOUND: the password is correct, but PasswordHash is" -ForegroundColor Yellow
        Write-Host "  $($storedHash.Length) bytes instead of 32 - the column is BINARY, which pads" -ForegroundColor Yellow
        Write-Host "  with zeros, rather than VARBINARY. Fix the column type:`n" -ForegroundColor Yellow
        Write-Host "      ALTER TABLE dbo.Employees ALTER COLUMN PasswordHash VARBINARY(64) NOT NULL;" -ForegroundColor White
        Write-Host "      ALTER TABLE dbo.Employees ALTER COLUMN PasswordSalt VARBINARY(32) NOT NULL;"  -ForegroundColor White
        Write-Host "`n  Then re-run database\02_SeedData.sql, or reset with Reset-RemotePunchPassword.ps1.`n"
        return
    }
}

Write-Host "`n  The stored hash does not correspond to this password." -ForegroundColor Yellow
Write-Host "  Either the password differs from what you typed, or the row was seeded" -ForegroundColor Yellow
Write-Host "  with different values. Set a known password with:`n" -ForegroundColor Yellow
Write-Host "      .\Reset-RemotePunchPassword.ps1 -EmployeeCode $EmployeeCode -Password '<new password>' |" -ForegroundColor White
Write-Host "          sqlcmd -S $($builder.DataSource) -E -d $($builder.InitialCatalog)`n" -ForegroundColor White
