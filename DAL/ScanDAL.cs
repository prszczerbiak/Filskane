namespace WebApplication1.DAL;

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using WebApplication1.Models;

public class ScanDAL : BaseDAL
{
    public ScanDAL(IConfiguration configuration) : base(configuration)
    {
    }

    /// <summary>
    /// Zapisuje obraz rastrowy (TIFF) do bazy Oracle jako GeoRaster.
    /// Wymaga transakcji, ponieważ najpierw inicjalizujemy obiekt, a potem ładujemy dane.
    /// </summary>
    public async Task SaveRasterAsync(byte[] rasterData, int fieldId, DateTime date, string bboxJson)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        // Transakcja jest niezbędna przy operacjach LOB/GeoRaster
        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            int newId;

            // KROK 1: Insert pustego obiektu GeoRaster i pobranie nowego ID
            await using (var initCmd = conn.CreateCommand())
            {
                initCmd.Transaction = (OracleTransaction)transaction;
                initCmd.CommandText = @"
                    INSERT INTO SATELLITE_SCANS (FIELD_ID, SCAN_DATE, RASTER, BBOX)
                    VALUES (:fieldId, :scanDate, MDSYS.SDO_GEOR.init('SATELLITE_SCANS_RDT'), :bbox)
                    RETURNING SCAN_ID INTO :newId";

                initCmd.Parameters.Add("fieldId", fieldId);
                initCmd.Parameters.Add("scanDate", date);
                initCmd.Parameters.Add("bbox", bboxJson); // Oracle 19c+ obsługuje JSON w Varchar2/Blob

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
                    throw new Exception("Nie udało się pobrać ID nowego skanu.");
                }
            }

            // KROK 2: Import fizycznego pliku TIFF do zainicjowanego GeoRastera
            await using (var importCmd = conn.CreateCommand())
            {
                importCmd.Transaction = (OracleTransaction)transaction;

                // Blok PL/SQL wykonujący import
                importCmd.CommandText = @"
                    DECLARE
                      v_geor MDSYS.SDO_GEORASTER;
                    BEGIN
                      -- Blokowanie wiersza do edycji (SELECT FOR UPDATE)
                      SELECT RASTER INTO v_geor FROM SATELLITE_SCANS WHERE SCAN_ID = :id FOR UPDATE;

                      -- Import danych binarnych do obiektu GeoRaster
                      SDO_GEOR.importFrom(v_geor, '', 'TIFF', :blob);

                      -- Aktualizacja metadanych w tabeli
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
    /// Pobiera najnowszy skan dla danego pola.
    /// </summary>
    public async Task<ScanResultDto?> GetLatestScanAsync(int fieldId)
    {
        // Używamy helpera do wywołania logiki pobierania
        return await GetScanInternalAsync("WHERE field_id = :id ORDER BY scan_date DESC FETCH FIRST 1 ROWS ONLY", fieldId);
    }

    /// <summary>
    /// Pobiera konkretny skan po ID.
    /// </summary>
    public async Task<ScanResultDto?> GetScanByIdAsync(int scanId)
    {
        return await GetScanInternalAsync("WHERE scan_id = :id", scanId);
    }

    /// <summary>
    /// Prywatna metoda wykonująca PL/SQL do pobrania i eksportu GeoRastera do BLOBa.
    /// </summary>
    private async Task<ScanResultDto?> GetScanInternalAsync(string whereClause, int idParam)
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
        cmd.BindByName = true; // Kluczowe dla bloków PL/SQL z parametrami
        cmd.Parameters.Add("id", OracleDbType.Int32).Value = idParam;

        // Parametry wyjściowe
        cmd.Parameters.Add("result", OracleDbType.Blob).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("scanDate", OracleDbType.Date).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("bboxInfo", OracleDbType.Varchar2, 4000).Direction = ParameterDirection.Output;

        await cmd.ExecuteNonQueryAsync();

        // Odczyt wyników
        var blobVal = cmd.Parameters["result"].Value as OracleBlob;
        if (blobVal == null || blobVal.IsNull) return null;

        byte[] imageBytes = blobVal.Value; // Kopiuje dane z OracleBlob do pamięci RAM

        var dateVal = cmd.Parameters["scanDate"].Value;
        DateTime date = (dateVal is OracleDate od && !od.IsNull) ? od.Value : DateTime.MinValue;

        string bbox = cmd.Parameters["bboxInfo"].Value?.ToString() ?? "";

        return new ScanResultDto(date, imageBytes, bbox);
    }

    /// <summary>
    /// Pobiera listę dostępnych skanów dla pola (bez ciężkich danych binarnych).
    /// </summary>
    public async Task<List<ScanSummaryDto>> GetFieldScansAsync(int fieldId)
    {
        var list = new List<ScanSummaryDto>();
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        string sql = "SELECT SCAN_ID, FIELD_ID, SCAN_DATE FROM SATELLITE_SCANS WHERE FIELD_ID = :id ORDER BY SCAN_DATE DESC";
        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("id", fieldId);

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
    /// Usuwa skan z bazy danych.
    /// </summary>
    public async Task<bool> DeleteScanAsync(int scanId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        // Jeśli baza jest poprawnie skonfigurowana, usunięcie wiersza usunie też dane rastrowe (GeoRaster cleanup)
        string sql = "DELETE FROM SATELLITE_SCANS WHERE SCAN_ID = :id";
        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("id", scanId);

        return (await cmd.ExecuteNonQueryAsync()) > 0;
    }
}