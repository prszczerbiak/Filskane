using System.Data;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using Filskane.Models;

namespace Filskane.DAL;

/// <summary>
/// Warstwa dostępu do danych odpowiedzialna za zapisywanie raportów do tabeli RAPORTS.
/// </summary>
public class ReportDAL : BaseDAL
{
    private readonly ILogger<ReportDAL> _logger;

    public ReportDAL(IConfiguration configuration, ILogger<ReportDAL> logger)
        : base(configuration)
    {
        _logger = logger;
    }

    /// <summary>
    /// Pobiera identyfikator użytkownika na podstawie loginu.
    /// </summary>
    public async Task<int?> GetUserIdAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        const string sql = @"
            SELECT USER_ID
            FROM USERS
            WHERE USERNAME = :username";

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
                return null;

            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania USER_ID dla {Username}", username);
            throw;
        }
    }

    /// <summary>
    /// Zapisuje nowy raport w tabeli RAPORTS.
    /// </summary>
    public async Task<int> SaveReportAsync(int userId, string reportContent)
    {
        const string sql = @"
            INSERT INTO RAPORTS (USER_ID, RAPORT)
            VALUES (:userId, :raport)
            RETURNING RAPORT_ID INTO :reportId";

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("userId", OracleDbType.Int32).Value = userId;
            cmd.Parameters.Add("raport", OracleDbType.Clob).Value = reportContent;
            cmd.Parameters.Add("reportId", OracleDbType.Int32, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(cmd.Parameters["reportId"].Value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd zapisywania raportu dla USER_ID {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Aktualizuje treść istniejącego raportu.
    /// </summary>
    public async Task UpdateReportContentAsync(int reportId, string reportContent)
    {
        const string sql = @"
            UPDATE RAPORTS
            SET RAPORT = :raport
            WHERE RAPORT_ID = :reportId";

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("raport", OracleDbType.Clob).Value = reportContent;
            cmd.Parameters.Add("reportId", OracleDbType.Int32).Value = reportId;

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd aktualizacji treści raportu {ReportId}", reportId);
            throw;
        }
    }

    /// <summary>
    /// Oznacza raport jako zwalidowany.
    /// </summary>
    public async Task<bool> MarkReportAsValidatedAsync(int reportId)
    {
        const string sql = @"
            UPDATE RAPORTS
            SET VALIDATION = 'confirmed'
            WHERE RAPORT_ID = :reportId";

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("reportId", OracleDbType.Int32).Value = reportId;

            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd walidacji raportu {ReportId}", reportId);
            throw;
        }
    }

    /// <summary>
    /// Pobiera raporty zalogowanego użytkownika.
    /// </summary>
    public async Task<List<UserReportDto>> GetUserReportsAsync(string username)
    {
        var reports = new List<UserReportDto>();

        const string sql = @"
            SELECT r.RAPORT_ID, r.RAPORT, r.VALIDATION, r.USER_ID, u.USERNAME
            FROM RAPORTS r
            JOIN USERS u ON r.USER_ID = u.USER_ID
            WHERE u.USERNAME = :username
            ORDER BY r.RAPORT_ID DESC";

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var reportText = reader["RAPORT"]?.ToString() ?? string.Empty;
                int fieldId = 0;
                string fieldName = "Nieznane pole";
                DateTime? generatedAt = null;

                try
                {
                    if (!string.IsNullOrWhiteSpace(reportText))
                    {
                        using var json = JsonDocument.Parse(reportText);
                        var root = json.RootElement;
                        if (root.TryGetProperty("fieldId", out var fieldIdProp))
                            fieldId = fieldIdProp.GetInt32();
                        if (root.TryGetProperty("fieldName", out var fieldNameProp))
                            fieldName = fieldNameProp.GetString() ?? fieldName;
                        if (root.TryGetProperty("generatedAt", out var generatedAtProp) && generatedAtProp.ValueKind == JsonValueKind.String && DateTime.TryParse(generatedAtProp.GetString(), out var parsedDate))
                            generatedAt = parsedDate;
                    }
                }
                catch
                {
                    // Zachowujemy odporność na starsze rekordy z innym formatem.
                }

                reports.Add(new UserReportDto(
                    Convert.ToInt32(reader["RAPORT_ID"]),
                    reader["USERNAME"]?.ToString() ?? string.Empty,
                    fieldId,
                    fieldName,
                    generatedAt,
                    reader["VALIDATION"]?.ToString() ?? "pending"));
            }

            return reports;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania raportów dla użytkownika {Username}", username);
            throw;
        }
    }

    /// <summary>
    /// Pobiera wszystkie niezatwierdzone raporty dla organizacji walidacyjnej.
    /// </summary>
    public async Task<List<UserReportDto>> GetPendingReportsAsync()
    {
        var reports = new List<UserReportDto>();

        const string sql = @"
            SELECT r.RAPORT_ID, r.RAPORT, r.VALIDATION, r.USER_ID, u.USERNAME
            FROM RAPORTS r
            JOIN USERS u ON r.USER_ID = u.USER_ID
            WHERE r.VALIDATION = 'pending'
            ORDER BY r.RAPORT_ID DESC";

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var reportText = reader["RAPORT"]?.ToString() ?? string.Empty;
                int fieldId = 0;
                string fieldName = "Nieznane pole";
                DateTime? generatedAt = null;

                try
                {
                    if (!string.IsNullOrWhiteSpace(reportText))
                    {
                        using var json = JsonDocument.Parse(reportText);
                        var root = json.RootElement;
                        if (root.TryGetProperty("fieldId", out var fieldIdProp))
                            fieldId = fieldIdProp.GetInt32();
                        if (root.TryGetProperty("fieldName", out var fieldNameProp))
                            fieldName = fieldNameProp.GetString() ?? fieldName;
                        if (root.TryGetProperty("generatedAt", out var generatedAtProp) && generatedAtProp.ValueKind == JsonValueKind.String && DateTime.TryParse(generatedAtProp.GetString(), out var parsedDate))
                            generatedAt = parsedDate;
                    }
                }
                catch
                {
                }

                reports.Add(new UserReportDto(
                    Convert.ToInt32(reader["RAPORT_ID"]),
                    reader["USERNAME"]?.ToString() ?? string.Empty,
                    fieldId,
                    fieldName,
                    generatedAt,
                    reader["VALIDATION"]?.ToString() ?? "pending"));
            }

            return reports;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania niezatwierdzonych raportów");
            throw;
        }
    }

    /// <summary>
    /// Pobiera plik PDF raportu po identyfikatorze.
    /// </summary>
    public async Task<byte[]?> GetReportPdfAsync(int reportId)
    {
        const string sql = @"
            SELECT RAPORT
            FROM RAPORTS
            WHERE RAPORT_ID = :reportId";

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("reportId", OracleDbType.Int32).Value = reportId;

            var result = await cmd.ExecuteScalarAsync();
            var reportText = result?.ToString();
            if (string.IsNullOrWhiteSpace(reportText))
                return null;

            using var json = JsonDocument.Parse(reportText);
            if (!json.RootElement.TryGetProperty("pdfBase64", out var pdfBase64Prop))
                return null;

            var pdfBase64 = pdfBase64Prop.GetString();
            return string.IsNullOrWhiteSpace(pdfBase64) ? null : Convert.FromBase64String(pdfBase64);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania PDF raportu {ReportId}", reportId);
            throw;
        }
    }

    /// <summary>
    /// Ustawia status raportu na odrzucony.
    /// </summary>
    public async Task<bool> RejectReportAsync(int reportId)
    {
        const string sql = @"
            UPDATE RAPORTS
            SET VALIDATION = 'rejected'
            WHERE RAPORT_ID = :reportId";

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("reportId", OracleDbType.Int32).Value = reportId;

            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd odrzucania raportu {ReportId}", reportId);
            throw;
        }
    }
}
