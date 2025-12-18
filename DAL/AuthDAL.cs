namespace WebApplication1.DAL;

using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using WebApplication1.Models;   // Tutaj powinny być Twoje DTO (RegisterRequest itp.)
using WebApplication1.Services; // Tutaj powinien być IPasswordHasherService

public class AuthDAL : BaseDAL
{
    private readonly IPasswordHasherService _hasher;

    // Konstruktor pobiera konfigurację dla klasy bazowej oraz Hasher dla tej klasy
    public AuthDAL(IConfiguration configuration, IPasswordHasherService hasher)
        : base(configuration)
    {
        _hasher = hasher;
    }

    /// <summary>
    /// Sprawdza czy użytkownik istnieje i czy hasło jest poprawne.
    /// </summary>
    public async Task<bool> ValidateUserAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            string sql = @"SELECT PASSWORD_HASH 
                           FROM USERS
                           WHERE USERNAME = :username 
                             AND IS_VERIFIED = 1";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            // Pobieramy hash z bazy
            var result = await cmd.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                return false;

            string storedHash = result.ToString();

            // Weryfikacja hasła przez serwis zewnętrzny
            return _hasher.Verify(password, storedHash);
        }
        catch
        {
            // W przypadku błędu bazy danych bezpieczniej jest zwrócić false
            return false;
        }
    }

    /// <summary>
    /// Rejestruje nowego użytkownika w bazie danych.
    /// </summary>
    public async Task<bool> RegisterUserAsync(RegisterRequest request, string token)
    {
        // Hashowanie hasła przed zapisem
        string hashedPassword = _hasher.Hash(request.Password);

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            string sql = @"INSERT INTO USERS (FIRST_NAME, USERNAME, EMAIL, PASSWORD_HASH, VERIFICATION_TOKEN)
                           VALUES (:name, :username, :email, :passwordhash, :verificationToken)";

            await using var cmd = new OracleCommand(sql, conn);

            // Parametryzacja zapytania
            cmd.Parameters.Add("name", OracleDbType.Varchar2).Value = request.Name;
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = request.Username;
            cmd.Parameters.Add("email", OracleDbType.Varchar2).Value = request.Email;
            cmd.Parameters.Add("passwordhash", OracleDbType.Varchar2).Value = hashedPassword;
            cmd.Parameters.Add("verificationToken", OracleDbType.Varchar2).Value = token;

            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (OracleException ex)
        {
            // ORA-00001: Naruszenie unikalności (zajęty login lub email)
            if (ex.Number == 1)
            {
                return false;
            }
            throw; // Inne błędy rzucamy wyżej (np. brak połączenia)
        }
    }

    /// <summary>
    /// Weryfikuje konto użytkownika na podstawie tokena email.
    /// </summary>
    public async Task<bool> VerifyUserAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            // Ustawiamy IS_VERIFIED na 1 i czyścimy token, aby nie można go było użyć ponownie
            string sql = @"UPDATE USERS 
                           SET IS_VERIFIED = 1, VERIFICATION_TOKEN = NULL 
                           WHERE VERIFICATION_TOKEN = :token";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("token", OracleDbType.Varchar2).Value = token;

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sprawdza, czy podany email jest już zajęty.
    /// </summary>
    public async Task<bool> CheckIfEmailExistsAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            string sql = "SELECT COUNT(1) FROM USERS WHERE EMAIL = :email";

            await using var cmd = new OracleCommand(sql, conn);
            cmd.Parameters.Add("email", OracleDbType.Varchar2).Value = email;

            var result = await cmd.ExecuteScalarAsync();

            return Convert.ToInt32(result) > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Usuwa konto użytkownika po ponownej weryfikacji hasła.
    /// </summary>
    public async Task<bool> DeleteUserAccountAsync(string username, string password)
    {
        // KROK 1: Reużywamy metody ValidateUserAsync, aby sprawdzić poprawność hasła.
        // Dzięki temu nie powielamy logiki pobierania i porównywania hasha.
        bool isValid = await ValidateUserAsync(username, password);

        if (!isValid) return false;

        // KROK 2: Usuwanie konta
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            string deleteSql = "DELETE FROM USERS WHERE USERNAME = :username";

            await using var cmd = new OracleCommand(deleteSql, conn);
            cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch
        {
            return false;
        }
    }
}