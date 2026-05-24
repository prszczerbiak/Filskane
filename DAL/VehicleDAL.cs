using Filskane.Models;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace Filskane.DAL;

/// <summary>
/// Warstwa dostępu do danych odpowiedzialna za pobieranie pojazdów przypisanych do użytkownika.
/// </summary>
public class VehicleDAL : BaseDAL
{
    public VehicleDAL(IConfiguration configuration) : base(configuration)
    {
    }

    /// <summary>
    /// Dodaje nowy pojazd do bazy dla wskazanego użytkownika.
    /// </summary>
    public async Task<int> AddVehicleAsync(string username, string vehicleName, string ipAdress, int tcpPort)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
            INSERT INTO VEHICLES (
                VEHICLE_NAME, USER_ID, IP_ADRESS, TCP_PORT
            )
            VALUES (
                :vehicleName,
                (SELECT USER_ID FROM USERS WHERE USERNAME = :username),
                :ipAdress,
                :tcpPort
            )
            RETURNING VEHICLE_ID INTO :newId";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("vehicleName", OracleDbType.Varchar2).Value = vehicleName;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;
        cmd.Parameters.Add("ipAdress", OracleDbType.Varchar2).Value = ipAdress;
        cmd.Parameters.Add("tcpPort", OracleDbType.Int32).Value = tcpPort;

        var newIdParam = new OracleParameter("newId", OracleDbType.Decimal) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(newIdParam);

        await cmd.ExecuteNonQueryAsync();

        if (newIdParam.Value is Oracle.ManagedDataAccess.Types.OracleDecimal oraDec && !oraDec.IsNull)
            return oraDec.ToInt32();

        throw new InvalidOperationException("Nie udało się pobrać ID nowego pojazdu.");
    }

    /// <summary>
    /// Usuwa pojazd należący do wskazanego użytkownika.
    /// </summary>
    public async Task<bool> DeleteVehicleAsync(string username, int vehicleId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
            DELETE FROM VEHICLES
            WHERE VEHICLE_ID = :vehicleId
              AND USER_ID = (SELECT USER_ID FROM USERS WHERE USERNAME = :username)";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("vehicleId", OracleDbType.Int32).Value = vehicleId;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    /// <summary>
    /// Pobiera wszystkie pojazdy przypisane do użytkownika.
    /// </summary>
    public async Task<List<VehicleBaseDto>> GetUserVehiclesAsync(string username)
    {
        var vehicles = new List<VehicleBaseDto>();
        if (string.IsNullOrWhiteSpace(username)) return vehicles;

        const string sql = @"
            SELECT v.VEHICLE_ID, v.VEHICLE_NAME, v.IP_ADRESS, v.TCP_PORT
            FROM VEHICLES v
            JOIN USERS u ON v.USER_ID = u.USER_ID
            WHERE u.USERNAME = :username
            ORDER BY v.VEHICLE_ID";

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            vehicles.Add(new VehicleBaseDto(
                Convert.ToInt32(reader["VEHICLE_ID"]),
                reader["VEHICLE_NAME"]?.ToString() ?? "Bez nazwy",
                reader["IP_ADRESS"] is DBNull ? null : reader["IP_ADRESS"]?.ToString(),
                reader["TCP_PORT"] is DBNull ? null : Convert.ToInt32(reader["TCP_PORT"])
            ));
        }

        return vehicles;
    }
}
