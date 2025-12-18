namespace WebApplication1.DAL;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using WebApplication1.Models;

public class SettingsDAL : BaseDAL
{
    public SettingsDAL(IConfiguration configuration) : base(configuration)
    {
    }

    /// <summary>
    /// Pobiera szczegółowe dane do zakładki "Ustawienia/Profil".
    /// </summary>
    public async Task<UserDetailDto?> GetLongInfoAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            string sql = @"SELECT USERNAME, FIRST_NAME, EMAIL, TELEPHONE, FARM_LONGITUDE, FARM_LATITUDE
                           FROM USERS
                           WHERE USERNAME = :username";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new UserDetailDto(
                    reader["USERNAME"].ToString() ?? "",
                    reader["FIRST_NAME"].ToString() ?? "",
                    reader["EMAIL"].ToString() ?? "",
                    reader["TELEPHONE"] is DBNull ? null : reader["TELEPHONE"].ToString(),
                    reader["FARM_LONGITUDE"] is DBNull ? null : Convert.ToDouble(reader["FARM_LONGITUDE"]),
                    reader["FARM_LATITUDE"] is DBNull ? null : Convert.ToDouble(reader["FARM_LATITUDE"])
                );
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Pobiera podstawowe info (używane np. w nagłówku, ale też w serwisie Settings).
    /// </summary>
    public async Task<UserShortDto?> GetShortInfoAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            string sql = @"SELECT FIRST_NAME, IS_DARK_MODE, SURFACE_UNIT, FARM_LONGITUDE, FARM_LATITUDE
                           FROM USERS
                           WHERE USERNAME = :username";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new UserShortDto(
                    reader["FIRST_NAME"].ToString() ?? "",
                    Convert.ToInt32(reader["IS_DARK_MODE"]),
                    Convert.ToInt32(reader["SURFACE_UNIT"]),
                    reader["FARM_LONGITUDE"] is DBNull ? null : Convert.ToDouble(reader["FARM_LONGITUDE"]),
                    reader["FARM_LATITUDE"] is DBNull ? null : Convert.ToDouble(reader["FARM_LATITUDE"])
                );
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Prywatny helper do aktualizacji pojedynczego pola w tabeli USERS.
    /// </summary>
    private async Task UpdateUserFieldAsync(string username, string columnName, object value)
    {
        if (string.IsNullOrWhiteSpace(username)) return;

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        string sql = $"UPDATE USERS SET {columnName} = :val WHERE USERNAME = :username";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("val", value);
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

        await cmd.ExecuteNonQueryAsync();
    }

    // --- Metody publiczne wywoływane przez SettingsService ---

    public async Task UpdateSurfaceAsync(string username, int surface)
        => await UpdateUserFieldAsync(username, "SURFACE_UNIT", surface);

    public async Task UpdateThemeAsync(string username, int theme)
        => await UpdateUserFieldAsync(username, "IS_DARK_MODE", theme);

    public async Task UpdateFirstNameAsync(string username, string name)
        => await UpdateUserFieldAsync(username, "FIRST_NAME", name);

    public async Task UpdateEmailAsync(string username, string email)
        => await UpdateUserFieldAsync(username, "EMAIL", email);

    public async Task UpdatePhoneAsync(string username, string phone)
        => await UpdateUserFieldAsync(username, "TELEPHONE", phone);
}