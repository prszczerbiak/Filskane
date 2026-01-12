using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

/// <summary>
/// Model danych przesyłany podczas próby logowania.
/// </summary>
/// <param name="Username">Nazwa użytkownika.</param>
/// <param name="Password">Hasło użytkownika.</param>
public record LoginRequest(string Username, string Password);

/// <summary>
/// Model danych wymagany do rejestracji nowego konta w systemie.
/// </summary>
/// <param name="Name">Imię lub nazwa wyświetlana użytkownika.</param>
/// <param name="Username">Unikalny login.</param>
/// <param name="Email">Adres e-mail.</param>
/// <param name="Password">Hasło (powinno spełniać politykę bezpieczeństwa).</param>
public record RegisterRequest(string Name, string Username, string Email, string Password);

/// <summary>
/// Żądanie usunięcia konta wymagające potwierdzenia hasłem.
/// </summary>
/// <param name="Password">Hasło użytkownika w celu weryfikacji tożsamości przed usunięciem.</param>
public record DeleteAccountRequest(string Password);

/// <summary>
/// Model służący do weryfikacji dostępności lub poprawności formatu adresu e-mail.
/// </summary>
public record CheckEmailRequest(
    [Required(ErrorMessage = "Email jest wymagany.")]
    [EmailAddress(ErrorMessage = "Nieprawidłowy format adresu e-mail.")]
    string Email
);

/// <summary>
/// Wynik operacji logowania zwracany przez warstwę serwisu.
/// </summary>
/// <param name="Success">Określa, czy logowanie zakończyło się sukcesem.</param>
/// <param name="Token">Wygenerowany token JWT (tylko w przypadku sukcesu).</param>
/// <param name="ErrorMessage">Komunikat błędu (tylko w przypadku niepowodzenia).</param>
public record LoginResult(bool Success, string? Token = null, string? ErrorMessage = null);