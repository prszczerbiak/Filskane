namespace WebApplication1.Services;

using Oracle.ManagedDataAccess.Client;
using System;
using System.Configuration;
public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public bool ValidateUser(string username, string password)
    {
        using (var conn = new OracleConnection(_connectionString))
        {
            conn.Open();
            string sql = "SELECT COUNT(*) FROM Users WHERE Username = :username AND PasswordHash = :password";

            using (var cmd = new OracleCommand(sql, conn))
            {
                cmd.Parameters.Add(":username", OracleDbType.Varchar2).Value = username;
                cmd.Parameters.Add(":password", OracleDbType.Varchar2).Value = password; // Użyj hashowania!

                int count = Convert.ToInt32(cmd.ExecuteScalar());
                return count > 0;
            }
        }
    }
}
