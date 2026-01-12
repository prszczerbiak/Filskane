using Isopoh.Cryptography.Argon2;

namespace WebApplication1.Services;

/// <summary>
/// Serwis implementujący mechanizm haszowania haseł przy użyciu algorytmu Argon2.
/// </summary>
public class Argon2PasswordHasherService : IPasswordHasherService
{
    /// <summary>
    /// Generuje bezpieczny hash dla podanego hasła.
    /// </summary>
    /// <param name="password">Hasło w formie jawnej (plain text).</param>
    /// <returns>Ciąg znaków zawierający hash, sól oraz parametry konfiguracyjne algorytmu.</returns>
    public string Hash(string password)
    {
        return Argon2.Hash(password);
    }

    /// <summary>
    /// Weryfikuje zgodność podanego hasła z przechowywanym hashem.
    /// </summary>
    /// <param name="password">Hasło do sprawdzenia.</param>
    /// <param name="hash">Oryginalny hash z bazy danych.</param>
    /// <returns>True, jeśli hasło pasuje do hasha; w przeciwnym razie False.</returns>
    public bool Verify(string password, string hash)
    {
        return Argon2.Verify(hash, password);
    }
}
