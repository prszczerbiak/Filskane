namespace WebApplication1.DAL;

using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using WebApplication1.Models;
using WebApplication1.Utils;

public class FieldDAL : BaseDAL
{
    public FieldDAL(IConfiguration configuration) : base(configuration)
    {
    }

    // ==============================================================================
    // CZĘŚĆ 1: ZARZĄDZANIE POLAMI (CRUD)
    // ==============================================================================

    public async Task<int> SaveFieldAsync(string username, string name, string geojson, double centerX, double centerY, double area, string complex, string type, string substrate)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        string sql = @"INSERT INTO FIELDS (
                            FIELD_NAME, CENTER_LONGITUDE, CENTER_LATITUDE, USER_ID, 
                            GEO_JSON, AREA_M2, SOIL_COMPLEX, SOIL_TYPE, SOIL_SUBSTRATE
                       )
                       VALUES (
                            :name, :centerX, :centerY, 
                            (SELECT USER_ID FROM USERS WHERE USERNAME = :username), 
                            :geojson, :area, :soilComplex, :soilType, :soilSubstrate
                       )
                       RETURNING FIELD_ID INTO :newId";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("name", OracleDbType.Varchar2).Value = name;
        cmd.Parameters.Add("centerX", OracleDbType.Double).Value = centerX;
        cmd.Parameters.Add("centerY", OracleDbType.Double).Value = centerY;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;
        cmd.Parameters.Add("geojson", OracleDbType.Clob).Value = geojson;
        cmd.Parameters.Add("area", OracleDbType.Double).Value = area;
        cmd.Parameters.Add("soilComplex", OracleDbType.Varchar2).Value = complex;
        cmd.Parameters.Add("soilType", OracleDbType.Varchar2).Value = type;
        cmd.Parameters.Add("soilSubstrate", OracleDbType.Varchar2).Value = substrate;

        var newIdParam = new OracleParameter("newId", OracleDbType.Decimal) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(newIdParam);

        try
        {
            await cmd.ExecuteNonQueryAsync();
            if (newIdParam.Value is OracleDecimal oraDec && !oraDec.IsNull) return oraDec.ToInt32();
            throw new Exception("Nie udało się pobrać ID nowego pola.");
        }
        catch (OracleException ex)
        {
            throw new Exception($"Błąd zapisu pola: {ex.Message}", ex);
        }
    }

    public async Task<List<FieldShortDto>> GetUserFieldsAsync(string username)
    {
        var fields = new List<FieldShortDto>();
        if (string.IsNullOrWhiteSpace(username)) return fields;

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            string sql = @"SELECT f.FIELD_ID, f.FIELD_NAME, f.CENTER_LONGITUDE, f.CENTER_LATITUDE, f.GEO_JSON
                           FROM FIELDS f
                           JOIN USERS u ON f.USER_ID = u.USER_ID
                           WHERE u.USERNAME = :username";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                fields.Add(new FieldShortDto(
                    Convert.ToInt32(reader["FIELD_ID"]),
                    reader["FIELD_NAME"]?.ToString() ?? "Bez nazwy",
                    Convert.ToDouble(reader["CENTER_LONGITUDE"]),
                    Convert.ToDouble(reader["CENTER_LATITUDE"]),
                    reader["GEO_JSON"]?.ToString() ?? ""
                ));
            }
            return fields;
        }
        catch (Exception ex)
        {
            throw new Exception($"Błąd pobierania pól: {ex.Message}", ex);
        }
    }

    public async Task<List<FieldListItemDto>> GetFieldListAsync(string username)
    {
        var list = new List<FieldListItemDto>();
        if (string.IsNullOrWhiteSpace(username)) return list;

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            string sql = @"SELECT f.FIELD_ID, f.FIELD_NAME
                           FROM FIELDS f
                           JOIN USERS u ON f.USER_ID = u.USER_ID
                           WHERE u.USERNAME = :username
                           ORDER BY f.FIELD_NAME";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new FieldListItemDto(
                    Convert.ToInt32(reader["FIELD_ID"]),
                    reader["FIELD_NAME"]?.ToString() ?? "Bez nazwy"
                ));
            }
            return list;
        }
        catch (Exception ex) { throw new Exception($"Błąd listy pól: {ex.Message}", ex); }
    }

    public async Task<FieldDetailDto?> GetUserFieldByIdAsync(string username, int fieldId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        string sql = @"SELECT f.FIELD_ID, f.FIELD_NAME, f.CROP_ID, p.PLANT_NAME, f.PLANT_STATE, 
                              g.CYCLE_NAME, f.SOWING_DATE, sc.COMPLEX_NAME, st.TYPE_NAME, 
                              ss.SUBSTRATE_NAME, f.AREA_M2, f.GEO_JSON
                       FROM FIELDS f
                       LEFT JOIN PLANTS p ON f.CROP_ID = p.PLANT_ID
                       LEFT JOIN GROWTH_CYCLES g ON f.PLANT_STATE = g.CYCLE_ID
                       LEFT JOIN SOIL_COMPLEXES sc ON f.SOIL_COMPLEX = sc.COMPLEX_CODE
                       LEFT JOIN SOIL_TYPES st ON f.SOIL_TYPE = st.TYPE_CODE
                       LEFT JOIN SOIL_SUBSTRATES ss ON f.SOIL_SUBSTRATE = ss.SUBSTRATE_CODE
                       JOIN USERS u ON f.USER_ID = u.USER_ID 
                       WHERE f.FIELD_ID = :fieldId AND u.USERNAME = :username";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("fieldId", OracleDbType.Int32).Value = fieldId;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                string geoJson = reader["GEO_JSON"]?.ToString() ?? "";
                string bbox = GeoUtils.GetBboxFromGeoJson(geoJson)?.ToString() ?? "";

                return new FieldDetailDto(
                    Convert.ToInt32(reader["FIELD_ID"]),
                    GetSafeString(reader["FIELD_NAME"]) ?? "Bez nazwy",
                    reader["CROP_ID"] is DBNull ? null : Convert.ToInt32(reader["CROP_ID"]),
                    GetSafeString(reader["PLANT_NAME"]),
                    reader["PLANT_STATE"] is DBNull ? null : Convert.ToInt32(reader["PLANT_STATE"]),
                    GetSafeString(reader["CYCLE_NAME"]),
                    reader["SOWING_DATE"] is DBNull ? null : Convert.ToDateTime(reader["SOWING_DATE"]),
                    GetSafeString(reader["COMPLEX_NAME"]),
                    GetSafeString(reader["TYPE_NAME"]),
                    GetSafeString(reader["SUBSTRATE_NAME"]),
                    Convert.ToDouble(reader["AREA_M2"] is DBNull ? 0 : reader["AREA_M2"]),
                    geoJson, bbox
                );
            }
            return null;
        }
        catch (Exception ex) { throw new Exception($"Błąd szczegółów pola: {ex.Message}", ex); }
    }

    public async Task SaveFieldChangesAsync(int fieldId, UpdateFieldRequest dto)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.BindByName = true;

        bool canCalculateCycle = (dto.CropId > 0) && (dto.SowingDate.HasValue);
        StringBuilder sql = new StringBuilder();
        sql.Append("UPDATE FIELDS SET CROP_ID = :crop, SOWING_DATE = :dateSow, ");

        if (canCalculateCycle)
        {
            sql.Append(@"PLANT_STATE = (SELECT CYCLE_ID FROM PLANT_STATES WHERE PLANT_ID = :cropSub 
                         AND :dateSowSub + MIN_DAYS <= SYSDATE AND :dateSowSub + MAX_DAYS >= SYSDATE FETCH FIRST 1 ROWS ONLY) ");
        }
        else
        {
            sql.Append("PLANT_STATE = NULL ");
        }
        sql.Append("WHERE FIELD_ID = :fieldId");

        cmd.CommandText = sql.ToString();
        cmd.Parameters.Add("crop", OracleDbType.Int32).Value = (dto.CropId > 0) ? dto.CropId : DBNull.Value;
        cmd.Parameters.Add("dateSow", OracleDbType.Date).Value = dto.SowingDate.HasValue ? dto.SowingDate.Value : DBNull.Value;
        cmd.Parameters.Add("fieldId", OracleDbType.Int32).Value = fieldId;

        if (canCalculateCycle)
        {
            cmd.Parameters.Add("cropSub", OracleDbType.Int32).Value = dto.CropId;
            cmd.Parameters.Add("dateSowSub", OracleDbType.Date).Value = dto.SowingDate.Value;
        }

        if (await cmd.ExecuteNonQueryAsync() == 0) throw new Exception("Nie znaleziono pola.");
    }

    public async Task DeleteFieldAsync(string username, int fieldId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        string sql = "DELETE FROM FIELDS WHERE FIELD_ID = :fieldId AND USER_ID = (SELECT USER_ID FROM USERS WHERE USERNAME = :username)";
        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("fieldId", OracleDbType.Int32).Value = fieldId;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

        if (await cmd.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException($"Pole {fieldId} nie istnieje lub brak uprawnień.");
    }

    // ==============================================================================
    // CZĘŚĆ 2: SŁOWNIKI (PRZENIESIONE Z DictionaryDAL)
    // ==============================================================================

    public async Task<List<PlantDto>> GetPlantsAsync()
    {
        var list = new List<PlantDto>();
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        string sql = "SELECT PLANT_ID, PLANT_NAME FROM PLANTS ORDER BY PLANT_NAME";
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new PlantDto(Convert.ToInt32(reader["PLANT_ID"]), reader["PLANT_NAME"].ToString() ?? ""));
        }
        return list;
    }

    public async Task<List<ThresholdDto>> GetThresholdsAsync()
    {
        var list = new List<ThresholdDto>();
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        string sql = "SELECT CYCLE_ID, MIN_NDVI, MAX_NDVI FROM GROWTH_CYCLES";
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ThresholdDto(Convert.ToInt32(reader["CYCLE_ID"]), Convert.ToDouble(reader["MIN_NDVI"]), Convert.ToDouble(reader["MAX_NDVI"])));
        }
        return list;
    }

    public async Task<CycleDto?> GetCycleByIdAsync(int fieldId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        string sql = @"SELECT g.CYCLE_ID, g.CYCLE_NAME FROM FIELDS f 
                       JOIN GROWTH_CYCLES g ON f.PLANT_STATE = g.CYCLE_ID WHERE f.FIELD_ID = :fieldId";
        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("fieldId", OracleDbType.Int32).Value = fieldId;
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new CycleDto(Convert.ToInt32(reader["CYCLE_ID"]), reader["CYCLE_NAME"].ToString() ?? "");
        }
        return null;
    }

    private string? GetSafeString(object value)
    {
        if (value == null || value is DBNull) return null;
        string str = value.ToString()?.Trim() ?? "";
        return string.IsNullOrEmpty(str) ? null : str;
    }
}