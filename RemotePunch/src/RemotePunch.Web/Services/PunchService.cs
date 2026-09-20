using System;
using System.Collections.Generic;
using System.Globalization;
using RemotePunch.Web.Core;
using RemotePunch.Web.Data;
using RemotePunch.Web.Models;

namespace RemotePunch.Web.Services
{
    /// <summary>
    /// Everything the punch screen needs to render before anyone presses a
    /// button: who they are, what they may do next, and where their sites are.
    /// </summary>
    public class PunchStatus
    {
        public string NextAction { get; set; }              // IN | OUT
        public bool IsPunchedIn { get; set; }
        public Punch LastPunch { get; set; }
        public List<Punch> TodayPunches { get; set; }
        public List<Site> Sites { get; set; }
        public bool SelfieRequired { get; set; }
        public bool AllowRemotePunch { get; set; }
        public int WorkedMinutesToday { get; set; }

        public PunchStatus()
        {
            TodayPunches = new List<Punch>();
            Sites = new List<Site>();
            NextAction = "IN";
        }
    }

    /// <summary>
    /// The punch engine: validates a location fix, scores it for tampering,
    /// decides accepted / flagged / rejected, and writes the record.
    ///
    /// Nothing the browser says is taken at face value. The client's own
    /// distance calculation, its idea of which site it is at and its claim to
    /// be inside a geofence are all recomputed here from the raw coordinates.
    /// </summary>
    public class PunchService
    {
        private readonly EmployeeRepository _employees = new EmployeeRepository();
        private readonly SiteRepository _sites = new SiteRepository();
        private readonly PunchRepository _punches = new PunchRepository();
        private readonly DeviceRepository _devices = new DeviceRepository();
        private readonly AuditRepository _audit = new AuditRepository();
        private readonly ReverseGeocoder _geocoder = new ReverseGeocoder();

        // ---- status -----------------------------------------------------

        public PunchStatus GetStatus(int employeeId)
        {
            Employee employee = _employees.GetById(employeeId);
            PunchStatus status = new PunchStatus();
            if (employee == null) return status;

            status.SelfieRequired = AppConfig.SelfieEnabled &&
                                    (AppConfig.SelfieRequiredForAll || employee.RequireSelfie);
            status.AllowRemotePunch = employee.AllowRemotePunch;
            status.Sites = _sites.GetCandidateSitesForEmployee(employeeId);

            Punch last = _punches.GetLastAcceptedPunch(employeeId);
            status.LastPunch = last;
            status.IsPunchedIn = last != null && string.Equals(last.PunchType, "IN", StringComparison.Ordinal);
            status.NextAction = status.IsPunchedIn ? "OUT" : "IN";

            DateTime workDate = CurrentWorkDate();
            status.TodayPunches = _punches.GetForEmployeeAndDate(employeeId, workDate);
            status.WorkedMinutesToday = WorkedMinutes(status.TodayPunches);
            return status;
        }

        /// <summary>Pairs IN/OUT punches of one day and totals the minutes between them.</summary>
        public static int WorkedMinutes(List<Punch> dayPunches)
        {
            if (dayPunches == null) return 0;

            int total = 0;
            DateTime? openIn = null;
            foreach (Punch punch in dayPunches)
            {
                if (string.Equals(punch.Status, "Rejected", StringComparison.Ordinal)) continue;

                if (string.Equals(punch.PunchType, "IN", StringComparison.Ordinal))
                {
                    openIn = punch.PunchTimeLocal;
                }
                else if (openIn.HasValue)
                {
                    total += (int)Math.Max(0, (punch.PunchTimeLocal - openIn.Value).TotalMinutes);
                    openIn = null;
                }
            }
            return total;
        }

        /// <summary>
        /// The local work date a punch belongs to. Punches before the configured
        /// roll-over hour count towards the previous day so night shifts stay on
        /// one row.
        /// </summary>
        public static DateTime CurrentWorkDate()
        {
            return WorkDateFor(AppConfig.ToLocal(DateTime.UtcNow));
        }

        public static DateTime WorkDateFor(DateTime localTime)
        {
            int rollover = AppConfig.WorkDayRolloverHour;
            return localTime.Hour < rollover ? localTime.Date.AddDays(-1) : localTime.Date;
        }

        // ---- the punch --------------------------------------------------

        public PunchResult Register(int employeeId, PunchRequest request, ClientInfo client)
        {
            if (request == null) return Error("The punch request was empty.");

            Employee employee = _employees.GetById(employeeId);
            if (employee == null || !employee.IsActive)
                return Error("This account can no longer punch. Contact your administrator.");

            string requestId = ClientInfo.Truncate((request.ClientRequestId ?? string.Empty).Trim(), 64);
            if (string.IsNullOrEmpty(requestId)) return Error("The punch request was missing its request id.");

            // A retried request (flaky mobile network, double tap) must never
            // create a second punch: return the original outcome instead.
            Punch existing = _punches.GetByClientRequestId(employeeId, requestId);
            if (existing != null)
            {
                PunchResult replay = ToResult(existing, employee);
                replay.duplicate = true;
                replay.message = "This punch was already recorded at " +
                                 existing.PunchTimeLocal.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + ".";
                return replay;
            }

            GeoFix fix = request.Fix;
            if (fix == null) return Error("No location was supplied. Allow location access and try again.");
            if (!GeoMath.IsPlausibleCoordinate(fix.Latitude, fix.Longitude))
                return Error("The location reported by your device is not usable. Move outdoors and try again.");

            DateTime nowUtc = DateTime.UtcNow;
            DateTime nowLocal = AppConfig.ToLocal(nowUtc);

            List<string> flags = new List<string>();
            int risk = 0;
            string rejectReason = null;

            // ---- fix quality --------------------------------------------
            if (!fix.AccuracyMeters.HasValue || fix.AccuracyMeters.Value <= 0)
            {
                AddFlag(flags, ref risk, FlagCodes.NoAccuracy);
            }
            else if (fix.AccuracyMeters.Value > AppConfig.MaxAccuracyMeters)
            {
                rejectReason = "Your location is only accurate to " +
                               GeoMath.FormatDistance(fix.AccuracyMeters.Value) +
                               ". Move into the open and try again.";
                AddFlag(flags, ref risk, FlagCodes.LowAccuracy);
            }
            else if (fix.AccuracyMeters.Value > AppConfig.WarnAccuracyMeters)
            {
                AddFlag(flags, ref risk, FlagCodes.LowAccuracy);
            }

            double ageSeconds = FixAgeSeconds(fix, nowUtc);
            if (ageSeconds > AppConfig.MaxPositionAgeSeconds)
            {
                AddFlag(flags, ref risk, FlagCodes.StaleFix);
                if (rejectReason == null)
                {
                    rejectReason = "Your device returned a cached location from " +
                                   Math.Round(ageSeconds / 60.0).ToString("F0", CultureInfo.InvariantCulture) +
                                   " minute(s) ago. Refresh your location and try again.";
                }
            }

            // A fix with no altitude, speed or heading at all, yet a suspiciously
            // perfect accuracy, is the shape most mock-location apps produce.
            bool noMotionData = !fix.Altitude.HasValue && !fix.Speed.HasValue && !fix.Heading.HasValue;
            if (noMotionData && fix.AccuracyMeters.HasValue && fix.AccuracyMeters.Value > 0 &&
                fix.AccuracyMeters.Value <= 5)
            {
                AddFlag(flags, ref risk, FlagCodes.SuspiciousFixShape);
            }

            if (fix.PositionTimestampUtc.HasValue)
            {
                double skewMinutes = Math.Abs((fix.PositionTimestampUtc.Value - nowUtc).TotalMinutes);
                if (skewMinutes > 5) AddFlag(flags, ref risk, FlagCodes.ClockSkew);
            }

            // ---- findings the browser itself reported --------------------
            // The page reports what it could check about its own environment -
            // useful, but only ever additive: a tampered client simply sends
            // nothing, which is why every check above runs server side.
            if (request.ClientWarnings != null)
            {
                int accepted = 0;
                foreach (string warning in request.ClientWarnings)
                {
                    if (string.IsNullOrWhiteSpace(warning)) continue;
                    if (++accepted > 10) break;
                    AddFlag(flags, ref risk, ClientInfo.Truncate(warning.Trim().ToUpperInvariant(), 40));
                }
            }

            // ---- geofence ------------------------------------------------
            List<Site> candidateSites = _sites.GetCandidateSitesForEmployee(employeeId);
            GeofenceMatch match = GeofenceEvaluator.Evaluate(candidateSites, fix.Latitude, fix.Longitude,
                                                             fix.AccuracyMeters);

            if (!match.HadCandidates)
            {
                AddFlag(flags, ref risk, FlagCodes.NoSiteConfigured);
            }
            else if (!match.IsInside)
            {
                AddFlag(flags, ref risk, FlagCodes.OutsideGeofence);

                if (!employee.AllowRemotePunch && AppConfig.RejectOutsideGeofence && rejectReason == null)
                {
                    rejectReason = "You are " + GeoMath.FormatDistance(match.DistanceMeters.GetValueOrDefault()) +
                                   " from " + (match.NearestSite != null ? match.NearestSite.Name : "your site") +
                                   ". Punching is only allowed at your assigned location.";
                }
            }
            if (!match.IsInside && employee.AllowRemotePunch) AddFlag(flags, ref risk, FlagCodes.RemotePunch);

            // ---- history-based checks ------------------------------------
            Punch last = _punches.GetLastAcceptedPunch(employeeId);
            if (last != null)
            {
                double secondsSinceLast = (nowUtc - last.PunchTimeUtc).TotalSeconds;
                if (secondsSinceLast >= 0 && secondsSinceLast < AppConfig.MinPunchIntervalSeconds && rejectReason == null)
                {
                    rejectReason = "You punched less than " +
                                   AppConfig.MinPunchIntervalSeconds.ToString(CultureInfo.InvariantCulture) +
                                   " seconds ago. Please wait before punching again.";
                }

                double impliedSpeed = GeoMath.ImpliedSpeedKmh(last.Latitude, last.Longitude, last.PunchTimeUtc,
                                                              fix.Latitude, fix.Longitude, nowUtc);
                if (impliedSpeed > AppConfig.MaxImpliedSpeedKmh)
                {
                    AddFlag(flags, ref risk, FlagCodes.ImpossibleTravel);
                }

                if (AppConfig.FlagIdenticalCoordinates &&
                    Math.Abs(last.Latitude - Round6(fix.Latitude)) < 0.0000005 &&
                    Math.Abs(last.Longitude - Round6(fix.Longitude)) < 0.0000005)
                {
                    AddFlag(flags, ref risk, FlagCodes.IdenticalCoordinates);
                }
            }

            // ---- device --------------------------------------------------
            string fingerprint = null;
            if (!string.IsNullOrWhiteSpace(request.DeviceSignals))
            {
                fingerprint = ClientInfo.Fingerprint(request.DeviceSignals, client == null ? null : client.UserAgent);
                Device device = _devices.Upsert(employeeId, fingerprint,
                                                ClientInfo.Truncate(request.DeviceLabel, 200),
                                                AppConfig.AutoTrustFirstDevice);
                if (device != null)
                {
                    if (device.IsBlocked && rejectReason == null)
                    {
                        rejectReason = "This device has been blocked for punching. Contact your administrator.";
                    }
                    if (!device.IsTrusted && AppConfig.FlagUntrustedDevice)
                    {
                        AddFlag(flags, ref risk, FlagCodes.UntrustedDevice);
                    }
                }
            }

            if (client != null && client.ForwardedForPresent) AddFlag(flags, ref risk, FlagCodes.ProxyHeader);

            // ---- punch direction -----------------------------------------
            bool punchedIn = last != null && string.Equals(last.PunchType, "IN", StringComparison.Ordinal);
            string expected = punchedIn ? "OUT" : "IN";
            string punchType = expected;

            string requested = (request.RequestedType ?? "AUTO").Trim().ToUpperInvariant();
            if (requested == "IN" || requested == "OUT")
            {
                punchType = requested;
                if (requested != expected && rejectReason == null)
                {
                    rejectReason = punchedIn
                        ? "You are already punched in. Punch out first."
                        : "You are not punched in yet. Punch in first.";
                }
            }

            // ---- shift window --------------------------------------------
            if (IsOutsideShift(employee, nowLocal)) AddFlag(flags, ref risk, FlagCodes.OutsideShift);

            // ---- selfie ---------------------------------------------------
            bool selfieRequired = AppConfig.SelfieEnabled &&
                                  (AppConfig.SelfieRequiredForAll || employee.RequireSelfie);
            string selfiePath = null;
            if (AppConfig.SelfieEnabled && !string.IsNullOrWhiteSpace(request.SelfieBase64))
            {
                string selfieProblem;
                selfiePath = SelfieStore.Save(request.SelfieBase64, employeeId, out selfieProblem);
                if (selfiePath == null && selfieProblem != null && rejectReason == null) rejectReason = selfieProblem;
            }
            if (selfieRequired && selfiePath == null)
            {
                AddFlag(flags, ref risk, FlagCodes.NoSelfie);
                if (rejectReason == null) rejectReason = "A photo is required with every punch on this account.";
            }

            // ---- verdict ---------------------------------------------------
            // Rejected  - refused outright, the employee's state is unchanged.
            // Flagged    - recorded and counted, but queued for a manager to review.
            // Accepted   - inside a geofence with nothing worth reporting.
            string status;
            if (rejectReason != null || risk >= AppConfig.RejectRiskThreshold)
            {
                status = "Rejected";
                if (rejectReason == null) rejectReason = "This punch looked unsafe to accept automatically.";
            }
            else if (!match.IsInside || risk >= AppConfig.FlagRiskThreshold)
            {
                status = "Flagged";
            }
            else
            {
                status = "Accepted";
            }

            // Best-effort address for the audit trail; never affects the verdict.
            string address = _geocoder.Resolve(fix.Latitude, fix.Longitude);

            Punch punch = new Punch
            {
                EmployeeId = employeeId,
                PunchType = punchType,
                Status = status,
                PunchTimeUtc = nowUtc,
                PunchTimeLocal = nowLocal,
                WorkDateLocal = WorkDateFor(nowLocal),
                Latitude = Round6(fix.Latitude),
                Longitude = Round6(fix.Longitude),
                AccuracyMeters = fix.AccuracyMeters,
                Altitude = fix.Altitude,
                AltitudeAccuracy = fix.AltitudeAccuracy,
                Speed = fix.Speed,
                Heading = fix.Heading,
                PositionTimestamp = fix.PositionTimestampUtc,
                SiteId = match.NearestSite != null ? (int?)match.NearestSite.SiteId : null,
                DistanceMeters = match.DistanceMeters,
                IsWithinGeofence = match.IsInside,
                ResolvedAddress = address,
                RiskScore = risk,
                FlagReasons = flags.Count == 0 ? null : string.Join(",", flags.ToArray()),
                RejectReason = ClientInfo.Truncate(rejectReason, 400),
                IpAddress = client == null ? null : client.IpAddress,
                UserAgent = client == null ? null : client.UserAgent,
                DeviceFingerprint = fingerprint,
                SelfiePath = selfiePath,
                ClientRequestId = requestId,
                Note = ClientInfo.Truncate(request.Note, 500),
                ReviewStatus = string.Equals(status, "Flagged", StringComparison.Ordinal) ? "Pending" : "None"
            };

            punch.PunchId = _punches.Insert(punch);
            punch.SiteName = match.NearestSite != null ? match.NearestSite.Name : null;

            _audit.Write(employeeId, "Punch" + status,
                punchType + " at " + punch.PunchTimeLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                "; distance=" + (match.DistanceMeters.HasValue
                    ? Math.Round(match.DistanceMeters.Value).ToString("F0", CultureInfo.InvariantCulture)
                    : "n/a") +
                "m; risk=" + risk.ToString(CultureInfo.InvariantCulture) +
                (punch.FlagReasons == null ? "" : "; flags=" + punch.FlagReasons),
                client);

            return ToResult(punch, employee);
        }

        // ---- helpers ----------------------------------------------------

        private static void AddFlag(List<string> flags, ref int risk, string code)
        {
            if (flags.Contains(code)) return;
            flags.Add(code);
            risk += FlagCodes.RiskOf(code);
        }

        private static double Round6(double value)
        {
            return Math.Round(value, 6, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Age of the fix in seconds. The client's own measurement is preferred
        /// because it is immune to a wrong device clock; the position timestamp
        /// is the fallback.
        /// </summary>
        private static double FixAgeSeconds(GeoFix fix, DateTime nowUtc)
        {
            if (fix.AgeSeconds.HasValue && fix.AgeSeconds.Value >= 0) return fix.AgeSeconds.Value;
            if (fix.PositionTimestampUtc.HasValue)
            {
                double seconds = (nowUtc - fix.PositionTimestampUtc.Value).TotalSeconds;
                return seconds > 0 ? seconds : 0;
            }
            return 0;
        }

        private static bool IsOutsideShift(Employee employee, DateTime nowLocal)
        {
            if (!employee.ShiftStartLocal.HasValue || !employee.ShiftEndLocal.HasValue) return false;

            TimeSpan now = nowLocal.TimeOfDay;
            TimeSpan start = employee.ShiftStartLocal.Value;
            TimeSpan end = employee.ShiftEndLocal.Value;
            // An hour of slack either side keeps early arrivals off the report.
            TimeSpan slack = TimeSpan.FromHours(1);

            if (start <= end)
            {
                return now < start - slack || now > end + slack;
            }
            // Shift crosses midnight.
            return now < start - slack && now > end + slack;
        }

        private static PunchResult Error(string message)
        {
            return new PunchResult { ok = false, status = "Error", message = message };
        }

        private PunchResult ToResult(Punch punch, Employee employee)
        {
            bool accepted = !string.Equals(punch.Status, "Rejected", StringComparison.Ordinal);
            string label = string.Equals(punch.PunchType, "IN", StringComparison.Ordinal) ? "Punched in" : "Punched out";

            string message;
            if (!accepted)
            {
                message = punch.RejectReason ?? "This punch was rejected.";
            }
            else if (string.Equals(punch.Status, "Flagged", StringComparison.Ordinal))
            {
                message = label + " at " + punch.PunchTimeLocal.ToString("HH:mm:ss", CultureInfo.InvariantCulture) +
                          ", but it needs a manager's review.";
            }
            else
            {
                message = label + " at " + punch.PunchTimeLocal.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + ".";
            }

            PunchResult result = new PunchResult
            {
                ok = accepted,
                status = punch.Status,
                punchType = punch.PunchType,
                message = message,
                timeLocal = punch.PunchTimeLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                workDate = punch.WorkDateLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                siteName = punch.SiteName,
                distanceMeters = punch.DistanceMeters.HasValue
                    ? (double?)Math.Round(punch.DistanceMeters.Value, 1)
                    : null,
                withinGeofence = punch.IsWithinGeofence,
                address = punch.ResolvedAddress,
                riskScore = punch.RiskScore,
                flags = FlagCodes.DescribeAll(punch.FlagReasons),
                punchId = punch.PunchId
            };

            // A rejected punch does not change the employee's state, so the next
            // action stays whatever it was before the attempt.
            if (accepted)
            {
                result.nextAction = string.Equals(punch.PunchType, "IN", StringComparison.Ordinal) ? "OUT" : "IN";
            }
            else
            {
                PunchStatus status = GetStatus(punch.EmployeeId);
                result.nextAction = status.NextAction;
            }
            return result;
        }
    }
}
