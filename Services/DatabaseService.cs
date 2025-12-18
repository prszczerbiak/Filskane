namespace WebApplication1.Services;

using BitMiracle.LibTiff.Classic;
using HarfBuzzSharp;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using OSGeo.GDAL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Configuration;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using WebApplication1.Models;
using WebApplication1.Utils;

[Obsolete]
public class DatabaseService
{
    private readonly string _connectionString;
    private readonly string _gdalConnectionString;
    private readonly IPasswordHasherService _hasher;

    public DatabaseService(string connectionString, string gdalString, IPasswordHasherService hasher)
    {
        _connectionString = connectionString;
        _gdalConnectionString = gdalString;
        _hasher = hasher;
    }



    public async Task<bool> ValidateUserAsync(string username, string password)
    {
        // 1. Szybkie wyjście, jeśli dane są puste
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        try
        {
            // 2. await using - automatycznie zamyka połączenie, nawet przy błędzie
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"SELECT PASSWORD_HASH 
                           FROM USERS
                           WHERE USERNAME = :username 
                             AND IS_VERIFIED = 1";

            await using var cmd = new OracleCommand(sql, conn);

            // Bezpieczniejszy typ danych niż domyślny
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            // 3. Asynchroniczne zapytanie
            var result = await cmd.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                return false;

            string storedHash = result.ToString();

            // 4. Weryfikacja hasła
            return _hasher.Verify(password,storedHash);
        }
        catch
        {
            // Bez loggera po prostu zakładamy, że błąd bazy = nieudane logowanie
            return false;
        }
    }

    public async Task<UserDetailDto?> GetUserLongInfoAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        try
        {
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
            SELECT USERNAME, FIRST_NAME, EMAIL, TELEPHONE, FARM_LONGITUDE, FARM_LATITUDE
            FROM USERS
            WHERE USERNAME = :username";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new UserDetailDto(
                    // 1. Username
                    reader["USERNAME"].ToString() ?? "",

                    // 2. FirstName
                    reader["FIRST_NAME"].ToString() ?? "",

                    // 3. Email
                    reader["EMAIL"].ToString() ?? "",

                    // 4. Phone (obsługa nulla)
                    reader["TELEPHONE"] is DBNull ? null : reader["TELEPHONE"].ToString(),

                    // 5. FarmX (rzutowanie)
                    reader["FARM_LONGITUDE"] is DBNull ? null : Convert.ToDouble(reader["FARM_LONGITUDE"]),

                    // 6. FarmY
                    reader["FARM_LATITUDE"] is DBNull ? null : Convert.ToDouble(reader["FARM_LATITUDE"])
                );
            }

            return null;
        }
        catch 
        {
            return null;
        }
    }

    public async Task<UserShortDto?> GetUserShortInfoAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        try
        {
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
            SELECT FIRST_NAME, IS_DARK_MODE, SURFACE_UNIT, FARM_LONGITUDE, FARM_LATITUDE
            FROM USERS
            WHERE USERNAME = :username";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                // TWORZENIE REKORDU (Konstruktor)
                return new UserShortDto(
                    // 1. Name
                    reader["FIRST_NAME"].ToString() ?? "",

                    // 2. DarkMode
                    Convert.ToInt32(reader["IS_DARK_MODE"]),

                    // 3. Surface
                    Convert.ToInt32(reader["SURFACE_UNIT"]),

                    // 4. FarmX (bezpieczne rzutowanie nulla)
                    reader["FARM_LONGITUDE"] is DBNull ? null : Convert.ToDouble(reader["FARM_LONGITUDE"]),

                    // 5. FarmY
                    reader["FARM_LATITUDE"] is DBNull ? null : Convert.ToDouble(reader["FARM_LATITUDE"])
                );
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> UpdateUserFieldAsync(string username, string columnName, object value)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;

        try
        {
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            // UWAGA: columnName wstawiamy bezpośrednio w string (bezpieczne, bo wywołujemy to tylko z kodu backendu)
            string sql = $"UPDATE USERS SET {columnName} = :val WHERE USERNAME = :username";

            await using var cmd = new OracleCommand(sql, conn);

            // Oracle automatycznie dopasuje typ parametru (int/string)
            cmd.Parameters.Add("val", value);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch
        {
            // Logowanie błędu
            return false;
        }
    }

    // Publiczna metoda korzystająca z helpera
    public async Task<bool> UpdateUserSurfaceAsync(string username, int surface)
        => await UpdateUserFieldAsync(username, "SURFACE_UNIT", surface);
    
    // Jeśli potrzebujesz, od razu możesz dodać resztę:
    public async Task<bool> UpdateUserThemeAsync(string username, int isDarkMode)
        => await UpdateUserFieldAsync(username, "IS_DARK_MODE", isDarkMode);

    public async Task<bool> UpdateUserFirstNameAsync(string username, string firstName)
        => await UpdateUserFieldAsync(username, "FIRST_NAME", firstName);

    public async Task<bool> UpdateUserEmailAsync(string username, string email)
        => await UpdateUserFieldAsync(username, "EMAIL", email);

    public async Task<bool> UpdateUserPhoneAsync(string username, string telephone)
        => await UpdateUserFieldAsync(username, "TELEPHONE", telephone);


    public async Task SaveFarmCoordinatesAsync(string username, double? farmX, double? farmY)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
        UPDATE USERS
        SET FARM_LONGITUDE = :farmX, FARM_LATITUDE = :farmY
        WHERE USERNAME = :username";

        await using var cmd = new OracleCommand(sql, conn);

        // Obsługa NULLi dla koordynatów
        cmd.Parameters.Add("farmX", OracleDbType.Double).Value = (object?)farmX ?? DBNull.Value;
        cmd.Parameters.Add("farmY", OracleDbType.Double).Value = (object?)farmY ?? DBNull.Value;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteFarmCoordinatesAsync(string username)
    {
        // Reużywamy logiki - usunięcie to po prostu nadpisanie nullami
        await SaveFarmCoordinatesAsync(username, null, null);
    }

    public async Task<int> SaveFieldAsync(string username, string name, string geojson, double centerX, double centerY, double area, string complex, string type, string substrate)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        // OPTYMALIZACJA SQL:
        // Zamiast robić SELECT UserID w C#, robimy go wewnątrz INSERTa (podzapytanie).
        // To oszczędza jeden "kurs" do bazy danych.
        string sql = @"
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

        // Parametry wejściowe
        cmd.Parameters.Add("name", OracleDbType.Varchar2).Value = name;
        cmd.Parameters.Add("centerX", OracleDbType.Double).Value = centerX;
        cmd.Parameters.Add("centerY", OracleDbType.Double).Value = centerY;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username; // Tu podajemy username do podzapytania
        cmd.Parameters.Add("geojson", OracleDbType.Clob).Value = geojson; // Clob dla długich JSONów
        cmd.Parameters.Add("area", OracleDbType.Double).Value = area;
        cmd.Parameters.Add("soilComplex", OracleDbType.Varchar2).Value = complex;
        cmd.Parameters.Add("soilType", OracleDbType.Varchar2).Value = type;
        cmd.Parameters.Add("soilSubstrate", OracleDbType.Varchar2).Value = substrate;

        // Parametr wyjściowy (ID nowego pola)
        var newIdParam = new OracleParameter("newId", OracleDbType.Decimal)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(newIdParam);

        try
        {
            await cmd.ExecuteNonQueryAsync();

            // Konwersja OracleDecimal na int
            if (newIdParam.Value is OracleDecimal oraDec && !oraDec.IsNull)
            {
                return oraDec.ToInt32();
            }
        
            throw new Exception("Nie udało się pobrać ID nowego pola.");
        }
        catch (OracleException ex)
        {
            // Obsługa błędów, np. jeśli użytkownik nie istnieje (podzapytanie zwróci null i insert się wywali na constraintach)
            throw new Exception($"Błąd zapisu pola: {ex.Message}", ex);
        }
    }

    public async Task<List<FieldShortDto>> GetUserFieldsAsync(string username)
    {
        var fields = new List<FieldShortDto>();

        // Zabezpieczenie przed pustym username
        if (string.IsNullOrWhiteSpace(username)) return fields;

        try
        {
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"
            SELECT f.FIELD_ID,
                   f.FIELD_NAME, 
                   f.CENTER_LONGITUDE, 
                   f.CENTER_LATITUDE, 
                   f.GEO_JSON
            FROM FIELDS f
            JOIN USERS u ON f.USER_ID = u.USER_ID
            WHERE u.USERNAME = :username";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                // Mapowanie na rekord FieldShortDto
                // Używamy bezpiecznej konwersji i obsługi nulli
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
        catch (OracleException ex)
        {
            // Specyficzna obsługa błędów Oracle
            // Opakowujemy błąd w Exception z kontekstem (jaka operacja, jaki user, jaki kod błędu)
            // 'ex' przekazujemy jako InnerException - dzięki temu nie tracimy oryginalnego śladu błędu!
            throw new Exception($"Błąd bazy danych (Oracle) podczas pobierania pól dla użytkownika '{username}'. Kod błędu: {ex.Number}. Treść: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            // Obsługa innych błędów (np. błąd rzutowania Convert.ToDouble, gdyby w bazie były śmieci)
            throw new Exception($"Nieoczekiwany błąd podczas przetwarzania listy pól dla '{username}': {ex.Message}", ex);
        }
    }

    public async Task<List<FieldListItemDto>> GetFieldListAsync(string username)
    {
        var list = new List<FieldListItemDto>();

        if (string.IsNullOrWhiteSpace(username)) return list;

        try
        {
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            // Pobieramy TYLKO to, co niezbędne do wyświetlenia listy
            string sql = @"
            SELECT f.FIELD_ID, f.FIELD_NAME
            FROM FIELDS f
            JOIN USERS u ON f.USER_ID = u.USER_ID
            WHERE u.USERNAME = :username
            ORDER BY f.FIELD_NAME"; // Warto posortować alfabetycznie dla wygody usera

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
            // Logujemy i rzucamy wyżej, lub zwracamy pustą listę w zależności od strategii
            throw new Exception($"Błąd pobierania listy pól: {ex.Message}", ex);
        }
    }

    public async Task DeleteFieldAsync(string username, int fieldId)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        // SQL jest bezpieczny - usuwa tylko jeśli zgadza się ID i Właściciel
        string sql = @"
        DELETE FROM FIELDS
        WHERE FIELD_ID = :fieldId
          AND USER_ID = (SELECT USER_ID FROM USERS WHERE USERNAME = :username)";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("fieldId", OracleDbType.Int32).Value = fieldId;
        cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

        try
        {
            int rows = await cmd.ExecuteNonQueryAsync();

            if (rows == 0)
            {
                // ZABEZPIECZENIE:
                // Jeśli rows == 0, to znaczy, że albo pole nie istnieje, 
                // albo należy do innego użytkownika (podzapytanie nie zwróciło match'a).
                // Rzucamy specyficzny wyjątek.
                throw new KeyNotFoundException($"Pole o ID {fieldId} nie istnieje lub nie należy do użytkownika {username}.");
            }
        }
        catch (OracleException ex)
        {
            // Obsługa błędów bazy danych (np. zerwane połączenie)
            throw new Exception($"Błąd bazy danych podczas usuwania pola: {ex.Message}", ex);
        }
    }

    public async Task<FieldDetailDto?> GetUserFieldByIdAsync(string username, int fieldId)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
            SELECT 
                f.FIELD_ID, 
                f.FIELD_NAME, 
                f.CROP_ID, 
                p.PLANT_NAME,        
                f.PLANT_STATE, 
                g.CYCLE_NAME,        
                f.SOWING_DATE,
                sc.COMPLEX_NAME, 
                st.TYPE_NAME, 
                ss.SUBSTRATE_NAME,
                f.AREA_M2, 
                f.GEO_JSON
            FROM FIELDS f
            LEFT JOIN PLANTS p ON f.CROP_ID = p.PLANT_ID
            LEFT JOIN GROWTH_CYCLES g ON f.PLANT_STATE = g.CYCLE_ID
            LEFT JOIN SOIL_COMPLEXES sc ON f.SOIL_COMPLEX = sc.COMPLEX_CODE
            LEFT JOIN SOIL_TYPES st ON f.SOIL_TYPE = st.TYPE_CODE
            LEFT JOIN SOIL_SUBSTRATES ss ON f.SOIL_SUBSTRATE = ss.SUBSTRATE_CODE
            JOIN USERS u ON f.USER_ID = u.USER_ID -- Joinujemy z USERS dla bezpieczeństwa
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

                // Używamy Utils do wyliczenia Bboxa z GeoJSONa (zakładam, że masz taką metodę)
                string bbox = GeoUtils.GetBboxFromGeoJson(geoJson)?.ToString() ?? "";

                return new FieldDetailDto(
                    Convert.ToInt32(reader["FIELD_ID"]),
                    GetSafeString(reader["FIELD_NAME"]) ?? "Bez nazwy", // Tu akurat chcemy stringa, nie nulla

                    reader["CROP_ID"] is DBNull ? null : Convert.ToInt32(reader["CROP_ID"]),

                    // Tu używamy naszego helpera:
                    GetSafeString(reader["PLANT_NAME"]),   // Zwróci null zamiast ""

                    reader["PLANT_STATE"] is DBNull ? null : Convert.ToInt32(reader["PLANT_STATE"]),

                    GetSafeString(reader["CYCLE_NAME"]),   // Zwróci null zamiast ""

                    reader["SOWING_DATE"] is DBNull ? null : Convert.ToDateTime(reader["SOWING_DATE"]),

                    GetSafeString(reader["COMPLEX_NAME"]), // Zwróci null zamiast ""
                    GetSafeString(reader["TYPE_NAME"]),    // Zwróci null zamiast ""
                    GetSafeString(reader["SUBSTRATE_NAME"]), // Zwróci null zamiast ""

                    Convert.ToDouble(reader["AREA_M2"] is DBNull ? 0 : reader["AREA_M2"]),

                    reader["GEO_JSON"]?.ToString() ?? "",
                    bbox
                );
            }
            return null;
        }
        catch (Exception ex)
        {
            // Logowanie...
            throw new Exception($"Błąd pobierania pola ID {fieldId}: {ex.Message}", ex);
        }
    }

    private string? GetSafeString(object value)
    {
        if (value == null || value is DBNull) return null;

        string str = value.ToString()?.Trim() ?? ""; // Trim() usuwa spacje " "

        return string.IsNullOrEmpty(str) ? null : str;
    }

    public async Task SaveFieldChangesAsync(int fieldId, UpdateFieldRequest dto)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.BindByName = true; // Ważne dla Oracle!

        // Sprawdzamy, czy mamy komplet danych do wyliczenia cyklu
        bool canCalculateCycle = (dto.CropId > 0) && (dto.SowingDate.HasValue);

        StringBuilder sql = new StringBuilder();
        sql.Append("UPDATE FIELDS SET ");

        // 1. Aktualizacja Crop (jeśli podano, wpisujemy. Jeśli 0/null -> null w bazie)
        sql.Append("CROP_ID = :crop, ");

        // 2. Aktualizacja Daty
        sql.Append("SOWING_DATE = :dateSow, ");

        // 3. Aktualizacja Cyklu (PLANT_STATE)
        if (canCalculateCycle)
        {
            // Mamy komplet -> Wyliczamy cykl z bazy
            sql.Append(@"PLANT_STATE = (
            SELECT CYCLE_ID
            FROM PLANT_STATES  -- <--- TUTAJ BYŁ BŁĄD. Tabela z obrazka 4 (ta z dniami).
            WHERE PLANT_ID = :cropSub
              AND :dateSowSub + MIN_DAYS <= SYSDATE
              AND :dateSowSub + MAX_DAYS >= SYSDATE
            FETCH FIRST 1 ROWS ONLY
        ) ");
        }
        else
        {
            // Nie mamy kompletu -> Resetujemy cykl, bo stare dane mogą być mylące
            // (np. zmieniliśmy kukurydzę na pszenicę, ale bez daty nie wiemy jaki jest cykl)
            sql.Append("PLANT_STATE = NULL ");
        }

        sql.Append("WHERE FIELD_ID = :fieldId");

        cmd.CommandText = sql.ToString();

        // Parametry
        // Jeśli CropId jest null lub 0, wstawiamy DBNull.Value
        cmd.Parameters.Add("crop", OracleDbType.Int32).Value = (dto.CropId > 0) ? dto.CropId : DBNull.Value;

        // Jeśli Data jest null, wstawiamy DBNull.Value
        cmd.Parameters.Add("dateSow", OracleDbType.Date).Value = dto.SowingDate.HasValue ? dto.SowingDate.Value : DBNull.Value;
        cmd.Parameters.Add("fieldId", OracleDbType.Int32).Value = fieldId;

        if (canCalculateCycle)
        {
            // Parametry do podzapytania (muszą być dodane, jeśli używamy tej części SQL)
            // Oracle z BindByName=true poradzi sobie z powtórzeniem nazw, 
            // ale w podzapytaniu użyłem :cropSub i :dateSowSub dla pewności.
            cmd.Parameters.Add("cropSub", OracleDbType.Int32).Value = dto.CropId;
            cmd.Parameters.Add("dateSowSub", OracleDbType.Date).Value = dto.SowingDate.Value;
        }

        int rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0) throw new Exception("Nie znaleziono pola lub brak uprawnień.");
    }

    public async Task<CycleDto?> GetCycleByIdAsync(int fieldId)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        string sql = @"
            SELECT g.CYCLE_ID, g.CYCLE_NAME
            FROM FIELDS f
            JOIN GROWTH_CYCLES g ON f.PLANT_STATE = g.CYCLE_ID
            WHERE f.FIELD_ID = :fieldId";

        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("fieldId", OracleDbType.Int32).Value = fieldId;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new CycleDto(
                Convert.ToInt32(reader["CYCLE_ID"]),
                reader["CYCLE_NAME"].ToString() ?? ""
            );
        }
        return null;
    }

    public async Task<bool> CheckIfEmailExistsAsync(string email)
    {
        // Guard clause: puste dane nie mają sensu
        if (string.IsNullOrWhiteSpace(email)) return false;

        try
        {
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            // COUNT(1) jest czasem minimalnie szybsze niż COUNT(*)
            string sql = "SELECT COUNT(1) FROM USERS WHERE EMAIL = :email";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("email", OracleDbType.Varchar2).Value = email;

            var result = await cmd.ExecuteScalarAsync();

            // Bezpieczna konwersja
            return Convert.ToInt32(result) > 0;
        }
        catch
        {
            // W razie awarii bazy zakładamy false (lub rzucamy wyjątek w zależności od strategii)
            return false;
        }
    }

    public async Task<bool> RegisterUserAsync(WebApplication1.Models.RegisterRequest request, string token)
    {
        // Hashowanie przed połączeniem
        string hashedPassword = _hasher.Hash(request.Password);

        try
        {
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            string sql = @"INSERT INTO USERS (FIRST_NAME, USERNAME, EMAIL, PASSWORD_HASH, VERIFICATION_TOKEN)
                           VALUES (:name, :username, :email, :passwordhash, :verificationToken)";

            await using var cmd = new OracleCommand(sql, conn);

            // Parametryzacja
            cmd.Parameters.Add("name", OracleDbType.Varchar2).Value = request.Name;
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = request.Username;
            cmd.Parameters.Add("email", OracleDbType.Varchar2).Value = request.Email;
            cmd.Parameters.Add("passwordhash", OracleDbType.Varchar2).Value = hashedPassword;
            cmd.Parameters.Add("verificationToken", OracleDbType.Varchar2).Value = token;

            // Asynchroniczny zapis
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (OracleException ex)
        {
            // ORA-00001: Naruszenie unikalności (zajęty login/email)
            if (ex.Number == 1)
            {
                return false; // Zwracamy false, kontroler może zwrócić np. "Login zajęty"
            }

            // Inne błędy rzucamy dalej, żeby kontroler zwrócił 500
            throw;
        }
    }

    public async Task<bool> VerifyUserAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            // OPTYMALIZACJA: Nie robimy SELECT potem UPDATE. 
            // Robimy od razu UPDATE. Jeśli token nie istnieje, zaktualizuje 0 wierszy.
            // Dodatkowo ustawiamy TOKEN na NULL, żeby nie można go było użyć drugi raz.
            string sql = @"UPDATE USERS 
                       SET IS_VERIFIED = 1, VERIFICATION_TOKEN = NULL 
                       WHERE VERIFICATION_TOKEN = :token";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("token", OracleDbType.Varchar2).Value = token;

            int rowsAffected = await cmd.ExecuteNonQueryAsync();

            // Jeśli zaktualizowano > 0 wierszy, to znaczy że token był poprawny
            return rowsAffected > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task SaveRasterAsync(byte[] rasterData, int fieldId, DateTime date, string bboxJson)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        // Transakcja jest wymagana przy operacjach na GeoRaster/LOB
        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            int newId;

            // KROK 1: Insert pustego GeoRastera i pobranie ID
            await using (var initCmd = conn.CreateCommand())
            {
                initCmd.Transaction = (OracleTransaction)transaction;
                initCmd.CommandText = @"
                INSERT INTO SATELLITE_SCANS (FIELD_ID, SCAN_DATE, RASTER, BBOX)
                VALUES (:fieldId, :scanDate, MDSYS.SDO_GEOR.init('SATELLITE_SCANS_RDT'), :bbox)
                RETURNING SCAN_ID INTO :newId";

                initCmd.Parameters.Add("fieldId", fieldId);
                initCmd.Parameters.Add("scanDate", date);
                initCmd.Parameters.Add("bbox", bboxJson); // Oracle 19c+ obsługuje JSON jako varchar/blob/json type

                var newIdParam = new OracleParameter("newId", OracleDbType.Int32) { Direction = ParameterDirection.Output };
                initCmd.Parameters.Add(newIdParam);

                await initCmd.ExecuteNonQueryAsync();

                // Pobieramy wygenerowane ID (OracleDecimal -> Int)
                if (newIdParam.Value is OracleDecimal d) newId = d.ToInt32();
                else throw new Exception("Nie udało się pobrać ID skanu.");
            }

            // KROK 2: Import TIFF do GeoRastera
            await using (var importCmd = conn.CreateCommand())
            {
                importCmd.Transaction = (OracleTransaction)transaction;
                importCmd.CommandText = @"
                DECLARE
                  v_geor MDSYS.SDO_GEORASTER;
                BEGIN
                  -- Lock wiersza
                  SELECT RASTER INTO v_geor FROM SATELLITE_SCANS WHERE SCAN_ID = :id FOR UPDATE;

                  -- Import z BLOBA
                  SDO_GEOR.importFrom(v_geor, '', 'TIFF', :blob);

                  -- Aktualizacja metadanych
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

    public async Task<ScanResultDto?> GetLatestScanAsync(int fieldId)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        // Używamy Twojego bloku PL/SQL, jest OK.
        string sql = @"
            DECLARE
                gr MDSYS.SDO_GEORASTER;
                out_blob BLOB;
                v_date DATE;
                v_bbox VARCHAR2(4000); -- Lub CLOB/JSON
            BEGIN
                BEGIN
                    SELECT raster, scan_date, bbox
                    INTO gr, v_date, v_bbox
                    FROM satellite_scans
                    WHERE field_id = :id
                    ORDER BY scan_date DESC
                    FETCH FIRST 1 ROWS ONLY;
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
        cmd.Parameters.Add("id", OracleDbType.Int32).Value = fieldId;

        cmd.Parameters.Add("result", OracleDbType.Blob).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("scanDate", OracleDbType.Date).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("bboxInfo", OracleDbType.Varchar2, 4000).Direction = ParameterDirection.Output;

        await cmd.ExecuteNonQueryAsync();

        // Odczyt parametrów wyjściowych (bezpiecznie)
        var blobVal = cmd.Parameters["result"].Value as OracleBlob;
        if (blobVal == null || blobVal.IsNull) return null; // Brak skanu

        byte[] imageBytes = blobVal.Value; // To kopiuje bajty do pamięci

        var dateVal = cmd.Parameters["scanDate"].Value;
        DateTime date = (dateVal is Oracle.ManagedDataAccess.Types.OracleDate od && !od.IsNull) ? od.Value : DateTime.MinValue;

        string bbox = cmd.Parameters["bboxInfo"].Value?.ToString() ?? "";

        return new ScanResultDto(date, imageBytes, bbox);
    }

    public async Task<ScanResultDto?> GetScanByIdAsync(int scanId)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        // Używamy Twojego bloku PL/SQL, jest OK.
        string sql = @"
            DECLARE
                gr MDSYS.SDO_GEORASTER;
                out_blob BLOB;
                v_date DATE;
                v_bbox VARCHAR2(4000); -- Lub CLOB/JSON
            BEGIN
                BEGIN
                    SELECT raster, scan_date, bbox
                    INTO gr, v_date, v_bbox
                    FROM satellite_scans
                    WHERE scan_id = :id;
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
        cmd.Parameters.Add("id", OracleDbType.Int32).Value = scanId;

        cmd.Parameters.Add("result", OracleDbType.Blob).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("scanDate", OracleDbType.Date).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("bboxInfo", OracleDbType.Varchar2, 4000).Direction = ParameterDirection.Output;

        await cmd.ExecuteNonQueryAsync();

        // Odczyt parametrów wyjściowych (bezpiecznie)
        var blobVal = cmd.Parameters["result"].Value as OracleBlob;
        if (blobVal == null || blobVal.IsNull) return null; // Brak skanu

        byte[] imageBytes = blobVal.Value; // To kopiuje bajty do pamięci

        var dateVal = cmd.Parameters["scanDate"].Value;
        DateTime date = (dateVal is Oracle.ManagedDataAccess.Types.OracleDate od && !od.IsNull) ? od.Value : DateTime.MinValue;

        string bbox = cmd.Parameters["bboxInfo"].Value?.ToString() ?? "";

        return new ScanResultDto(date, imageBytes, bbox);
    }

    public async Task<List<ScanSummaryDto>> GetFieldScansAsync(int fieldId)
    {
        var list = new List<ScanSummaryDto>();
        await using var conn = new OracleConnection(_connectionString);
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

    public async Task<bool> DeleteScanAsync(int scanId)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        // Przy GeoRasterach trzeba uważać, czy usuwamy też dane z tabeli RDT (Raster Data Table).
        // Jeśli masz poprawne triggery w bazie Oracle, zwykły DELETE wystarczy.
        string sql = "DELETE FROM SATELLITE_SCANS WHERE SCAN_ID = :id";
        await using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add("id", scanId);

        return (await cmd.ExecuteNonQueryAsync()) > 0;
    }

    public async Task<List<PlantDto>> GetPlantsAsync()
    {
        var list = new List<PlantDto>();
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        string sql = "SELECT PLANT_ID, PLANT_NAME FROM PLANTS ORDER BY PLANT_NAME";
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new PlantDto(
                Convert.ToInt32(reader["PLANT_ID"]),
                reader["PLANT_NAME"].ToString() ?? ""
            ));
        }
        return list;
    }
    public async Task<List<ThresholdDto>> GetThresholdsAsync()
    {
        var list = new List<ThresholdDto>();
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        string sql = "SELECT CYCLE_ID, MIN_NDVI, MAX_NDVI FROM GROWTH_CYCLES";
        await using var cmd = new OracleCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new ThresholdDto(
                Convert.ToInt32(reader["CYCLE_ID"]),
                Convert.ToDouble(reader["MIN_NDVI"]),
                Convert.ToDouble(reader["MAX_NDVI"])
            ));
        }
        return list;
    }
    public async Task<bool> DeleteUserAccountAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return false;

        await using var conn = new OracleConnection(_connectionString);

        try
        {
            await conn.OpenAsync();

            // KROK 1: Pobieramy hash hasła dla tego użytkownika
            string selectSql = "SELECT PASSWORD_HASH FROM USERS WHERE USERNAME = :username";

            string storedHash;

            await using (var checkCmd = new OracleCommand(selectSql, conn))
            {
                checkCmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

                var result = await checkCmd.ExecuteScalarAsync();

                // Jeśli result jest null, to użytkownik nie istnieje
                if (result == null || result == DBNull.Value)
                    return false;

                storedHash = result.ToString();
            }

            // KROK 2: Weryfikacja hasła w C# (To jest kluczowe dla bezpieczeństwa!)
            // Nie możemy tego zrobić w SQL, bo hashe mają "sól"
            var verificationResult = _hasher.Verify(storedHash, password);

            if (!verificationResult)
            {
                return false; // Złe hasło
            }

            // KROK 3: Usuwanie konta
            // Nie potrzebujemy transakcji SQL, bo robimy tylko jedną operację DELETE.
            // Jeśli masz w bazie CASCADE DELETE (klucze obce), Oracle usunie powiązane dane (Pola, Skany) automatycznie.
            string deleteSql = "DELETE FROM USERS WHERE USERNAME = :username";

            await using (var deleteCmd = new OracleCommand(deleteSql, conn))
            {
                deleteCmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;
                int rows = await deleteCmd.ExecuteNonQueryAsync();

                return rows > 0;
            }
        }
        catch
        {
            // Tutaj logujemy błąd (jeśli kiedyś dodasz logger)
            // throw; // Odkomentuj, jeśli chcesz, żeby kontroler wiedział o błędzie 500
            return false;
        }
    }
}



