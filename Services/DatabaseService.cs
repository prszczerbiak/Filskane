namespace WebApplication1.Services;

using Oracle.ManagedDataAccess.Client;
using System;
using System.Configuration;
using WebApplication1.Models;

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

    public UserInfo? GetUserInfo(string username)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        string sql = "SELECT Username, Name, Email, Telephone FROM Users WHERE Username = :username";

        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(":username", OracleDbType.Varchar2).Value = username;

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new UserInfo
            {
                Username = reader.GetString(0),
                Name = reader.GetString(1),
                Email = reader.GetString(2),
                Telephone = reader.IsDBNull(3) ? null : reader.GetString(3)
            };
        }

        return null;
    }
}
