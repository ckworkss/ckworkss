/* =====================================================================
   RemotePunch - Database schema (Microsoft SQL Server 2016+)
   Run this script against an empty database, e.g.:
       sqlcmd -S .\SQLEXPRESS -E -i 01_Schema.sql
   ===================================================================== */

IF DB_ID('RemotePunch') IS NULL
    EXEC('CREATE DATABASE RemotePunch');
GO

USE RemotePunch;
GO

/* ---------------------------------------------------------------
   Sites: the geofenced locations an employee may punch from.
   A site is a circle: centre (Latitude, Longitude) + RadiusMeters.
   --------------------------------------------------------------- */
IF OBJECT_ID('dbo.Sites', 'U') IS NULL
CREATE TABLE dbo.Sites
(
    SiteId          INT IDENTITY(1,1)   NOT NULL PRIMARY KEY,
    Name            NVARCHAR(120)       NOT NULL,
    Address         NVARCHAR(400)       NULL,
    Latitude        DECIMAL(9,6)        NOT NULL,
    Longitude       DECIMAL(9,6)        NOT NULL,
    RadiusMeters    INT                 NOT NULL CONSTRAINT DF_Sites_Radius DEFAULT (150),
    IsActive        BIT                 NOT NULL CONSTRAINT DF_Sites_IsActive DEFAULT (1),
    CreatedAtUtc    DATETIME2(0)        NOT NULL CONSTRAINT DF_Sites_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Sites_Lat    CHECK (Latitude  BETWEEN -90  AND 90),
    CONSTRAINT CK_Sites_Lng    CHECK (Longitude BETWEEN -180 AND 180),
    CONSTRAINT CK_Sites_Radius CHECK (RadiusMeters BETWEEN 25 AND 100000)
);
GO

/* ---------------------------------------------------------------
   Employees. Passwords are PBKDF2-HMAC-SHA256, salt + iterations
   stored per row so the work factor can be raised over time.
   --------------------------------------------------------------- */
IF OBJECT_ID('dbo.Employees', 'U') IS NULL
CREATE TABLE dbo.Employees
(
    EmployeeId          INT IDENTITY(1,1)   NOT NULL PRIMARY KEY,
    EmployeeCode        NVARCHAR(40)        NOT NULL,
    FullName            NVARCHAR(160)       NOT NULL,
    Email               NVARCHAR(200)       NOT NULL,
    Phone               NVARCHAR(40)        NULL,
    PasswordHash        VARBINARY(64)       NOT NULL,
    PasswordSalt        VARBINARY(32)       NOT NULL,
    PasswordIterations  INT                 NOT NULL CONSTRAINT DF_Emp_Iter DEFAULT (120000),
    MustChangePassword  BIT                 NOT NULL CONSTRAINT DF_Emp_MustChange DEFAULT (1),
    Role                NVARCHAR(20)        NOT NULL CONSTRAINT DF_Emp_Role DEFAULT ('Employee'),
    IsActive            BIT                 NOT NULL CONSTRAINT DF_Emp_Active DEFAULT (1),
    /* Attendance policy, per employee. */
    ShiftStartLocal     TIME(0)             NULL,
    ShiftEndLocal       TIME(0)             NULL,
    AllowRemotePunch    BIT                 NOT NULL CONSTRAINT DF_Emp_Remote DEFAULT (0),
    RequireSelfie       BIT                 NOT NULL CONSTRAINT DF_Emp_Selfie DEFAULT (0),
    /* Login throttling. */
    FailedLoginCount    INT                 NOT NULL CONSTRAINT DF_Emp_Failed DEFAULT (0),
    LockoutUntilUtc     DATETIME2(0)        NULL,
    LastLoginUtc        DATETIME2(0)        NULL,
    CreatedAtUtc        DATETIME2(0)        NOT NULL CONSTRAINT DF_Emp_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Employees_Code  UNIQUE (EmployeeCode),
    CONSTRAINT UQ_Employees_Email UNIQUE (Email),
    CONSTRAINT CK_Employees_Role  CHECK (Role IN ('Employee', 'Manager', 'Admin'))
);
GO

/* Which sites an employee may punch from. No rows = every active site. */
IF OBJECT_ID('dbo.EmployeeSites', 'U') IS NULL
CREATE TABLE dbo.EmployeeSites
(
    EmployeeId  INT NOT NULL,
    SiteId      INT NOT NULL,
    CONSTRAINT PK_EmployeeSites PRIMARY KEY (EmployeeId, SiteId),
    CONSTRAINT FK_EmployeeSites_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId) ON DELETE CASCADE,
    CONSTRAINT FK_EmployeeSites_Site     FOREIGN KEY (SiteId)     REFERENCES dbo.Sites(SiteId)         ON DELETE CASCADE
);
GO

/* ---------------------------------------------------------------
   Devices seen for an employee, keyed by a client fingerprint hash.
   The first device is trusted automatically; later ones are flagged
   until an administrator trusts them.
   --------------------------------------------------------------- */
IF OBJECT_ID('dbo.Devices', 'U') IS NULL
CREATE TABLE dbo.Devices
(
    DeviceId        INT IDENTITY(1,1)   NOT NULL PRIMARY KEY,
    EmployeeId      INT                 NOT NULL,
    Fingerprint     CHAR(64)            NOT NULL,       -- SHA-256 hex of the client signals
    Label           NVARCHAR(200)       NULL,           -- e.g. "Android 14 / Chrome"
    IsTrusted       BIT                 NOT NULL CONSTRAINT DF_Devices_Trusted DEFAULT (0),
    IsBlocked       BIT                 NOT NULL CONSTRAINT DF_Devices_Blocked DEFAULT (0),
    FirstSeenUtc    DATETIME2(0)        NOT NULL CONSTRAINT DF_Devices_First DEFAULT (SYSUTCDATETIME()),
    LastSeenUtc     DATETIME2(0)        NOT NULL CONSTRAINT DF_Devices_Last  DEFAULT (SYSUTCDATETIME()),
    PunchCount      INT                 NOT NULL CONSTRAINT DF_Devices_Count DEFAULT (0),
    CONSTRAINT UQ_Devices UNIQUE (EmployeeId, Fingerprint),
    CONSTRAINT FK_Devices_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId) ON DELETE CASCADE
);
GO

/* ---------------------------------------------------------------
   Punches. Every attempt is stored, including rejected ones, so the
   audit trail shows what was tried and why it was refused.
   --------------------------------------------------------------- */
IF OBJECT_ID('dbo.Punches', 'U') IS NULL
CREATE TABLE dbo.Punches
(
    PunchId             BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EmployeeId          INT                 NOT NULL,
    PunchType           NVARCHAR(3)         NOT NULL,   -- 'IN' | 'OUT'
    Status              NVARCHAR(10)        NOT NULL,   -- 'Accepted' | 'Flagged' | 'Rejected'
    PunchTimeUtc        DATETIME2(0)        NOT NULL,
    PunchTimeLocal      DATETIME2(0)        NOT NULL,
    WorkDateLocal       DATE                NOT NULL,   -- the shift day the punch belongs to
    /* Raw geolocation payload, exactly as the browser reported it. */
    Latitude            DECIMAL(9,6)        NOT NULL,
    Longitude           DECIMAL(9,6)        NOT NULL,
    AccuracyMeters      FLOAT               NULL,
    Altitude            FLOAT               NULL,
    AltitudeAccuracy    FLOAT               NULL,
    Speed               FLOAT               NULL,
    Heading             FLOAT               NULL,
    PositionTimestamp   DATETIME2(0)        NULL,       -- position.timestamp, from the device clock
    /* Server-computed geofence verdict. */
    SiteId              INT                 NULL,       -- nearest candidate site
    DistanceMeters      FLOAT               NULL,
    IsWithinGeofence    BIT                 NOT NULL CONSTRAINT DF_Punch_Inside DEFAULT (0),
    ResolvedAddress     NVARCHAR(400)       NULL,       -- reverse-geocoded, best effort
    /* Integrity signals. */
    RiskScore           INT                 NOT NULL CONSTRAINT DF_Punch_Risk DEFAULT (0),
    FlagReasons         NVARCHAR(1000)      NULL,       -- comma-separated reason codes
    RejectReason        NVARCHAR(400)       NULL,
    IpAddress           NVARCHAR(64)        NULL,
    UserAgent           NVARCHAR(500)       NULL,
    DeviceFingerprint   CHAR(64)            NULL,
    SelfiePath          NVARCHAR(300)       NULL,
    ClientRequestId     NVARCHAR(64)        NOT NULL,   -- idempotency key from the browser
    Note                NVARCHAR(500)       NULL,       -- employee-supplied reason for a remote punch
    /* Review workflow for flagged punches. */
    ReviewStatus        NVARCHAR(10)        NOT NULL CONSTRAINT DF_Punch_Review DEFAULT ('None'),
    ReviewedByEmpId     INT                 NULL,
    ReviewedAtUtc       DATETIME2(0)        NULL,
    ReviewNote          NVARCHAR(400)       NULL,
    CreatedAtUtc        DATETIME2(0)        NOT NULL CONSTRAINT DF_Punch_Created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Punches_ClientRequest UNIQUE (EmployeeId, ClientRequestId),
    CONSTRAINT FK_Punches_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeId),
    CONSTRAINT FK_Punches_Site     FOREIGN KEY (SiteId)     REFERENCES dbo.Sites(SiteId),
    CONSTRAINT CK_Punches_Type     CHECK (PunchType IN ('IN', 'OUT')),
    CONSTRAINT CK_Punches_Status   CHECK (Status IN ('Accepted', 'Flagged', 'Rejected')),
    CONSTRAINT CK_Punches_Review   CHECK (ReviewStatus IN ('None', 'Pending', 'Approved', 'Declined'))
);
GO

CREATE NONCLUSTERED INDEX IX_Punches_Emp_Date
    ON dbo.Punches (EmployeeId, WorkDateLocal DESC, PunchTimeUtc DESC);
GO
CREATE NONCLUSTERED INDEX IX_Punches_Date_Status
    ON dbo.Punches (WorkDateLocal DESC, Status);
GO
CREATE NONCLUSTERED INDEX IX_Punches_Review
    ON dbo.Punches (ReviewStatus, PunchTimeUtc DESC) WHERE ReviewStatus = 'Pending';
GO

/* ---------------------------------------------------------------
   Security / activity audit trail (logins, lockouts, admin edits).
   --------------------------------------------------------------- */
IF OBJECT_ID('dbo.AuditLog', 'U') IS NULL
CREATE TABLE dbo.AuditLog
(
    AuditId         BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EmployeeId      INT                 NULL,
    EventType       NVARCHAR(60)        NOT NULL,
    Detail          NVARCHAR(1000)      NULL,
    IpAddress       NVARCHAR(64)        NULL,
    UserAgent       NVARCHAR(500)       NULL,
    CreatedAtUtc    DATETIME2(0)        NOT NULL CONSTRAINT DF_Audit_Created DEFAULT (SYSUTCDATETIME())
);
GO

CREATE NONCLUSTERED INDEX IX_AuditLog_Created ON dbo.AuditLog (CreatedAtUtc DESC);
GO

/* Cache for reverse-geocode lookups, so repeat punches from the same
   spot never hit the external provider again. Key is the coordinate
   rounded to 4 decimals (~11 m). */
IF OBJECT_ID('dbo.GeocodeCache', 'U') IS NULL
CREATE TABLE dbo.GeocodeCache
(
    CacheKey        VARCHAR(32)     NOT NULL PRIMARY KEY,
    Address         NVARCHAR(400)   NOT NULL,
    CreatedAtUtc    DATETIME2(0)    NOT NULL CONSTRAINT DF_Geo_Created DEFAULT (SYSUTCDATETIME())
);
GO

/* ---------------------------------------------------------------
   Daily attendance roll-up: first IN, last OUT and paired worked
   minutes per employee per work date. Rejected punches are excluded.
   --------------------------------------------------------------- */
IF OBJECT_ID('dbo.vw_DailyAttendance', 'V') IS NOT NULL
    DROP VIEW dbo.vw_DailyAttendance;
GO
CREATE VIEW dbo.vw_DailyAttendance
AS
WITH Paired AS
(
    SELECT  p.EmployeeId,
            p.WorkDateLocal,
            p.PunchType,
            p.Status,
            p.PunchTimeLocal,
            /* The matching OUT for an IN is simply the next punch of the day. */
            LEAD(p.PunchTimeLocal) OVER (PARTITION BY p.EmployeeId, p.WorkDateLocal ORDER BY p.PunchTimeLocal) AS NextPunchLocal,
            LEAD(p.PunchType)      OVER (PARTITION BY p.EmployeeId, p.WorkDateLocal ORDER BY p.PunchTimeLocal) AS NextPunchType
    FROM    dbo.Punches p
    WHERE   p.Status <> 'Rejected'
),
Daily AS
(
    SELECT  EmployeeId,
            WorkDateLocal,
            MIN(CASE WHEN PunchType = 'IN'  THEN PunchTimeLocal END) AS FirstInLocal,
            MAX(CASE WHEN PunchType = 'OUT' THEN PunchTimeLocal END) AS LastOutLocal,
            SUM(CASE WHEN PunchType = 'IN' AND NextPunchType = 'OUT'
                     THEN DATEDIFF(MINUTE, PunchTimeLocal, NextPunchLocal) ELSE 0 END) AS WorkedMinutes,
            COUNT(*) AS PunchCount,
            SUM(CASE WHEN Status = 'Flagged' THEN 1 ELSE 0 END) AS FlaggedCount
    FROM    Paired
    GROUP BY EmployeeId, WorkDateLocal
)
SELECT  d.EmployeeId,
        e.EmployeeCode,
        e.FullName,
        d.WorkDateLocal,
        d.FirstInLocal,
        d.LastOutLocal,
        d.WorkedMinutes,
        d.PunchCount,
        d.FlaggedCount,
        CASE WHEN e.ShiftStartLocal IS NULL OR d.FirstInLocal IS NULL THEN NULL
             ELSE CASE WHEN DATEDIFF(MINUTE,
                             DATEADD(MINUTE, DATEDIFF(MINUTE, 0, e.ShiftStartLocal), CAST(d.WorkDateLocal AS DATETIME2(0))),
                             d.FirstInLocal) > 0
                       THEN DATEDIFF(MINUTE,
                             DATEADD(MINUTE, DATEDIFF(MINUTE, 0, e.ShiftStartLocal), CAST(d.WorkDateLocal AS DATETIME2(0))),
                             d.FirstInLocal)
                       ELSE 0 END
        END AS LateMinutes
FROM    Daily d
INNER JOIN dbo.Employees e ON e.EmployeeId = d.EmployeeId;
GO
