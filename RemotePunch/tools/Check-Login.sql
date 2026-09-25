/* Which database am I actually in, and is the account there? */
SELECT DB_NAME() AS CurrentDatabase, @@SERVERNAME AS ServerName;

SELECT  EmployeeId,
        EmployeeCode,
        Email,
        Role,
        IsActive,
        MustChangePassword,
        FailedLoginCount,
        LockoutUntilUtc,
        DATALENGTH(PasswordHash) AS HashBytes,   -- expect 32
        DATALENGTH(PasswordSalt) AS SaltBytes,   -- expect 32
        PasswordIterations                       -- expect 120000
FROM    dbo.Employees
ORDER BY EmployeeCode;
