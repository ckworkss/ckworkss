using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace RemotePunch.Web.Data
{
    /// <summary>
    /// Thin ADO.NET helper. Every statement in this application is a
    /// parameterised command created through here - no string concatenation
    /// ever reaches SQL Server.
    /// </summary>
    public static class Db
    {
        public const string ConnectionName = "RemotePunch";

        public static string ConnectionString
        {
            get
            {
                ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[ConnectionName];
                if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                {
                    throw new ConfigurationErrorsException(
                        "Connection string '" + ConnectionName + "' is missing from Web.config.");
                }
                return settings.ConnectionString;
            }
        }

        public static SqlConnection Open()
        {
            SqlConnection connection = new SqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        public static SqlCommand Command(SqlConnection connection, string sql)
        {
            SqlCommand command = new SqlCommand(sql, connection);
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 30;
            return command;
        }

        /// <summary>Adds a parameter, mapping a CLR null to DBNull.</summary>
        public static SqlParameter Add(SqlCommand command, string name, SqlDbType type, object value)
        {
            SqlParameter parameter = command.Parameters.Add(name, type);
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }

        public static SqlParameter Add(SqlCommand command, string name, SqlDbType type, int size, object value)
        {
            SqlParameter parameter = command.Parameters.Add(name, type, size);
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }

        /// <summary>
        /// Adds a DECIMAL parameter from a double, rounded to the column's scale.
        /// Coordinates are stored as DECIMAL(9,6): ~11 cm of resolution, which is
        /// finer than any phone GPS and keeps values exactly comparable.
        /// </summary>
        public static SqlParameter AddDecimal(SqlCommand command, string name, double? value, byte precision, byte scale)
        {
            SqlParameter parameter = command.Parameters.Add(name, SqlDbType.Decimal);
            parameter.Precision = precision;
            parameter.Scale = scale;
            parameter.Value = value.HasValue
                ? (object)Math.Round((decimal)value.Value, scale, MidpointRounding.AwayFromZero)
                : DBNull.Value;
            return parameter;
        }

        // ---- null-safe readers ------------------------------------------

        public static string GetString(IDataRecord row, string column)
        {
            int i = row.GetOrdinal(column);
            return row.IsDBNull(i) ? null : row.GetString(i);
        }

        public static int GetInt(IDataRecord row, string column)
        {
            int i = row.GetOrdinal(column);
            return row.IsDBNull(i) ? 0 : Convert.ToInt32(row.GetValue(i));
        }

        public static int? GetNullableInt(IDataRecord row, string column)
        {
            int i = row.GetOrdinal(column);
            return row.IsDBNull(i) ? (int?)null : Convert.ToInt32(row.GetValue(i));
        }

        public static long GetLong(IDataRecord row, string column)
        {
            int i = row.GetOrdinal(column);
            return row.IsDBNull(i) ? 0L : Convert.ToInt64(row.GetValue(i));
        }

        public static double GetDouble(IDataRecord row, string column)
        {
            int i = row.GetOrdinal(column);
            return row.IsDBNull(i) ? 0d : Convert.ToDouble(row.GetValue(i));
        }

        public static double? GetNullableDouble(IDataRecord row, string column)
        {
            int i = row.GetOrdinal(column);
            return row.IsDBNull(i) ? (double?)null : Convert.ToDouble(row.GetValue(i));
        }

        public static bool GetBool(IDataRecord row, string column)
        {
            int i = row.GetOrdinal(column);
            return !row.IsDBNull(i) && Convert.ToBoolean(row.GetValue(i));
        }

        public static DateTime GetDateTime(IDataRecord row, string column)
        {
            int i = row.GetOrdinal(column);
            return row.IsDBNull(i) ? DateTime.MinValue : Convert.ToDateTime(row.GetValue(i));
        }

        public static DateTime? GetNullableDateTime(IDataRecord row, string column)
        {
            int i = row.GetOrdinal(column);
            return row.IsDBNull(i) ? (DateTime?)null : Convert.ToDateTime(row.GetValue(i));
        }

        public static TimeSpan? GetNullableTime(IDataRecord row, string column)
        {
            int i = row.GetOrdinal(column);
            return row.IsDBNull(i) ? (TimeSpan?)null : (TimeSpan)row.GetValue(i);
        }

        public static byte[] GetBytes(IDataRecord row, string column)
        {
            int i = row.GetOrdinal(column);
            return row.IsDBNull(i) ? null : (byte[])row.GetValue(i);
        }
    }
}
