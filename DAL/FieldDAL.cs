using System.Data;
using System.Text;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using Filskane.Models;
using Filskane.Utils;

namespace Filskane.DAL;
/// <summary>
/// Warstwa dostępu do danych odpowiedzialna za operacje na polach uprawnych (CRUD, słowniki, przypisywanie upraw).
/// </summary>
public class FieldDAL : BaseDAL
{
    public FieldDAL(IConfiguration configuration) : base(configuration)
    {
    }

    /// <summary>
    /// Tworzy nowe pole uprawne w bazie danych i zwraca jego ID.
    /// </summary>
    /// <param name="username">Nazwa właściciela pola.</param>
    /// <param name="name">Nazwa pola.</param>
    /// <param name="geojson">Geometria w formacie GeoJSON.</param>
    /// <param name="centerX">Współrzędna X środka pola.</param>
    /// <param name="centerY">Współrzędna Y środka pola.</param>
    /// <param name="area">Powierzchnia w m2.</param>
    /// <param name="complex">Kategoria kompleksu glebowego.</param>
    /// <param name="type">Typ gleby.</param>
    /// <param name="substrate">Podłoże glebowe.</param>
    /// <returns>ID nowo utworzonego pola.</returns>
    public async Task<int> SaveFieldAsync(string username, string name, string geojson, double centerX, double centerY, double area, string complex, string type, string substrate)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
            INSERT INTO FIELDS (
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

            if (newIdParam.Value is OracleDecimal oraDec && !oraDec.IsNull)
                return oraDec.ToInt32();

            throw new InvalidOperationException("Nie udało się pobrać ID nowego pola.");
        }
        catch (OracleException ex)
        {
            throw new Exception($"Błąd zapisu pola: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Pobiera listę wszystkich pól należących do użytkownika (wersja skrócona).
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <returns>Lista obiektów DTO z podstawowymi danymi pól.</returns>
    public async Task<List<FieldShortDto>> GetUserFieldsAsync(string username)
    {
        var fields = new List<FieldShortDto>();
        if (string.IsNullOrWhiteSpace(username)) return fields;

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT f.FIELD_ID, f.FIELD_NAME, f.CENTER_LONGITUDE, f.CENTER_LATITUDE, f.GEO_JSON
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

    /// <summary>
    /// Pobiera uproszczoną listę pól (ID + Nazwa) do wyświetlenia w menu.
    /// </summary>
    /// <param name="username">Nazwa użytkownika, dla którego pobieramy listę.</param>
    /// <returns>Lista prostych obiektów DTO zawierających tylko ID i nazwę pola.</returns>
    public async Task<List<FieldListItemDto>> GetFieldListAsync(string username)
    {
        var list = new List<FieldListItemDto>();
        if (string.IsNullOrWhiteSpace(username)) return list;

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            const string sql = @"
                SELECT f.FIELD_ID, f.FIELD_NAME
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
        catch (Exception ex)
        {
            throw new Exception($"Błąd listy pól: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Pobiera szczegółowe informacje o polu na podstawie ID, weryfikując własność.
    /// </summary>
    /// <param name="username">Nazwa użytkownika próbującego uzyskać dostęp.</param>
    /// <param name="fieldId">Identyfikator pola.</param>
    /// <returns>Obiekt szczegółowy pola lub null, jeśli pole nie istnieje/nie należy do użytkownika.</returns>
    public async Task<FieldDetailDto?> GetUserFieldByIdAsync(string username, int fieldId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
            SELECT f.FIELD_ID, f.FIELD_NAME, f.CROP_ID, p.PLANT_NAME, f.PLANT_STATE, 
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
                Bbox? bbox = GeoUtils.GetBboxFromGeoJson(geoJson);

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
                    geoJson,
                    bbox
                );
            }
            return null;
        }
        catch (Exception ex)
        {
            throw new Exception($"Błąd szczegółów pola: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Aktualizuje dane pola (uprawa, data zasiewu) i przelicza fazę wzrostu.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">Identyfikator pola.</param>
    /// <param name="dto">Obiekt zawierający nowe dane (CropId, SowingDate).</param>
    public async Task SaveFieldChangesAsync(string username, int fieldId, UpdateFieldRequest dto)
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
            // Automatyczne wyliczanie ID cyklu na podstawie daty zasiewu i bieżącej daty
            sql.Append(@"
                PLANT_STATE = (
                    SELECT CYCLE_ID FROM PLANT_STATES 
                    WHERE PLANT_ID = :cropSub 
                        AND :dateSowSub + MIN_DAYS <= SYSDATE 
                        AND :dateSowSub + MAX_DAYS >= SYSDATE 
                    FETCH FIRST 1 ROWS ONLY
                ) ");
        }
        else
        {
            sql.Append("PLANT_STATE = NULL ");
        }

        sql.Append(@"
            WHERE FIELD_ID = :fieldId 
                AND USER_ID = (SELECT USER_ID FROM USERS WHERE USERNAME = :username)");

        cmd.CommandText = sql.ToString();

        cmd.Parameters.Add("crop", OracleDbType.Int32).Value = (dto.CropId > 0) ? dto.CropId : DBNull.Value;
        cmd.Parameters.Add("dateSow", OracleDbType.Date).Value = dto.SowingDate.HasValue ? dto.SowingDate.Value : DBNull.Value;

        if (canCalculateCycle)
        {
            cmd.Parameters.Add("cropSub", OracleDbType.Int32).Value = dto.CropId;
            cmd.Parameters.Add("dateSowSub", OracleDbType.Date).Value = dto.SowingDate!.Value;
        }

        cmd.Parameters.Add("fieldId", OracleDbType.Int32).Value = fieldId;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

        if (await cmd.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException("Nie znaleziono pola lub brak uprawnień.");
    }

    /// <summary>
    /// Usuwa pole należące do wskazanego użytkownika.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">ID pola do usunięcia.</param>
    public async Task DeleteFieldAsync(string username, int fieldId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
            DELETE FROM FIELDS 
            WHERE FIELD_ID = :fieldId 
                AND USER_ID = (SELECT USER_ID FROM USERS WHERE USERNAME = :username)";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("fieldId", OracleDbType.Int32).Value = fieldId;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

        if (await cmd.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException($"Pole {fieldId} nie istnieje lub brak uprawnień.");
    }

    /// <summary>
    /// Pobiera słownik dostępnych roślin uprawnych.
    /// </summary>
    /// <returns>Lista roślin dostępnych w systemie.</returns>
    public async Task<List<PlantDto>> GetPlantsAsync()
    {
        var list = new List<PlantDto>();
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = "SELECT PLANT_ID, PLANT_NAME FROM PLANTS ORDER BY PLANT_NAME";

        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new PlantDto(Convert.ToInt32(reader["PLANT_ID"]), reader["PLANT_NAME"].ToString() ?? ""));
        }
        return list;
    }

    /// <summary>
    /// Pobiera progi NDVI dla poszczególnych cykli wzrostu.
    /// </summary>
    /// <returns>Lista progów NDVI dla cykli.</returns>
    // <summary>
    /// Pobiera progi wartości indeksów (np. NDVI) dla konkretnej rośliny i wszystkich jej cykli.
    /// </summary>
    /// <param name="plantId">ID rośliny uprawnej.</param>
    /// <param name="indexType">Typ wskaźnika (domyślnie 'NDVI').</param>
    /// <returns>Lista progów dla cykli danej rośliny.</returns>
    public async Task<ThresholdDto?> GetThresholdAsync(int plantId, int cycleId, string indexType = "NDVI")
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        // Dodano CYCLE_ID do warunków WHERE, nie pobieramy nazwy cyklu bo to zbędne do obliczeń
        const string sql = @"
        SELECT MIN_VALUE, MAX_VALUE 
        FROM PLANT_THRESHOLDS 
        WHERE PLANT_ID = :plantId 
          AND CYCLE_ID = :cycleId 
          AND INDEX_TYPE = :indexType";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("plantId", OracleDbType.Int32).Value = plantId;
        cmd.Parameters.Add("cycleId", OracleDbType.Int32).Value = cycleId;
        cmd.Parameters.Add("indexType", OracleDbType.Varchar2).Value = indexType.ToUpper();

        await using var reader = await cmd.ExecuteReaderAsync();

        // Używamy if zamiast while, bo kombinacja kluczy gwarantuje maksymalnie 1 wynik
        if (await reader.ReadAsync())
        {
            return new ThresholdDto(
                cycleId, // Przekazujemy z powrotem ID cyklu z parametru dla spójności struktury DTO
                Convert.ToDouble(reader["MIN_VALUE"]),
                Convert.ToDouble(reader["MAX_VALUE"])
            );
        }

        // Zwracamy null, jeśli w bazie nie ma klamer dla tej dziwnej kombinacji
        return null;
    }

    /// <summary>
    /// Pobiera informacje o aktualnym cyklu wzrostu dla pola.
    /// </summary>
    /// <param name="username">Nazwa użytkownika (właściciela).</param>
    /// <param name="fieldId">Identyfikator pola.</param>
    /// <returns>Obiekt z informacjami o cyklu lub null.</returns>
    public async Task<CycleDto?> GetCycleByIdAsync(string username, int fieldId)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        const string sql = @"
            SELECT g.CYCLE_ID, g.CYCLE_NAME 
            FROM FIELDS f 
            JOIN GROWTH_CYCLES g ON f.PLANT_STATE = g.CYCLE_ID 
            JOIN USERS u ON f.USER_ID = u.USER_ID
            WHERE f.FIELD_ID = :fieldId AND u.USERNAME = :username";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("fieldId", OracleDbType.Int32).Value = fieldId;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

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
