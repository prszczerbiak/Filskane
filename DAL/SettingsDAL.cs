using Oracle.ManagedDataAccess.Client;
using Filskane.Models;

namespace Filskane.DAL;
/// <summary>
/// Warstwa dostępu do danych odpowiedzialna za pobieranie i edycję ustawień profilu użytkownika.
/// </summary>
public class SettingsDAL : BaseDAL
{
    private readonly ILogger<SettingsDAL> _logger;

    public SettingsDAL(IConfiguration configuration, ILogger<SettingsDAL> logger)
        : base(configuration)
    {
        _logger = logger;
    }

    /// <summary>
    /// Pobiera pełne szczegóły profilu użytkownika (do edycji).
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <returns>Obiekt DTO ze szczegółami lub null, jeśli użytkownik nie istnieje.</returns>
    public async Task<UserDetailDto?> GetLongInfoAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        const string sql = @"
            SELECT USERNAME, FIRST_NAME, EMAIL, TELEPHONE, FARM_LONGITUDE, FARM_LATITUDE
            FROM USERS
            WHERE USERNAME = :username";

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych szczegółowych dla {Username}", username);
            throw;
        }
    }

    /// <summary>
    /// Pobiera skrócone informacje o użytkowniku (np. imię, preferencje motywu).
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <returns>Obiekt DTO z podstawowymi ustawieniami.</returns>
    public async Task<UserShortDto?> GetShortInfoAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        const string sql = @"
            SELECT FIRST_NAME, IS_DARK_MODE, SURFACE_UNIT, FARM_LONGITUDE, FARM_LATITUDE, ACCOUNT_TYPE
            FROM USERS
            WHERE USERNAME = :username";

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

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
                    reader["FARM_LATITUDE"] is DBNull ? null : Convert.ToDouble(reader["FARM_LATITUDE"]),
                    reader["ACCOUNT_TYPE"]?.ToString() ?? "USER"
                );
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych skróconych dla {Username}", username);
            throw;
        }
    }

    /// <summary>
    /// Aktualizuje preferowaną jednostkę powierzchni.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="surface">Nowa wartość jednostki (int).</param>
    public async Task UpdateSurfaceAsync(string username, int surface)
        => await UpdateUserFieldAsync(username, "SURFACE_UNIT", surface);

    /// <summary>
    /// Aktualizuje preferencje motywu (Jasny/Ciemny).
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="theme">Wartość motywu (0 lub 1).</param>
    public async Task UpdateThemeAsync(string username, int theme)
        => await UpdateUserFieldAsync(username, "IS_DARK_MODE", theme);

    /// <summary>
    /// Aktualizuje imię użytkownika.
    /// </summary>
    /// <param name="username">Nazwa użytkownika (identyfikator).</param>
    /// <param name="name">Nowe imię.</param>
    public async Task UpdateFirstNameAsync(string username, string name)
        => await UpdateUserFieldAsync(username, "FIRST_NAME", name);

    /// <summary>
    /// Aktualizuje adres e-mail użytkownika.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="email">Nowy adres e-mail.</param>
    public async Task UpdateEmailAsync(string username, string email)
        => await UpdateUserFieldAsync(username, "EMAIL", email);

    /// <summary>
    /// Aktualizuje numer telefonu użytkownika.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="phone">Nowy numer telefonu.</param>
    public async Task UpdatePhoneAsync(string username, string phone)
        => await UpdateUserFieldAsync(username, "TELEPHONE", phone);

    /// <summary>
    /// Metoda pomocnicza do bezpiecznej aktualizacji pojedynczej kolumny w tabeli USERS.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="columnName">Nazwa kolumny do zaktualizowania.</param>
    /// <param name="value">Nowa wartość.</param>
    /// <remarks>
    /// Parametr <paramref name="columnName"/> jest wstawiany bezpośrednio do zapytania SQL. 
    /// Należy upewnić się, że pochodzi on z bezpiecznego źródła (jest hardcoded w kodzie), 
    /// a nie z danych wejściowych użytkownika, aby uniknąć SQL Injection.
    /// </remarks>
    private async Task UpdateUserFieldAsync(string username, string columnName, object value)
    {
        string sql = $"UPDATE USERS SET {columnName} = :val WHERE USERNAME = :username";

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(sql, conn);

            cmd.Parameters.Add("val", value);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd aktualizacji pola {Column} dla {Username}", columnName, username);
            throw;
        }
    }
}
