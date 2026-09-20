using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using RemotePunch.Web.Models;

namespace RemotePunch.Web.Data
{
    public class SiteRepository
    {
        private const string SelectColumns =
            "SiteId, Name, Address, Latitude, Longitude, RadiusMeters, IsActive, CreatedAtUtc";

        public List<Site> GetAll(bool includeInactive)
        {
            List<Site> list = new List<Site>();
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT " + SelectColumns + " FROM dbo.Sites WHERE (@all = 1 OR IsActive = 1) ORDER BY Name"))
            {
                Db.Add(command, "@all", SqlDbType.Bit, includeInactive);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) list.Add(Map(reader));
                }
            }
            return list;
        }

        public Site GetById(int siteId)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT " + SelectColumns + " FROM dbo.Sites WHERE SiteId = @id"))
            {
                Db.Add(command, "@id", SqlDbType.Int, siteId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        /// <summary>
        /// The active sites an employee may punch from: their explicit
        /// assignments, or every active site when none is assigned.
        /// </summary>
        public List<Site> GetCandidateSitesForEmployee(int employeeId)
        {
            List<Site> list = new List<Site>();
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "SELECT " + SelectColumns + " FROM dbo.Sites " +
                "WHERE IsActive = 1 AND " +
                "      (NOT EXISTS (SELECT 1 FROM dbo.EmployeeSites WHERE EmployeeId = @id) " +
                "       OR SiteId IN (SELECT SiteId FROM dbo.EmployeeSites WHERE EmployeeId = @id)) " +
                "ORDER BY Name"))
            {
                Db.Add(command, "@id", SqlDbType.Int, employeeId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) list.Add(Map(reader));
                }
            }
            return list;
        }

        public int Insert(Site site)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "INSERT INTO dbo.Sites (Name, Address, Latitude, Longitude, RadiusMeters, IsActive) " +
                "VALUES (@name, @address, @lat, @lng, @radius, @active); " +
                "SELECT CAST(SCOPE_IDENTITY() AS INT);"))
            {
                Bind(command, site);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public void Update(Site site)
        {
            using (SqlConnection connection = Db.Open())
            using (SqlCommand command = Db.Command(connection,
                "UPDATE dbo.Sites SET Name = @name, Address = @address, Latitude = @lat, Longitude = @lng, " +
                "RadiusMeters = @radius, IsActive = @active WHERE SiteId = @id"))
            {
                Bind(command, site);
                Db.Add(command, "@id", SqlDbType.Int, site.SiteId);
                command.ExecuteNonQuery();
            }
        }

        private static void Bind(SqlCommand command, Site site)
        {
            Db.Add(command, "@name", SqlDbType.NVarChar, 120, site.Name);
            Db.Add(command, "@address", SqlDbType.NVarChar, 400, site.Address);
            Db.AddDecimal(command, "@lat", site.Latitude, 9, 6);
            Db.AddDecimal(command, "@lng", site.Longitude, 9, 6);
            Db.Add(command, "@radius", SqlDbType.Int, site.RadiusMeters);
            Db.Add(command, "@active", SqlDbType.Bit, site.IsActive);
        }

        private static Site Map(IDataRecord row)
        {
            return new Site
            {
                SiteId = Db.GetInt(row, "SiteId"),
                Name = Db.GetString(row, "Name"),
                Address = Db.GetString(row, "Address"),
                Latitude = Db.GetDouble(row, "Latitude"),
                Longitude = Db.GetDouble(row, "Longitude"),
                RadiusMeters = Db.GetInt(row, "RadiusMeters"),
                IsActive = Db.GetBool(row, "IsActive"),
                CreatedAtUtc = Db.GetDateTime(row, "CreatedAtUtc")
            };
        }
    }
}
