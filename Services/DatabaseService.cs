namespace WebApplication1.Services;

using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Configuration;
using WebApplication1.Models;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public bool ValidateUser(string username, string password)
    {
        using var conn = new OracleConnection(_connectionString);
        
        conn.Open();
        string sql = @"SELECT COUNT(*) 
            FROM Users 
            WHERE Username = :username 
                AND PasswordHash = :password";

        using var cmd = new OracleCommand(sql, conn);
        
        cmd.Parameters.Add(":username", OracleDbType.Varchar2).Value = username;
        cmd.Parameters.Add(":password", OracleDbType.Varchar2).Value = password; // Użyj hashowania!

        int count = Convert.ToInt32(cmd.ExecuteScalar());
        return count > 0;
        
        
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

    public void SaveField(string username, string name, string geojson, double centerX, double centerY, double area, string complex, string type, string substrate)
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
        VALUES (:name, :centerX, :centerY, :userId, :geojson, :area, :soilComplex, :soilType, :soilSubstrate)";

        using var clob = new OracleClob(conn);
        
        clob.Write(geojson.ToCharArray(), 0, geojson.Length);

        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":name", name));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":centerX", centerX));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":centerY", centerY));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":userId", userId));
        var geojsonParam = new OracleParameter
        {
            ParameterName = ":geojson",
            OracleDbType = OracleDbType.Clob,
            Value = clob
        };
        cmd.Parameters.Add(geojsonParam);
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":area", area));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":soilComplex", complex));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":soilType", type));
        cmd.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter(":soilSubstrate", substrate));

        try
        {
            cmd.ExecuteNonQuery();
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
               f.Crop, 
               f.PlantState, 
               f.SowingDate,
               f.SoilComplex,
               f.SoilType,
               f.SoilSubstrate,
               f.Area
        FROM FIELDS f
        JOIN USERS u ON f.UserId = u.UserId
        WHERE u.Username = :username
          AND f.FieldsId = :fieldId";

        using var cmd = new OracleCommand(sql, conn);
        cmd.Parameters.Add(":username", OracleDbType.Varchar2).Value = username;
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
                    Crop = reader.IsDBNull(2) ? "Nieuzupełnione" : reader.GetString(2),
                    PlantState = reader.IsDBNull(3) ? "Nieuzupełnione" : reader.GetString(3),
                    SowingDate = reader.IsDBNull(4) ? "Nieuzupełnione" : reader.GetDateTime(4).ToString("dd-MM-yyyy"),
                    SoilComplex = reader.IsDBNull(5) ? "Nieuzupełnione" : reader.GetString(5),
                    SoilType = reader.IsDBNull(6) ? "Nieuzupełnione" : reader.GetString(6),
                    SoilSubstrate = reader.IsDBNull(7) ? "Nieuzupełnione" : reader.GetString(7),
                    Area = reader.IsDBNull(8) ? 0.0 : reader.GetDouble(8)
            }
            ;
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

    public void SaveFieldChanges(int fieldId, string username, UpdateFieldDto updateDto)
    {
        using var conn = new OracleConnection(_connectionString);
        conn.Open();

        var updates = new List<string>();
        var cmd = new OracleCommand();
        cmd.Connection = conn;

        if (!string.IsNullOrEmpty(updateDto.Crop))
        {
            updates.Add("Crop = :crop");
            cmd.Parameters.Add(":crop", OracleDbType.Varchar2).Value = updateDto.Crop;
        }

        if (!string.IsNullOrEmpty(updateDto.PlantState))
        {
            updates.Add("PlantState = :state");
            cmd.Parameters.Add(":state", OracleDbType.Varchar2).Value = updateDto.PlantState;
        }

        if (updateDto.SowingDate.HasValue)
        {
            updates.Add("SowingDate = :dateSow");
            cmd.Parameters.Add(":dateSow", OracleDbType.Date).Value = updateDto.SowingDate.Value;
        }

        if (!updates.Any())
            throw new ArgumentException("Brak danych do aktualizacji");

        string sql = $@"
        UPDATE FIELDS f
        SET {string.Join(", ", updates)}
        WHERE f.FieldsId = :fieldId
          AND f.UserId = (SELECT UserId FROM USERS WHERE Username = :username)";

        cmd.CommandText = sql;
        cmd.Parameters.Add(":fieldId", OracleDbType.Int32).Value = fieldId;
        cmd.Parameters.Add(":username", OracleDbType.Varchar2).Value = username;

        int rowsAffected = cmd.ExecuteNonQuery();

        if (rowsAffected == 0)
            throw new Exception("Nie znaleziono pola lub brak uprawnień do edycji");
    }
}


