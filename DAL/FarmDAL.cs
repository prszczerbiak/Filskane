using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace WebApplication1.DAL;
/// <summary>
/// Warstwa dostępu do danych odpowiedzialna za zarządzanie informacjami o gospodarstwie (lokalizacja).
/// </summary>
public class FarmDAL : BaseDAL
{
    public FarmDAL(IConfiguration configuration) : base(configuration)
    {
    }

    /// <summary>
    /// Zapisuje lub aktualizuje współrzędne geograficzne środka gospodarstwa dla wskazanego użytkownika.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="farmX">Długość geograficzna (Longitude). Może być null.</param>
    /// <param name="farmY">Szerokość geograficzna (Latitude). Może być null.</param>
 
    public async Task SaveFarmCoordinatesAsync(string username, double? farmX, double? farmY)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
            UPDATE USERS
            SET FARM_LONGITUDE = :farmX, 
                FARM_LATITUDE = :farmY
            WHERE USERNAME = :username";

        await using var cmd = new OracleCommand(sql, conn);

        // Rzutowanie na object? jest wymagane, aby operator ?? zadziałał poprawnie z DBNull.Value
        cmd.Parameters.Add("farmX", OracleDbType.Double).Value = (object?)farmX ?? DBNull.Value;
        cmd.Parameters.Add("farmY", OracleDbType.Double).Value = (object?)farmY ?? DBNull.Value;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Usuwa zapisane współrzędne gospodarstwa (resetuje lokalizację).
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <remarks>
    /// Metoda wykorzystuje <see cref="SaveFarmCoordinatesAsync"/> przekazując wartości puste.
    /// </remarks>
    public async Task DeleteFarmCoordinatesAsync(string username)
    {
        await SaveFarmCoordinatesAsync(username, null, null);
    }
}
