namespace WebApplication1.Services;

using BitMiracle.LibTiff.Classic;
using HarfBuzzSharp;
using Microsoft.AspNetCore.Components.Routing;
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
using System.Text.Json;
using WebApplication1.Models;
using WebApplication1.Utils;

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



    public bool ValidateUser(string username, string password)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        string sql = @"SELECT PasswordHash 
                   FROM Users 
                   WHERE Username = :username 
                         AND Verification = 1";

        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(":username", OracleDbType.Varchar2).Value = username;

        var result = cmd.ExecuteScalar();

        if (result == null || result == DBNull.Value)
            return false;

        string storedHash = result.ToString();

        // Porównanie hashy — BCrypt
        return _hasher.Verify(password, storedHash);
    }

    public UserInfo? GetUserInfo(string username)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        string sql = @"
            SELECT Username, Name, Email, Telephone, FarmX, FarmY
            FROM Users
            WHERE Username = :username";

        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(":username", OracleDbType.Varchar2).Value = username;

        try
        {
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new UserInfo
                {
                    Username = reader.GetString(0),
                    Name = reader.GetString(1),
                    Email = reader.GetString(2),
                    Telephone = reader.IsDBNull(3) ? null : reader.GetString(3),
                    FarmX = reader.IsDBNull(4) ? (double?)null : Convert.ToDouble(reader.GetValue(4)),
                    FarmY = reader.IsDBNull(5) ? (double?)null : Convert.ToDouble(reader.GetValue(5))
                };
            }
            else
                return null;
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex)
        {
            throw new Exception($"Oracle error {ex.Number}: {ex.Message}", ex);
        }

    }

    public UserShortInfoDto? GetUserShortInfo(string username)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        string sql = @"
            SELECT Name, Darkmode, Surface, FarmX, FarmY
            FROM Users
            WHERE Username = :username";

        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(":username", OracleDbType.Varchar2).Value = username;

        try
        {
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new UserShortInfoDto
                {
                    Name = reader.GetString(0),
                    DarkMode = reader.GetInt16(1),
                    Surface = reader.GetInt16(2),
                    FarmX = reader.IsDBNull(3) ? (double?)null : Convert.ToDouble(reader.GetValue(3)),
                    FarmY = reader.IsDBNull(4) ? (double?)null : Convert.ToDouble(reader.GetValue(4))
                };
            }
            else
                return null;
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex)
        {
            throw new Exception($"Oracle error {ex.Number}: {ex.Message}", ex);
        }

    }

    public void UpdateUserSurface(string username, int surface)
    {
        using (var connection = new OracleConnection(_connectionString))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                UPDATE USERS
                SET SURFACE = :surface
                WHERE USERNAME = :username";

                command.Parameters.Add(new OracleParameter("surface", surface));
                command.Parameters.Add(new OracleParameter("username", username));

                int rows = command.ExecuteNonQuery();
                if (rows == 0)
                    throw new ArgumentException("Nie znaleziono użytkownika");
            }
        }
    }

    public void UpdateUserName(string username, string name)
    {
        using (var connection = new OracleConnection(_connectionString))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                UPDATE USERS
                SET Name = :name
                WHERE USERNAME = :username";

                command.Parameters.Add(new OracleParameter("name", name));
                command.Parameters.Add(new OracleParameter("username", username));

                int rows = command.ExecuteNonQuery();
                if (rows == 0)
                    throw new ArgumentException("Nie znaleziono użytkownika");
            }
        }
    }

    public void UpdateUserEmail(string username, string email)
    {
        using (var connection = new OracleConnection(_connectionString))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                UPDATE USERS
                SET Email = :email
                WHERE USERNAME = :username";

                command.Parameters.Add(new OracleParameter("email", email));
                command.Parameters.Add(new OracleParameter("username", username));

                int rows = command.ExecuteNonQuery();
                if (rows == 0)
                    throw new ArgumentException("Nie znaleziono użytkownika");
            }
        }
    }

    public void UpdateUserPhone(string username, string phone)
    {
        using (var connection = new OracleConnection(_connectionString))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                UPDATE USERS
                SET Telephone = :phone
                WHERE USERNAME = :username";

                command.Parameters.Add(new OracleParameter("phone", phone));
                command.Parameters.Add(new OracleParameter("username", username));

                int rows = command.ExecuteNonQuery();
                if (rows == 0)
                    throw new ArgumentException("Nie znaleziono użytkownika");
            }
        }
    }

    public void UpdateUserTheme(string username, string theme)
    {
        using (var connection = new OracleConnection(_connectionString))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                UPDATE USERS
                SET DARKMODE = :themeFlag
                WHERE UPPER(USERNAME) = UPPER(:username)";

                // Zamieniamy na 0/1 w bazie
                int themeFlag = theme == "dark" ? 1 : 0;
                command.Parameters.Add(new OracleParameter("themeFlag", themeFlag));
                command.Parameters.Add(new OracleParameter("username", username));

                int rows = command.ExecuteNonQuery();
                if (rows == 0)
                    throw new ArgumentException("Nie znaleziono użytkownika o nazwie: " + username);
            }
        }
    }
    public void SaveFarmCoordinates(string username, double? farmX, double? farmY)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        string sql = @"
            UPDATE Users
            SET FarmX = :farmX, FarmY = :farmY
            WHERE Username = :username";

        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(":farmX", OracleDbType.Decimal).Value = (object?)farmX ?? DBNull.Value;
        cmd.Parameters.Add(":farmY", OracleDbType.Decimal).Value = (object?)farmY ?? DBNull.Value;
        cmd.Parameters.Add(":username", OracleDbType.Varchar2).Value = username;

        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex)
        {
            throw new Exception($"Oracle error {ex.Number}: {ex.Message}", ex);
        }
    }

    public void DeleteFarmCoordinates(string username)
    {
        using var connection = new OracleConnection(_connectionString);
        
        connection.Open();

        string query = @"
            UPDATE Users
            SET FarmX = NULL, FarmY = NULL
            WHERE Username = :username";

        using var command = new OracleCommand(query, connection);
        
        command.Parameters.Add(new OracleParameter(":username", username));

        try
        {
            int rows = command.ExecuteNonQuery();

            if (rows == 0)
            {
                throw new Exception("Nie znaleziono użytkownika o podanej nazwie.");
            }
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex)
        {
            throw new Exception($"Oracle error {ex.Number}: {ex.Message}", ex);
        }
    }

    public int SaveField(string username, string name, string geojson, double centerX, double centerY, double area, string complex, string type, string substrate)
    {
        using var conn = new OracleConnection(_connectionString);
        
        conn.Open();

        // pobranie UserId po username
        int userId;
        using (var cmdUser = conn.CreateCommand())
        {
            cmdUser.CommandText = "SELECT UserId FROM USERS WHERE Username = :username";
            cmdUser.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":username", username));

            var result = cmdUser.ExecuteScalar();
            if (result == null)
                throw new Exception("Nie znaleziono użytkownika.");

            userId = Convert.ToInt32(result);
        }

        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO FIELDS (NAME, CENTERX, CENTERY, USERID, GEOJSON, AREA, SOILCOMPLEX, SOILTYPE, SOILSUBSTRATE)
            VALUES (:name, :centerX, :centerY, :userId, :geojson, :area, :soilComplex, :soilType, :soilSubstrate)
            RETURNING FIELDSID INTO :newId";


        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":name", name));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":centerX", centerX));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":centerY", centerY));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":userId", userId));
        cmd.Parameters.Add(new OracleParameter(":geojson", geojson));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":area", area));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":soilComplex", complex));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":soilType", type));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":soilSubstrate", substrate));

        var newIdParam = new OracleParameter(":newId", OracleDbType.Decimal)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(newIdParam);
        Console.WriteLine(newIdParam.Value);
        try
        {
            cmd.ExecuteNonQuery();
            var oracleDecimal = (OracleDecimal)newIdParam.Value;
            int newFieldId = oracleDecimal.ToInt32();
            return newFieldId;
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex)
        {
            throw new Exception($"Oracle error {ex.Number}: {ex.Message}", ex);
        }
        
    }

    public IEnumerable<object> GetUserFields(string username)
    {
        var fields = new List<object>();

        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT f.FieldsId,
                   f.Name, 
                   f.CENTERX, 
                   f.CENTERY, 
                   f.GeoJSON
            FROM FIELDS f
            JOIN USERS u ON f.UserId = u.UserId
            WHERE u.Username = :username";

        cmd.Parameters.Add(new OracleParameter(":username", OracleDbType.Varchar2)).Value = username;

        try
        {
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                fields.Add(new
                {
                    FieldId = Convert.ToInt32(reader.GetValue(0)),
                    Name = reader.GetString(1),
                    CenterX = reader.GetDouble(2),
                    CenterY = reader.GetDouble(3),
                    GeoJSON = reader.GetString(4)
                });
            }

            return fields;
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex)
        {
            throw new Exception($"Oracle error {ex.Number}: {ex.Message}", ex);
        }
    }

    public void DeleteField(string username, int fieldId)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        // upewniamy się, że pole należy do zalogowanego użytkownika
        string sql = @"
            DELETE FROM Fields
            WHERE FieldsId = :fieldId
              AND UserId = (SELECT UserId FROM Users WHERE Username = :username)";

        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(":fieldId", OracleDbType.Int32).Value = fieldId;
        cmd.Parameters.Add(":username", OracleDbType.Varchar2).Value = username;

        try
        {
            int rows = cmd.ExecuteNonQuery();
            if (rows == 0)
            {
                throw new Exception("Pole nie istnieje lub nie należy do użytkownika.");
            }
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex)
        {
            throw new Exception($"Oracle error {ex.Number}: {ex.Message}", ex);
        }
    }

    public object? GetUserFieldById(string username, int fieldId)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        string sql = @"
            SELECT f.FieldsId,
                   f.Name, 
                   f.CropId,
                   p.PlantName,        
                   f.PlantState,
                   g.CycleName,       
                   f.SowingDate,
                   f.SoilComplex,
                   f.SoilType,
                   f.SoilSubstrate,
                   f.Area,
                   f.Geojson
            FROM FIELDS f
            LEFT JOIN PLANTS p ON f.CropId = p.PlantId
            LEFT JOIN GROWTHCYCLES g ON f.PlantState = g.CycleId
            WHERE f.FieldsId = :fieldId";

        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(":fieldId", OracleDbType.Int32).Value = fieldId;

        try
        {
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new
                {
                    FieldId = Convert.ToInt32(reader.GetValue(0)),
                    Name = reader.GetString(1),

                    Crop = reader.IsDBNull(2) ? (int?)null : Convert.ToInt32(reader.GetValue(2)),
                    PlantName = reader.IsDBNull(3) ? "Nieuzupełnione" : reader.GetString(3),

                    PlantState = reader.IsDBNull(4) ? (int?)null : Convert.ToInt32(reader.GetValue(4)),
                    CycleName = reader.IsDBNull(5) ? "Nieuzupełnione" : reader.GetString(5),

                    SowingDate = reader.IsDBNull(6) ? "Nieuzupełnione" : reader.GetDateTime(6).ToString("dd-MM-yyyy"),
                    SoilComplex = reader.IsDBNull(7) ? "Nieuzupełnione" : reader.GetString(7),
                    SoilType = reader.IsDBNull(8) ? "Nieuzupełnione" : reader.GetString(8),
                    SoilSubstrate = reader.IsDBNull(9) ? "Nieuzupełnione" : reader.GetString(9),
                    Area = reader.IsDBNull(10) ? 0.0 : reader.GetDouble(10),
                    Geojson = reader.IsDBNull(11) ? "Nieuzupełnione" : reader.GetString(11),
                    MinBbox = GeoUtils.GetBboxFromGeoJson(reader.IsDBNull(11) ? "Nieuzupełnione" : reader.GetString(11))?.ToString()
                };

            }
            else
            {
                return null; // nie znaleziono pola
            }
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex)
        {
            throw new Exception($"Oracle error {ex.Number}: {ex.Message}", ex);
        }
    }

    public void SaveFieldChanges(int fieldId, UpdateFieldDto updateDto)
    {
        if (updateDto.Crop == 0 || !updateDto.SowingDate.HasValue)
            throw new ArgumentException("Crop i SowingDate muszą być podane, aby wyznaczyć cykl wzrostu.");

        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
        UPDATE FIELDS
        SET 
            CropId = :crop,
            SowingDate = :dateSow,
            PlantState = (
                SELECT CycleId
                FROM plantStates
                WHERE PlantId = :cropSub
                  AND :dateSowSub1 + minDays <= SYSDATE
                  AND :dateSowSub2 + maxDays >= SYSDATE
                FETCH FIRST 1 ROWS ONLY
            )
        WHERE FieldsId = :fieldId";

        cmd.Parameters.Add(":crop", OracleDbType.Int32).Value = updateDto.Crop;
        cmd.Parameters.Add(":dateSow", OracleDbType.Date).Value = updateDto.SowingDate.Value;
        cmd.Parameters.Add(":cropSub", OracleDbType.Int32).Value = updateDto.Crop; //dlaczego muszę drugi raz deklarować?
        cmd.Parameters.Add(":dateSowSub1", OracleDbType.Date).Value = updateDto.SowingDate.Value;
        cmd.Parameters.Add(":dateSowSub2", OracleDbType.Date).Value = updateDto.SowingDate.Value;
        cmd.Parameters.Add(":fieldId", OracleDbType.Int32).Value = fieldId;

        Console.WriteLine(cmd.CommandText);
        foreach (OracleParameter p in cmd.Parameters)
            Console.WriteLine($"{p.ParameterName} = {p.Value}");

        int rowsAffected = cmd.ExecuteNonQuery();
        if (rowsAffected == 0)
            throw new Exception("Nie znaleziono pola lub brak uprawnień do edycji");
    }

    public object GetCycleById(int fieldId)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT CycleId, CycleName
        FROM Fields f
        LEFT JOIN GrowthCycles g ON (f.PlantState = g.CycleId)
        WHERE fieldsId = :fieldId";
        cmd.Parameters.Add(":fieldId", OracleDbType.Int32).Value = fieldId;

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new GetCycleDto
            {
                CycleId = reader.GetInt32(0),
                CycleName = reader.GetString(1)
            };
        }

        return null;
    }

    public bool CheckIfEmailExists(string email)
    {
        bool exists = false;

        using (var connection = new OracleConnection(_connectionString))
        {
            connection.Open();

            string query = "SELECT COUNT(*) FROM USERS WHERE EMAIL = :email";
            using (var command = new OracleCommand(query, connection))
            {
                command.Parameters.Add(new OracleParameter("email", email));

                var result = command.ExecuteScalar();
                int count = Convert.ToInt32(result);

                exists = count > 0;
            }
        }

        return exists;
    }

    public bool RegisterUser(WebApplication1.Models.RegisterRequest request, string token)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        string sql = @"INSERT INTO USERS (NAME, USERNAME, EMAIL, PASSWORDHASH, VERIFICATIONTOKEN)
                           VALUES (:name, :username, :email, :passwordhash, :verificationToken)";

        using var cmd = new OracleCommand(sql, conn);

        Console.WriteLine(request.Password);
        cmd.Parameters.Add(new OracleParameter("name", request.Name));
        cmd.Parameters.Add(new OracleParameter("username", request.Username));
        cmd.Parameters.Add(new OracleParameter("email", request.Email));
        cmd.Parameters.Add(new OracleParameter("passwordhash", _hasher.Hash(request.Password))); // UWAGA: hasło powinno być zahashowane!
        cmd.Parameters.Add(new OracleParameter("verificationToken", token));

        int rows = cmd.ExecuteNonQuery();
        return rows > 0;
    }

    public bool VerifyUser(string token)
    {
        using (var conn = new OracleConnection(_connectionString))
        {
            conn.Open();
            Console.WriteLine($"Verify user: {token}");

            // 🔹 Sprawdź, czy istnieje użytkownik z tym tokenem
            using (var checkCmd = new OracleCommand("SELECT COUNT(*) FROM Users WHERE VerificationToken = :token", conn))
            {
                checkCmd.Parameters.Add(new OracleParameter("token", token));
                int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count == 0)
                {
                    return false; // nie ma takiego tokena
                }
            }

            // 🔹 Zaktualizuj kolumnę IsVerified i usuń token
            using (var updateCmd = new OracleCommand(
                "UPDATE Users SET Verification = 1, VerificationToken = NULL WHERE VerificationToken = :token", conn))
            {
                updateCmd.Parameters.Add(new OracleParameter("token", token));
                int rowsAffected = updateCmd.ExecuteNonQuery();
                return rowsAffected > 0; // true jeśli udało się zaktualizować
            }
        }
    }

    public async Task SaveRasterAsync(byte[] rasterData, int fieldId, DateTime date, string bbox)
    {
        try
        {
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            OracleBlob blobParam = new(conn);
            blobParam.Write(rasterData, 0, rasterData.Length);

            // Begin a transaction
            await using var transaction = await conn.BeginTransactionAsync();

            // STEP 1: Insert an empty GeoRaster row
            await using (var initCmd = conn.CreateCommand())
            {
                initCmd.Transaction = (OracleTransaction)transaction;

                initCmd.CommandText = @"
                INSERT INTO SATELLITE_SCANS (FIELD_ID, SCAN_DATE, RASTER, BBOX)
                VALUES (:fieldId, :scanDate, MDSYS.SDO_GEOR.init('SATELLITE_SCANS_RDT'),:bbox)
                RETURNING SCAN_ID INTO :newId";

                initCmd.Parameters.Add(new OracleParameter(":fieldId", fieldId));
                initCmd.Parameters.Add(new OracleParameter(":scanDate", date));
                initCmd.Parameters.Add(new OracleParameter(":bbox", bbox));

                var newIdParam = new OracleParameter(":newId", OracleDbType.Int32)
                {
                    Direction = ParameterDirection.Output
                };
                initCmd.Parameters.Add(newIdParam);

                await initCmd.ExecuteNonQueryAsync();

                var newId = ((OracleDecimal)newIdParam.Value).ToInt32();

                // STEP 2: Import TIFF into the GeoRaster
                await using (var importCmd = conn.CreateCommand())
                {
                    importCmd.Transaction = (OracleTransaction)transaction;

                    importCmd.CommandText = @"
                    DECLARE
                      v_geor MDSYS.SDO_GEORASTER;
                    BEGIN
                      -- pobranie GeoRaster
                      SELECT RASTER INTO v_geor
                      FROM SATELLITE_SCANS
                      WHERE SCAN_ID = :id
                      FOR UPDATE;

                      -- import z użyciem pełnej sygnatury
                      SDO_GEOR.importFrom(
                          v_geor,               -- GeoRaster
                          'blocking=OPTIMALPADDING,blocksize=(256,256,3),compression=NONE',
                          'TIFF',               -- r_sourceFormat
                          :blob,                -- r_sourceBLOB
                          NULL,                 -- h_sourceFormat
                          NULL                  -- h_sourceCLOB
                      );

                      -- aktualizacja tabeli
                      UPDATE SATELLITE_SCANS SET RASTER = v_geor WHERE SCAN_ID = :id;
                    END;";

                    importCmd.Parameters.Add(new OracleParameter(":id", newId));
                    importCmd.Parameters.Add(new OracleParameter(":blob", blobParam));

                    await importCmd.ExecuteNonQueryAsync();
                }

                // Commit once both steps succeed
                await transaction.CommitAsync();
                Console.WriteLine($"✅ TIFF imported successfully into row ID = {newId}");
            }
        }
        catch (OracleException ex)
        {
            Console.WriteLine($"❌ OracleException: {ex.Message}");
            Console.WriteLine($"ErrorCode: {ex.ErrorCode}, Number: {ex.Number}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ General exception: {ex.Message}");
            throw;
        }
    }

    public async Task SaveRasterAsyncGDAL(byte[] rasterData, int fieldId, DateTime date)
    {
        try
        {
            Gdal.AllRegister();

            for (int i = 0; i < Gdal.GetDriverCount(); i++)
            {
                Driver drv = Gdal.GetDriver(i);
                Console.WriteLine(drv.ShortName);
            }

            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            await using var transaction = await conn.BeginTransactionAsync();

            int newId;
            // STEP 1: Insert an empty GeoRaster row
            await using (var initCmd = conn.CreateCommand())
            {
                initCmd.Transaction = (OracleTransaction)transaction;

                initCmd.CommandText = @"
                INSERT INTO SATELLITE_SCANS (FIELD_ID, SCAN_DATE, RASTER)
                VALUES (:fieldId, :scanDate, MDSYS.SDO_GEOR.init('SATELLITE_SCANS_RDT'))
                RETURNING SCAN_ID INTO :newId";

                initCmd.Parameters.Add(new OracleParameter(":fieldId", fieldId));
                initCmd.Parameters.Add(new OracleParameter(":scanDate", date));

                var newIdParam = new OracleParameter(":newId", OracleDbType.Int32)
                {
                    Direction = ParameterDirection.Output
                };
                initCmd.Parameters.Add(newIdParam);

                await initCmd.ExecuteNonQueryAsync();
                newId = ((OracleDecimal)newIdParam.Value).ToInt32();
            }

            // Załaduj byte[] do pamięci
            string memPath = "/vsimem/temp.tif";
            Gdal.FileFromMemBuffer(memPath, rasterData);

            // Otwórz dataset z GDAL
            Dataset srcDs = Gdal.Open(memPath, Access.GA_ReadOnly);


            // Utwórz GeoRaster w Oracle
            string oracleGeoRasterConn = $"georaster:{_gdalConnectionString},SATELLITE_SCANS,RASTER,id={newId}";
            Driver geoRasterDriver = Gdal.GetDriverByName("GeoRaster");
            if (geoRasterDriver == null)
                throw new Exception("GDAL GeoRaster driver not found. Upewnij się, że GDAL został zbudowany z obsługą Oracle GeoRaster i wszystkie biblioteki Oracle Instant Client są w PATH.");

            string[] options = new string[]
            {
                "SRID=4326",
                "BLOCKXSIZE=512",
                "BLOCKYSIZE=512",
                "BLOCKBSIZE=4",
                "INTERLEAVE=BIP",
                "COMPRESS=NONE"
            };

            Dataset dstDs = geoRasterDriver.CreateCopy(oracleGeoRasterConn, srcDs, 0, options, null, null);
            dstDs.FlushCache();
            dstDs.Dispose();
            srcDs.Dispose();

            // Usuń dataset w pamięci
            Gdal.Unlink(memPath);
        }
        catch (OracleException ex)
        {
            Console.WriteLine($"❌ OracleException: {ex.Message}");
            Console.WriteLine($"ErrorCode: {ex.ErrorCode}, Number: {ex.Number}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ General exception: {ex.Message}");
            throw;
        }
    }

    public async Task<string?> GetFieldPolygonAsync(int fieldId)
    {
        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT GEOJSON FROM FIELDS WHERE FIELDSID = :id";
        cmd.Parameters.Add(":id", fieldId);

        using var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
        if (!await reader.ReadAsync() || reader.IsDBNull(0))
            return null;

        string geoJsonString = reader.GetString(0);

        return geoJsonString;
    }

    public async Task<ScanResult?> GetLatestScanAsync(int fieldId)
    {
        try
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new OracleCommand(@"
                DECLARE
                    gr MDSYS.SDO_GEORASTER;
                    out_blob BLOB;
                    scan_date DATE;
                    bbox JSON;
                BEGIN
                    BEGIN
                        -- Pobranie najnowszego rasteru
                        SELECT raster, scan_date, bbox
                        INTO gr, scan_date, bbox
                        FROM satellite_scans
                        WHERE field_id = :id
                        ORDER BY scan_date DESC
                        FETCH FIRST 1 ROWS ONLY;

                    EXCEPTION
                        WHEN NO_DATA_FOUND THEN
                            gr := NULL;
                            scan_date := NULL;
                            bbox := NULL;
                    END;

                    IF gr IS NOT NULL THEN
                        DBMS_LOB.CREATETEMPORARY(out_blob, TRUE);
                        sdo_geor.exportTo(gr, '', 'TIFF', out_blob);
                        :result := out_blob;
                    ELSE
                        :result := EMPTY_BLOB();
                    END IF;

                    :scanDate := scan_date;
                    :bboxInfo := bbox;
                END;", conn);

            cmd.BindByName = true;

            cmd.Parameters.Add("id", OracleDbType.Int32).Value = fieldId;
            cmd.Parameters.Add("result", OracleDbType.Blob).Direction = ParameterDirection.Output;
            cmd.Parameters.Add("scanDate", OracleDbType.Date).Direction = ParameterDirection.Output;
            cmd.Parameters.Add("bboxInfo", OracleDbType.Json).Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();

            var blob = (OracleBlob)cmd.Parameters["result"].Value;
            var oracleDate = cmd.Parameters["scanDate"].Value;
            DateTime? scanDate = null;

            if (oracleDate is Oracle.ManagedDataAccess.Types.OracleDate od && !od.IsNull)
                scanDate = od.Value;

            var fieldBbox = cmd.Parameters["bboxInfo"].Value;

            if (blob == null || blob.Length == 0)
                return null;

            return new ScanResult
            {
                ScanDate = scanDate ?? DateTime.MinValue,
                ImageBytes = blob.Value,
                FieldBbox = fieldBbox.ToString()
            };

        }
        catch (Exception ex)
        {
            throw new Exception($"Błąd w GetLatestScanAsync dla fieldId={fieldId}: {ex.Message}", ex);
        }
    }

    public async Task<ScanResult?> GetScanByIdAsync(int scanId)
    {
        try
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new OracleCommand(@"
                DECLARE
                    gr MDSYS.SDO_GEORASTER;
                    out_blob BLOB;
                    scan_date DATE;
                    bbox JSON;
                BEGIN
                    BEGIN
                        -- Pobranie najnowszego rasteru
                        SELECT raster, scan_date, bbox
                        INTO gr, scan_date, bbox
                        FROM satellite_scans
                        WHERE scan_id = :id;

                    EXCEPTION
                        WHEN NO_DATA_FOUND THEN
                            gr := NULL;
                            scan_date := NULL;
                            bbox := NULL;
                    END;

                    IF gr IS NOT NULL THEN
                        DBMS_LOB.CREATETEMPORARY(out_blob, TRUE);
                        sdo_geor.exportTo(gr, '', 'TIFF', out_blob);
                        :result := out_blob;
                    ELSE
                        :result := EMPTY_BLOB();
                    END IF;

                    :scanDate := scan_date;
                    :bboxInfo := bbox;
                END;", conn);

            cmd.BindByName = true;

            cmd.Parameters.Add("id", OracleDbType.Int32).Value = scanId;
            cmd.Parameters.Add("result", OracleDbType.Blob).Direction = ParameterDirection.Output;
            cmd.Parameters.Add("scanDate", OracleDbType.Date).Direction = ParameterDirection.Output;
            cmd.Parameters.Add("bboxInfo", OracleDbType.Json).Direction = ParameterDirection.Output;
            await cmd.ExecuteNonQueryAsync();

            var blob = (OracleBlob)cmd.Parameters["result"].Value;
            var oracleDate = cmd.Parameters["scanDate"].Value;
            DateTime? scanDate = null;

            if (oracleDate is Oracle.ManagedDataAccess.Types.OracleDate od && !od.IsNull)
                scanDate = od.Value;

            var fieldBbox = cmd.Parameters["bboxInfo"].Value;

            if (blob == null || blob.Length == 0)
                return null;

            return new ScanResult
            {
                ScanDate = scanDate ?? DateTime.MinValue,
                ImageBytes = blob.Value,
                FieldBbox = fieldBbox.ToString()
            };

        }
        catch (Exception ex)
        {
            throw new Exception($"Błąd w GetLatestScanAsync dla scanId={scanId}: {ex.Message}", ex);
        }
    }

    public async Task<List<FieldScan>> GetFieldScansAsync(int fieldId)
    {
        // Tworzy połączenie do Oracle
        using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        // Wykonuje SQL SELECT
        var cmd = new OracleCommand(@"SELECT SCAN_ID, FIELD_ID, SCAN_DATE
                                  FROM SATELLITE_SCANS
                                  WHERE FIELD_ID = :fieldId
                                  ORDER BY SCAN_DATE DESC", conn);

        cmd.Parameters.Add(new OracleParameter("fieldId", fieldId));

        var scans = new List<FieldScan>();
        var reader = await cmd.ExecuteReaderAsync();



        while (await reader.ReadAsync())
        {
            var date = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
            scans.Add(new FieldScan
            {
                Id = reader.GetInt32(0),
                FieldId = reader.GetInt32(1),
                ScanDate = (DateTime)date,
            });
        }

        return scans;
    }

    public async Task<bool> DeleteScanAsync(int scanId)
    {
        using var conn = new Oracle.ManagedDataAccess.Client.OracleConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"DELETE FROM SATELLITE_SCANS WHERE scan_id = :scanId";
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("scanId", scanId));

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<List<PlantDto>> GetPlantsAsync()
    {
        var plants = new List<PlantDto>();

        using (var conn = new OracleConnection(_connectionString))
        {
            await conn.OpenAsync();

            var query = "SELECT plantId, plantName FROM plants ORDER BY plantName";

            using (var cmd = new OracleCommand(query, conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    plants.Add(new PlantDto
                    {
                        Id = Convert.ToInt32(reader["plantId"]),
                        Name = reader["plantName"].ToString()
                    });
                }
            }

            Console.WriteLine(plants.Count);
        }

        return plants;
    }
    public async Task<List<ThresholdDto>> GetThresholdsAsync()
    {
        var thresholds = new List<ThresholdDto>();

        using (var conn = new OracleConnection(_connectionString))
        {
            await conn.OpenAsync();

            var query = "SELECT cycleId, minndvi, maxndvi FROM growthcycles";

            using (var cmd = new OracleCommand(query, conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    thresholds.Add(new ThresholdDto
                    {
                        CycleId = Convert.ToInt32(reader["cycleId"]),
                        MinNdvi = Convert.ToDouble(reader["minNdvi"]),
                        MaxNdvi = Convert.ToDouble(reader["maxNdvi"])
                    });
                }
            }
        }

        Console.WriteLine(thresholds.Count);
        return thresholds;
    }
    public bool DeleteUserAccount(string username, string password)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        using var trans = conn.BeginTransaction();
        try
        {
            // 1️⃣ Weryfikacja hasła użytkownika
            using var checkCmd = new OracleCommand(
                "SELECT COUNT(*) FROM users WHERE username = :username AND passwordhash = :password",
                conn
            );
            checkCmd.Parameters.Add(new OracleParameter("username", username));
            checkCmd.Parameters.Add(new OracleParameter("password", password)); // później zastąp hash

            var result = Convert.ToInt32(checkCmd.ExecuteScalar());
            if (result == 0)
            {
                trans.Rollback();
                return false; // hasło niepoprawne
            }

            // 2️⃣ Usuwanie użytkownika (kaskadowe dla wszystkich powiązanych tabel)
            using var deleteCmd = new OracleCommand(
                "DELETE FROM users WHERE username = :username",
                conn
            );
            deleteCmd.Parameters.Add(new OracleParameter("username", username));
            deleteCmd.ExecuteNonQuery();

            trans.Commit();
            return true; // konto usunięte
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }
}



