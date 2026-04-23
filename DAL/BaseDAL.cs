using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace Filskane.DAL
{
    /// <summary>
    /// Abstrakcyjna klasa bazowa dla wszystkich komponentów warstwy dostępu do danych (DAL).
    /// Centralizuje zarządzanie ciągiem połączenia (Connection String) do bazy Oracle.
    /// </summary>
    public abstract class BaseDAL
    {
        private readonly string _connectionString;

        protected BaseDAL(IConfiguration configuration)
        {
            // Pobieramy ConnectionString raz przy inicjalizacji serwisu.
            // Jeśli go brakuje, rzucany jest krytyczny błąd, bo aplikacja nie może działać bez bazy.
            _connectionString = configuration.GetConnectionString("OracleDb")
                                ?? throw new InvalidOperationException("Nie znaleziono klucza 'OracleDb' w sekcji ConnectionStrings w appsettings.json.");

            Console.WriteLine(_connectionString);
        }

        /// <summary>
        /// Tworzy nową, nieotwartą instancję połączenia z bazą danych.
        /// </summary>
        /// <remarks>
        /// Należy pamiętać o używaniu konstrukcji 'using' lub 'await using' na zwróconym obiekcie,
        /// aby poprawnie zarządzać pulą połączeń.
        /// </remarks>
        /// <returns>Obiekt <see cref="OracleConnection"/> gotowy do otwarcia.</returns>
        protected OracleConnection CreateConnection()
        {
            return new OracleConnection(_connectionString);
        }
    }
}