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

/* Has anyone already changed this password, or signed in successfully before?
   A PasswordChanged row means the seeded password is no longer valid. */
SELECT TOP 30
        a.AuditId,
        a.CreatedAtUtc,
        e.EmployeeCode,
        a.EventType,
        a.Detail,
        a.IpAddress
FROM    dbo.AuditLog a
LEFT JOIN dbo.Employees e ON e.EmployeeId = a.EmployeeId
ORDER BY a.AuditId DESC;
