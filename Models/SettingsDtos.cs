namespace Filskane.Models;

/// <summary>
/// Żądanie zmiany imienia użytkownika.
/// </summary>
/// <param name="Name">Nowe imię.</param>
public record UpdateNameRequest(string Name);

/// <summary>
/// Żądanie aktualizacji adresu e-mail.
/// </summary>
/// <param name="Email">Nowy adres e-mail.</param>
public record UpdateEmailRequest(string Email);

/// <summary>
/// Żądanie zmiany numeru telefonu.
/// </summary>
/// <param name="Phone">Nowy numer telefonu.</param>
public record UpdatePhoneRequest(string Phone);

/// <summary>
/// Żądanie zmiany preferowanej jednostki powierzchni.
/// </summary>
/// <param name="Surface">Wartość liczbowa reprezentująca jednostkę (np. 0 = ha, 1 = ar, 2 = ac).</param>
public record UpdateSurfaceRequest(int Surface);

/// <summary>
/// Żądanie zmiany motywu aplikacji.
/// </summary>
/// <param name="DarkMode">Wartość określająca tryb (0 = jasny, 1 = ciemny).</param>
public record UpdateThemeRequest(int DarkMode);

/// <summary>
/// Skrócony obiekt DTO z danymi użytkownika (używany np. w nagłówku aplikacji).
/// </summary>
/// <param name="Name">Imię użytkownika.</param>
/// <param name="DarkMode">Ustawienie trybu ciemnego.</param>
/// <param name="Surface">Preferowana jednostka powierzchni.</param>
/// <param name="FarmX">Długość geograficzna farmy (jeśli ustawiona).</param>
/// <param name="FarmY">Szerokość geograficzna farmy (jeśli ustawiona).</param>
public record UserShortDto(
    string Name,
    int DarkMode,
    int Surface,
    double? FarmX,
    double? FarmY
);

/// <summary>
/// Szczegółowe dane profilowe użytkownika.
/// </summary>
/// <param name="Username">Unikalna nazwa użytkownika (login).</param>
/// <param name="Name">Imię.</param>
/// <param name="Email">Adres e-mail.</param>
/// <param name="Phone">Numer telefonu.</param>
/// <param name="FarmX">Długość geograficzna farmy.</param>
/// <param name="FarmY">Szerokość geograficzna farmy.</param>
public record UserDetailDto(
    string Username,
    string Name,
    string Email,
    string? Phone,
    double? FarmX,
    double? FarmY
);