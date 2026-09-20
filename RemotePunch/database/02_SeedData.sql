/* =====================================================================
   RemotePunch - seed data for a first run / demo environment.

   The three accounts below all have MustChangePassword = 1, so the app
   forces a new password at first login. Change or delete them before
   going anywhere near production.

       ADMIN001 / Admin@12345   (Admin)
       MGR001   / Mgr@12345     (Manager)
       EMP001   / Emp@12345     (Employee)

   Hashes are PBKDF2-HMAC-SHA256, 120000 iterations, 32-byte salt -
   the exact scheme Core/PasswordHasher.cs verifies against.
   ===================================================================== */

USE RemotePunch;
GO

/* ----- Sites --------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Sites WHERE Name = N'Head Office - New Delhi')
INSERT INTO dbo.Sites (Name, Address, Latitude, Longitude, RadiusMeters, IsActive)
VALUES (N'Head Office - New Delhi', N'Connaught Place, New Delhi 110001', 28.632800, 77.219700, 200, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Sites WHERE Name = N'Warehouse - Okhla')
INSERT INTO dbo.Sites (Name, Address, Latitude, Longitude, RadiusMeters, IsActive)
VALUES (N'Warehouse - Okhla', N'Okhla Industrial Area Phase II, New Delhi 110020', 28.531900, 77.273500, 250, 1);
GO

/* ----- Employees ----------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeCode = N'ADMIN001')
INSERT INTO dbo.Employees
    (EmployeeCode, FullName, Email, Phone, PasswordHash, PasswordSalt, PasswordIterations,
     MustChangePassword, Role, IsActive, ShiftStartLocal, ShiftEndLocal, AllowRemotePunch, RequireSelfie)
VALUES
    (N'ADMIN001', N'System Administrator', N'admin@example.com', NULL,
     0xA52FBDEE0BE9553C0E2A15CA8911ADD66A6A1FE34B09404FB47F5CFA05930846,
     0xED07B10CE5ECB26127EFE28895856C5B15B82887F4735815F15C55BB13EAD389,
     120000, 1, N'Admin', 1, '09:30', '18:30', 1, 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeCode = N'MGR001')
INSERT INTO dbo.Employees
    (EmployeeCode, FullName, Email, Phone, PasswordHash, PasswordSalt, PasswordIterations,
     MustChangePassword, Role, IsActive, ShiftStartLocal, ShiftEndLocal, AllowRemotePunch, RequireSelfie)
VALUES
    (N'MGR001', N'Reporting Manager', N'manager@example.com', NULL,
     0x35144C32F20E7FB1FEAFBE2775971D6B719A5444B85F437FF3EB80F64DE3A802,
     0x647C536D67660A1EE58D844F105E35A9ADB4B033B3E4A1D8A165C91063B7592C,
     120000, 1, N'Manager', 1, '09:30', '18:30', 1, 0);

IF NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE EmployeeCode = N'EMP001')
INSERT INTO dbo.Employees
    (EmployeeCode, FullName, Email, Phone, PasswordHash, PasswordSalt, PasswordIterations,
     MustChangePassword, Role, IsActive, ShiftStartLocal, ShiftEndLocal, AllowRemotePunch, RequireSelfie)
VALUES
    (N'EMP001', N'Demo Employee', N'employee@example.com', NULL,
     0x0D0EE0C8A0E030ABB294F41B09D611C78440295388ECEF0344E947EE7E380FE2,
     0x479A9BE03E0777BC619EC6F55B785FF215AB37DE6B405F14A27ABEC04CBB2F35,
     120000, 1, N'Employee', 1, '09:30', '18:30', 0, 0);
GO

/* ----- Site assignments ---------------------------------------------- */
INSERT INTO dbo.EmployeeSites (EmployeeId, SiteId)
SELECT e.EmployeeId, s.SiteId
FROM   dbo.Employees e
CROSS  JOIN dbo.Sites s
WHERE  e.EmployeeCode = N'EMP001'
  AND  s.Name = N'Head Office - New Delhi'
  AND  NOT EXISTS (SELECT 1 FROM dbo.EmployeeSites es
                   WHERE es.EmployeeId = e.EmployeeId AND es.SiteId = s.SiteId);
GO
