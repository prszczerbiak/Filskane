using System.Data;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using Filskane.Models;

namespace Filskane.DAL;
/// <summary>
/// Warstwa dostępu do danych odpowiedzialna za operacje na skanach satelitarnych (GeoRaster/LOB).
/// </summary>
public class ScanDAL : BaseDAL
{
    public ScanDAL(IConfiguration configuration) : base(configuration)
    {
    }

    /// <summary>
    /// Zapisuje nowy skan satelitarny (obraz rastrowy) dla wskazanego pola.
    /// </summary>
    /// <param name="username">Nazwa użytkownika (właściciela pola).</param>
    /// <param name="rasterData">Dane obrazu w formacie bajtowym (TIFF).</param>
    /// <param name="fieldId">ID pola.</param>
    /// <param name="date">Data wykonania skanu.</param>
    /// <param name="bboxJson">Koordynaty Bounding Box w formacie JSON.</param>
    /// <exception cref="UnauthorizedAccessException">Gdy pole nie należy do użytkownika.</exception>
    public async Task SaveRasterAsync(string username, byte[] rasterData, int fieldId, DateTime date, string bboxJson)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        // 1. Weryfikacja własności pola przed rozpoczęciem ciężkiej transakcji
        using (var checkCmd = conn.CreateCommand())
        {
            checkCmd.CommandText = @"
                SELECT COUNT(*) 
                FROM FIELDS f 
                JOIN USERS u ON f.USER_ID = u.USER_ID 
                WHERE f.FIELD_ID = :fid AND u.USERNAME = :uname";

            checkCmd.Parameters.Add("fid", fieldId);
            checkCmd.Parameters.Add("uname", username);

            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            if (count == 0)
                throw new UnauthorizedAccessException("Brak dostępu do pola lub pole nie istnieje.");
        }

        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            int newId;

            await using (var initCmd = conn.CreateCommand())
            {
                initCmd.Transaction = (OracleTransaction)transaction;
                initCmd.CommandText = @"
                    INSERT INTO SATELLITE_SCANS (FIELD_ID, SCAN_DATE, RASTER, BBOX)
                    VALUES (:fieldId, :scanDate, MDSYS.SDO_GEOR.init('SATELLITE_SCANS_RDT'), :bbox)
                    RETURNING SCAN_ID INTO :newId";

                initCmd.Parameters.Add("fieldId", fieldId);
                initCmd.Parameters.Add("scanDate", date);
                initCmd.Parameters.Add("bbox", bboxJson);

                var newIdParam = new OracleParameter("newId", OracleDbType.Int32)
                {
                    Direction = ParameterDirection.Output
                };
                initCmd.Parameters.Add(newIdParam);

                await initCmd.ExecuteNonQueryAsync();

                if (newIdParam.Value is OracleDecimal d && !d.IsNull)
                {
                    newId = d.ToInt32();
                }
                else
                {
                    throw new InvalidOperationException("Nie udało się pobrać ID nowego skanu.");
                }
            }

            await using (var importCmd = conn.CreateCommand())
            {
                importCmd.Transaction = (OracleTransaction)transaction;
                importCmd.CommandText = @"
                    DECLARE
                        v_geor MDSYS.SDO_GEORASTER;
                    BEGIN
                        SELECT RASTER INTO v_geor FROM SATELLITE_SCANS WHERE SCAN_ID = :id FOR UPDATE;
                        SDO_GEOR.importFrom(v_geor, '', 'TIFF', :blob);
                        UPDATE SATELLITE_SCANS SET RASTER = v_geor WHERE SCAN_ID = :id;
                    END;";

                importCmd.Parameters.Add("id", newId);
                importCmd.Parameters.Add("blob", OracleDbType.Blob).Value = rasterData;

                await importCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new Exception($"Błąd zapisu rastra w Oracle: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Pobiera najnowszy dostępny skan dla danego pola, weryfikując uprawnienia użytkownika.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">ID pola.</param>
    /// <returns>Obiekt z danymi skanu lub null, jeśli brak skanów.</returns>
    public async Task<ScanResultDto?> GetLatestScanAsync(string username, int fieldId)
    {
        const string securityClause = @"
            WHERE field_id = :id 
            AND field_id IN (
                SELECT f.field_id 
                FROM fields f 
                JOIN users u ON f.user_id = u.user_id 
                WHERE u.username = :username
            )
            ORDER BY scan_date DESC FETCH FIRST 1 ROWS ONLY";

        return await GetScanInternalAsync(securityClause, fieldId, username);
    }

    /// <summary>
    /// Pobiera konkretny skan na podstawie ID, weryfikując uprawnienia użytkownika.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="scanId">ID skanu.</param>
    /// <returns>Obiekt z danymi skanu lub null.</returns>
    public async Task<ScanResultDto?> GetScanByIdAsync(string username, int scanId)
    {
        const string securityClause = @"
            WHERE scan_id = :id 
            AND field_id IN (
                SELECT f.field_id 
                FROM fields f 
                JOIN users u ON f.user_id = u.user_id 
                WHERE u.username = :username
            )";

        return await GetScanInternalAsync(securityClause, scanId, username);
    }

    /// <summary>
    /// Pobiera listę nagłówków wszystkich skanów dla danego pola.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">ID pola.</param>
    /// <returns>Lista skróconych informacji o skanach.</returns>
    public async Task<List<ScanSummaryDto>> GetFieldScansAsync(string username, int fieldId)
    {
        var list = new List<ScanSummaryDto>();
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
            SELECT s.SCAN_ID, s.FIELD_ID, s.SCAN_DATE 
            FROM SATELLITE_SCANS s
            JOIN FIELDS f ON s.FIELD_ID = f.FIELD_ID
            JOIN USERS u ON f.USER_ID = u.USER_ID
            WHERE s.FIELD_ID = :id AND u.USERNAME = :username
            ORDER BY s.SCAN_DATE DESC";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("id", fieldId);
        cmd.Parameters.Add("username", username);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ScanSummaryDto(
                Convert.ToInt32(reader["SCAN_ID"]),
                Convert.ToInt32(reader["FIELD_ID"]),
                reader.GetDateTime(reader.GetOrdinal("SCAN_DATE"))
            ));
        }
        return list;
    }

    /// <summary>
    /// Usuwa skan satelitarny.
    /// </summary>
    /// <param name="username">Nazwa użytkownika (do weryfikacji uprawnień).</param>
    /// <param name="scanId">ID skanu.</param>
    /// <returns>True, jeśli skan został usunięty.</returns>
    public async Task<bool> DeleteScanAsync(string username, int scanId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
            DELETE FROM SATELLITE_SCANS 
            WHERE SCAN_ID = :id 
            AND FIELD_ID IN (
                SELECT FIELD_ID FROM FIELDS f 
                JOIN USERS u ON f.USER_ID = u.USER_ID 
                WHERE u.USERNAME = :username
            )";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("id", scanId);
        cmd.Parameters.Add("username", username);

        return (await cmd.ExecuteNonQueryAsync()) > 0;
    }

    /// <summary>
    /// Wewnętrzna metoda pomocnicza wykonująca blok PL/SQL w celu eksportu obiektu GeoRaster do formatu binarnego (TIFF).
    /// </summary>
    /// <param name="whereClause">Fragment zapytania SQL (klauzula WHERE) filtrujący odpowiedni rekord.</param>
    /// <param name="idParam">Wartość identyfikatora (ScanId lub FieldId) bindowana do parametru :id.</param>
    /// <param name="username">Nazwa użytkownika bindowana do parametru :username (do weryfikacji uprawnień).</param>
    /// <returns>
    /// Obiekt <see cref="ScanResultDto"/> zawierający bajty obrazu i metadane, 
    /// lub <c>null</c> jeśli nie znaleziono danych.
    /// </returns
    private async Task<ScanResultDto?> GetScanInternalAsync(string whereClause, int idParam, string username)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        string sql = $@"
            DECLARE
                gr MDSYS.SDO_GEORASTER;
                out_blob BLOB;
                v_date DATE;
                v_bbox VARCHAR2(4000);
            BEGIN
                BEGIN
                    SELECT raster, scan_date, bbox
                    INTO gr, v_date, v_bbox
                    FROM satellite_scans
                    {whereClause}; 
                EXCEPTION WHEN NO_DATA_FOUND THEN
                    gr := NULL;
                END;

                IF gr IS NOT NULL THEN
                    DBMS_LOB.CREATETEMPORARY(out_blob, TRUE);
                    sdo_geor.exportTo(gr, '', 'TIFF', out_blob);
                    :result := out_blob;
                ELSE
                    :result := NULL;
                END IF;

                :scanDate := v_date;
                :bboxInfo := v_bbox;
            END;";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;

        cmd.Parameters.Add("id", OracleDbType.Int32).Value = idParam;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

        cmd.Parameters.Add("result", OracleDbType.Blob).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("scanDate", OracleDbType.Date).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("bboxInfo", OracleDbType.Varchar2, 4000).Direction = ParameterDirection.Output;

        await cmd.ExecuteNonQueryAsync();

        var blobVal = cmd.Parameters["result"].Value as OracleBlob;
        if (blobVal == null || blobVal.IsNull) return null;

        byte[] imageBytes = blobVal.Value;

        var dateVal = cmd.Parameters["scanDate"].Value;
        DateTime date = (dateVal is OracleDate od && !od.IsNull) ? od.Value : DateTime.MinValue;

        string? bboxJson = cmd.Parameters["bboxInfo"].Value?.ToString();
        Bbox? bbox = Bbox.FromJson(bboxJson);

        return new ScanResultDto(date, imageBytes, bbox);
    }
}
