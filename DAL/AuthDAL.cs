using System.Data;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.DAL
{
    /// <summary>
    /// Warstwa dostępu do danych dotycząca autoryzacji.
    /// </summary>
    public class AuthDAL : BaseDAL
    {
        private readonly IPasswordHasherService _hasher;
        private readonly ILogger<AuthDAL> _logger;

        public AuthDAL(
            IConfiguration configuration,
            IPasswordHasherService hasher,
            ILogger<AuthDAL> logger)
            : base(configuration)
        {
            _hasher = hasher;
            _logger = logger;
        }

        /// <summary>
        /// Weryfikuje poprawność poświadczeń użytkownika (Login + Hasło) oraz status weryfikacji konta.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <param name="password">Hasło w formie jawnej.</param>
        /// <returns>True, jeśli dane są poprawne i konto jest aktywne.</returns>
        public async Task<bool> ValidateUserAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return false;

            const string sql = @"
                SELECT PASSWORD_HASH 
                FROM USERS 
                WHERE USERNAME = :username 
                  AND IS_VERIFIED = 1";

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();

                await using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

                var result = await cmd.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value)
                {
                    _logger.LogDebug("Nieudane logowanie: nie znaleziono aktywnego użytkownika {Username}", username);
                    return false;
                }

                string storedHash = result.ToString()!;
                bool isPasswordValid = _hasher.Verify(password, storedHash);

                if (!isPasswordValid)
                {
                    _logger.LogDebug("Nieudane logowanie: błędne hasło dla użytkownika {Username}", username);
                }

                return isPasswordValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd bazy danych podczas walidacji użytkownika {Username}", username);
                return false;
            }
        }

        /// <summary>
        /// Rejestruje nowego użytkownika w bazie danych.
        /// </summary>
        /// <param name="request">Dane rejestracyjne.</param>
        /// <param name="token">Wygenerowany token weryfikacyjny.</param>
        /// <returns>True, jeśli utworzono rekord.</returns>
        public async Task<bool> RegisterUserAsync(RegisterRequest request, string token)
        {
            string hashedPassword = _hasher.Hash(request.Password);

            const string sql = @"
                INSERT INTO USERS (FIRST_NAME, USERNAME, EMAIL, PASSWORD_HASH, VERIFICATION_TOKEN)
                VALUES (:name, :username, :email, :passwordhash, :verificationToken)";

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();

                await using var cmd = new OracleCommand(sql, conn);

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
                // ORA-00001: Naruszenie unikalności (Unique Constraint Violation)
                if (ex.Number == 1)
                {
                    _logger.LogWarning("Próba rejestracji na zajęte dane (Username/Email): {Username}, {Email}", request.Username, request.Email);
                    return false;
                }

                _logger.LogError(ex, "Krytyczny błąd Oracle podczas rejestracji użytkownika {Username}", request.Username);
                throw;
            }
        }

        /// <summary>
        /// Weryfikuje konto użytkownika na podstawie tokena email i aktywuje je.
        /// </summary>
        /// <param name="token">Token weryfikacyjny.</param>
        /// <returns>True, jeśli weryfikacja się powiodła.</returns>
        public async Task<bool> VerifyUserAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            const string sql = @"
                UPDATE USERS 
                SET IS_VERIFIED = 1, VERIFICATION_TOKEN = NULL 
                WHERE VERIFICATION_TOKEN = :token";

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();

                await using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add("token", OracleDbType.Varchar2).Value = token;

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Pomyślnie zweryfikowano konto przy użyciu tokena.");
                    return true;
                }

                _logger.LogWarning("Nieudana weryfikacja: token nie istnieje lub wygasł.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd bazy danych podczas weryfikacji tokena.");
                return false;
            }
        }

        /// <summary>
        /// Sprawdza, czy podany adres e-mail jest już zarejestrowany w bazie.
        /// </summary>
        /// <param name="email">Adres e-mail do sprawdzenia.</param>
        public async Task<bool> CheckIfEmailExistsAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;

            const string sql = "SELECT COUNT(1) FROM USERS WHERE EMAIL = :email";

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();

                await using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add("email", OracleDbType.Varchar2).Value = email;

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd sprawdzania dostępności emaila {Email}", email);
                return false;
            }
        }

        /// <summary>
        /// Trwale usuwa konto użytkownika po ponownej weryfikacji hasła.
        /// </summary>
        /// <param name="username">Nazwa użytkownika.</param>
        /// <param name="password">Hasło potwierdzające.</param>
        /// <returns>True, jeśli usunięto konto.</returns>
        public async Task<bool> DeleteUserAccountAsync(string username, string password)
        {
            // Ponowna weryfikacja tożsamości przed operacją destrukcyjną
            bool isValid = await ValidateUserAsync(username, password);
            if (!isValid)
            {
                _logger.LogWarning("Próba usunięcia konta {Username} odrzucona: nieprawidłowe hasło.", username);
                return false;
            }

            const string deleteSql = "DELETE FROM USERS WHERE USERNAME = :username";

            try
            {
                await using var conn = CreateConnection();
                await conn.OpenAsync();

                await using var cmd = new OracleCommand(deleteSql, conn);
                cmd.Parameters.Add("username", OracleDbType.Varchar2).Value = username;

                int rows = await cmd.ExecuteNonQueryAsync();

                if (rows > 0)
                {
                    _logger.LogInformation("Konto użytkownika {Username} zostało usunięte z bazy danych.", username);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania konta {Username}", username);
                return false;
            }
        }
    }
}