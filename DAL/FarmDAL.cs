namespace WebApplication1.DAL;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

public class FarmDAL : BaseDAL
{
    public FarmDAL(IConfiguration configuration) : base(configuration)
    {
    }

    /// <summary>
    /// Zapisuje lub aktualizuje współrzędne środka gospodarstwa.
    /// </summary>
    public async Task SaveFarmCoordinatesAsync(string username, double? farmX, double? farmY)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        string sql = @"UPDATE USERS
                       SET FARM_LONGITUDE = :farmX, FARM_LATITUDE = :farmY
                       WHERE USERNAME = :username";

        await using var cmd = new OracleCommand(sql, conn);

        // Obsługa NULLi (jeśli użytkownik usuwa lokalizację)
        cmd.Parameters.Add("farmX", OracleDbType.Double).Value = (object?)farmX ?? DBNull.Value;
        cmd.Parameters.Add("farmY", OracleDbType.Double).Value = (object?)farmY ?? DBNull.Value;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Usuwa lokalizację farmy (ustawia null).
    /// </summary>
    public async Task DeleteFarmCoordinatesAsync(string username)
    {
        // Reużywamy metody zapisu, przekazując null
        await SaveFarmCoordinatesAsync(username, null, null);
    }
}