using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;

namespace RemotePunch.Web.Data
{
    /// <summary>
    /// Remembers reverse-geocoded addresses so repeat punches from the same spot
    /// never hit the external provider twice.
    /// </summary>
    public class GeocodeCacheRepository
    {
        public string Get(string cacheKey)
        {
            if (string.IsNullOrEmpty(cacheKey)) return null;
            try
            {
                using (SqlConnection connection = Db.Open())
                using (SqlCommand command = Db.Command(connection,
                    "SELECT Address FROM dbo.GeocodeCache WHERE CacheKey = @key"))
                {
                    Db.Add(command, "@key", SqlDbType.VarChar, 32, cacheKey);
                    object value = command.ExecuteScalar();
                    return value == null || value == DBNull.Value ? null : Convert.ToString(value);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Geocode cache read failed: " + ex.Message);
                return null;
            }
        }

        public void Put(string cacheKey, string address)
        {
            if (string.IsNullOrEmpty(cacheKey) || string.IsNullOrEmpty(address)) return;
            try
            {
                using (SqlConnection connection = Db.Open())
                using (SqlCommand command = Db.Command(connection,
                    "IF NOT EXISTS (SELECT 1 FROM dbo.GeocodeCache WHERE CacheKey = @key) " +
                    "INSERT INTO dbo.GeocodeCache (CacheKey, Address) VALUES (@key, @address)"))
                {
                    Db.Add(command, "@key", SqlDbType.VarChar, 32, cacheKey);
                    Db.Add(command, "@address", SqlDbType.NVarChar, 400, address);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Geocode cache write failed: " + ex.Message);
            }
        }
    }
}
