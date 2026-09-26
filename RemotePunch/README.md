# RemotePunch

A geofenced remote attendance ("punching") web application built with
**ASP.NET Web Forms on .NET Framework 4.8**, C# and SQL Server.

Employees open one page on their phone, the browser reports where they are, and
the **server** decides whether that counts as being at work. Every punch is
stored with the raw sensor reading it came from, so an attendance report can
always be traced back to a coordinate, an accuracy radius, a device and a time.

---

## What it does

**Punching**

- One-tap punch in / punch out with automatic direction (the next punch is
  whichever one makes sense).
- Live location readout while the page is open: coordinates, accuracy, altitude,
  speed, heading, fix age and the number of readings taken.
- A map (Leaflet + OpenStreetMap) showing the employee, their accuracy circle
  and every geofence they may punch from.
- Optional selfie capture from the front camera, stored as evidence.
- A note field, required by the page when the employee is away from their site.
- Works on a flaky connection: a punch that fails to send is queued in the
  browser and retried, and an idempotency key stops the retry double-punching.

**Geo sensing and the geofence decision**

- Sites are circles: a centre and a radius in metres, picked on a map.
- Distance is computed with the haversine formula **on the server**, against
  the sites that employee is assigned to.
- Weak fixes can be forgiven by adding the reported accuracy to the radius,
  capped so a deliberately bad fix cannot swallow the neighbourhood.
- Fixes that are too inaccurate, or too old (cached GPS), are refused.

**Integrity checks** — every punch is scored, and the score decides accepted /
flagged / rejected:

| Finding | What it catches |
| --- | --- |
| `OUTSIDE_GEOFENCE` | Punching away from every assigned site |
| `LOW_ACCURACY` / `NO_ACCURACY_REPORTED` | A fix too vague to trust |
| `STALE_FIX` | A cached position replayed later |
| `IMPOSSIBLE_TRAVEL` | Two punches implying an impossible ground speed |
| `IDENTICAL_COORDINATES` | Byte-identical coordinates, the signature of a replayed fix |
| `SUSPICIOUS_FIX_SHAPE` | Perfect accuracy with no altitude, speed or heading |
| `DEVICE_CLOCK_SKEW` | A device clock well away from server time |
| `UNTRUSTED_DEVICE` | A phone or browser an administrator has not trusted |
| `GEOLOCATION_API_PATCHED` | The browser's geolocation function was not the native one |
| `OUTSIDE_SHIFT_HOURS` | A punch well outside the employee's shift |
| `FORWARDED_REQUEST` | The request arrived through a proxy |

**Administration**

- Dashboard: who punched today, how many punches, what was flagged or rejected.
- Review queue: every flagged punch with its map location, photo, findings and
  device, with approve / decline and a reviewer note.
- Employees: create, edit, roles (Employee / Manager / Admin), shift window,
  per-employee remote-punch and selfie policy, site assignment, password reset,
  lockout clearing.
- Sites: create and edit geofences on a map, with a live radius preview.
- Devices: trust, untrust or block a device fingerprint.
- Audit log: sign-ins, lockouts, punches, reviews, exports, admin edits, errors.
- Reports: daily attendance (first in, last out, hours worked, late minutes) and
  raw punch listings, both exportable to CSV.

---

## Getting it running

### Requirements

- Windows with IIS 8+ (or IIS Express / Visual Studio 2019+)
- .NET Framework 4.8
- SQL Server 2016 or newer (Express is fine)
- **HTTPS** — browsers refuse to share a location on a plain `http://` origin.
  `http://localhost` is the only exception, which is enough for development.

### 1. Database

```powershell
sqlcmd -S .\SQLEXPRESS -E -i database\01_Schema.sql
sqlcmd -S .\SQLEXPRESS -E -i database\02_SeedData.sql   # demo accounts, optional
```

The seed script creates three accounts, all of which must change their password
at first sign-in:

| Code | Password | Role |
| --- | --- | --- |
| `ADMIN001` | `Admin@12345` | Admin |
| `MGR001` | `Mgr@12345` | Manager |
| `EMP001` | `Emp@12345` | Employee |

**Delete or change these before the app is reachable by anyone else.**

### 2. Configure

Open `src/RemotePunch.Web/Web.config` and set:

- `<connectionStrings>` → your SQL Server instance.
- `App.TimeZoneId` → the Windows time-zone id for your local times
  (default `India Standard Time`).
- `Geo.*` and `Fraud.*` → how strict the geofence and the scoring should be.
  Every key is commented in place.
- In production also set `requireSSL="true"` on `<forms>` and `<httpCookies>`,
  and give `<machineKey>` explicit keys if you run more than one server.

### 3. Build and deploy

Open `RemotePunch.sln` in Visual Studio and press F5, or build and publish to
IIS:

```powershell
msbuild RemotePunch.sln /p:Configuration=Release
```

Then in IIS: point an application at `src/RemotePunch.Web`, give the app pool
(.NET CLR v4.0, integrated pipeline) **write access to `Uploads/Selfies`** and
read access to the rest, and bind an HTTPS certificate.

There are no NuGet packages to restore — the app uses only the framework.
Leaflet is loaded from a CDN and degrades gracefully to a hidden map; to run
fully offline, drop `leaflet.css` / `leaflet.js` into `Content/` and repoint the
two tags in `Punch.aspx` and `Admin/SiteEdit.aspx`.

### 4. First steps in the app

1. Sign in as the admin and change the password.
2. **Admin → Sites**: add your office, pick the centre on the map, set a radius.
3. **Admin → Employees**: add people, assign their sites, set shift hours, and
   decide who may punch remotely and who must send a photo.
4. Give everyone the HTTPS URL. On a phone, "Add to home screen" makes it behave
   like an app.

---

## Troubleshooting: "Employee code or password is incorrect"

That message is deliberately the same whether the account does not exist or the
password is wrong, so the form cannot be used to discover employee codes. To
find out which it is, run `tools/Check-Login.sql` against the database the app
is pointed at:

```powershell
sqlcmd -S .\SQLEXPRESS -E -d RemotePunch -i tools\Check-Login.sql
```

**No rows, or the account is missing** — the seed script has not run against
this database. Check `CurrentDatabase` in the output: running `02_SeedData.sql`
while connected to `master` creates nothing useful. Re-run it with `-d
RemotePunch`.

**The account is there** — then the stored hash does not match what you typed.
Mind the exact case and symbols (`Admin@12345`), and clear any stale value your
browser autofilled. If it still fails, set a known password with the same .NET
API the app verifies with:

```powershell
.\tools\Reset-RemotePunchPassword.ps1 -EmployeeCode ADMIN001 -Password 'Admin@12345'
```

That prints an `UPDATE` statement; run it against the database, or pipe it
straight in:

```powershell
.\tools\Reset-RemotePunchPassword.ps1 -EmployeeCode ADMIN001 -Password 'Admin@12345' |
    sqlcmd -S .\SQLEXPRESS -E -d RemotePunch
```

It also clears `FailedLoginCount` and any lockout. This is the way back in when
every administrator is locked out, since there is otherwise no account left to
reset passwords from.

**"Too many failed attempts"** — five wrong passwords lock the account for 15
minutes (`Auth.MaxFailedAttempts`, `Auth.LockoutMinutes`). Another admin can
clear it from **Admin → Employees → Edit → Clear lockout**, or the script above
does it.

---

## Command-line tools

Everything in `tools/` is for the cases where the admin screens are out of
reach - the first account, or recovery when nobody can sign in. Day to day,
use **Admin → Employees** instead.

| Script | What it does |
| --- | --- |
| `Check-Login.sql` | Reports the connected database, every employee row and the recent audit trail |
| `Test-RemotePunchLogin.ps1` | Runs the app's own password check against the stored hash and says why a sign-in fails |
| `Reset-RemotePunchPassword.ps1` | Sets a known password on an existing employee and clears any lockout |
| `New-RemotePunchEmployee.ps1` | Creates an employee, password included |

The PowerShell scripts print SQL rather than running it, so you can read it
first. Pipe to `sqlcmd` to apply it:

```powershell
.\tools\New-RemotePunchEmployee.ps1 -EmployeeCode CKJHA -FullName 'CK Jha' `
    -Email 'ck@example.com' -Password 'Chosen-Strong-Password' -Role Admin |
    sqlcmd -S .\SQLEXPRESS -E -d RemotePunch
```

They all derive password hashes with the same .NET API `Core/PasswordHasher.cs`
verifies against, so an account they create or reset works immediately.

---

## How a punch is decided

```
browser  ──► watchPosition(), keeps the most accurate reading
         ──► POST /Api/Punch.ashx  { raw coords, accuracy, altitude, speed,
                                     heading, timestamp, device signals, note,
                                     optional photo, idempotency key }
server   ──► is this a replay of a request already recorded?      → return the original
         ──► are the coordinates usable? accurate enough? fresh?  → else Rejected
         ──► haversine distance to each assigned site             → nearest + inside?
         ──► compare against the previous punch (speed, repeats)
         ──► device known? blocked? trusted?
         ──► score every finding
         ──► Rejected | Flagged (queued for review) | Accepted
         ──► store the punch, the findings, the photo and an audit entry
```

The browser's own distance calculation is only used to draw the screen. If a
client lies about being inside a geofence, the server ignores it, because the
server recomputes everything from the coordinates.

---

## Security notes

- Passwords: PBKDF2-HMAC-SHA256, 120 000 iterations, 32-byte per-user salt, with
  the iteration count stored per row so it can be raised later.
- Sign-in throttling: configurable lockout after repeated failures, and the same
  error message whether the account exists or not.
- Every SQL statement is parameterised; no query is built by concatenating input.
- Forms authentication with an encrypted, signed ticket; role checks for the
  admin area live in `Web.config`, and every page re-checks on the server.
- The punch endpoint requires a custom header, which a cross-site form post
  cannot set, on top of a `SameSite=Lax` auth cookie.
- Selfies are stored outside the servable path and handed out only by
  `Api/Selfie.ashx`, which checks that the caller owns the punch or may review
  others'.
- CSV exports neutralise leading `=`, `+`, `-`, `@` so a note cannot become a
  spreadsheet formula.

### What this cannot do

Be straight with your users about this: **no browser-based system can prove a
location.** A rooted phone with a mock-location provider, a patched browser or a
crafted HTTP request can all present coordinates that never happened. What this
application does is make that *expensive and visible*: the fix has to be recent,
accurate, consistent with the previous punch, from a known device, and it is all
recorded. The findings table above is a detection aid, not a guarantee, which is
why flagged punches go to a human rather than being silently dropped.

Reverse geocoding (`Geocode.Provider`) is **off** by default. Turning it on
sends employee coordinates to OpenStreetMap or Google — check that against your
privacy obligations first, and note Nominatim's usage policy.

---

## Project layout

```
RemotePunch.sln
database/
  01_Schema.sql            tables, indexes, daily-attendance view
  02_SeedData.sql          demo sites and accounts
src/RemotePunch.Web/
  Punch.aspx               the punch screen
  Login / Logout / ChangePassword / MyAttendance / Reports / Error
  Admin/                   dashboard, review queue, employees, sites, devices, audit
  Api/                     Punch.ashx, Status.ashx, Selfie.ashx, Export.ashx
  Core/                    AppConfig, AppUser, GeoMath, PasswordHasher, ClientInfo, Json, PageHelpers
  Data/                    ADO.NET repositories, one per table
  Models/                  plain data classes
  Services/                PunchService (the engine), GeofenceEvaluator, FlagCodes,
                           AuthService, ReverseGeocoder, SelfieStore
  Scripts/punch.js         client-side geo sensing, camera, offline queue
  Content/site.css         the whole stylesheet
  Uploads/Selfies/         stored photos (kept out of source control)
```

`Services/PunchService.cs` is the file to read first: it is where a location
reading becomes an attendance record.
