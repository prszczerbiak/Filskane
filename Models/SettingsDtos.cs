namespace WebApplication1.Models;

// ==========================================
// REQUESTS (Dane wejściowe - od Frontendu)
// Konwencja: [Akcja][Co]Request
// ==========================================

// Zmiana imienia
public record UpdateNameRequest(string Name);

// Zmiana emaila
public record UpdateEmailRequest(string Email);

// Zmiana telefonu
public record UpdatePhoneRequest(string Phone);

// Zmiana jednostki powierzchni (zmienione z SurfaceUpdateDto na Request dla spójności)
public record UpdateSurfaceRequest(int Surface); // 0=ha, 1=a, 2=ac

// Zmiana motywu (zmienione z ThemeUpdateDto na Request)
public record UpdateThemeRequest(int Theme); // 0=light, 1=dark


// ==========================================
// RESPONSES (Dane wyjściowe - do Frontendu)
// Konwencja: [Zasób][Szczegółowość]Dto
// ==========================================

// Krótkie info (np. do nagłówka strony)
public record UserShortDto(
    string Name,
    int DarkMode,
    int Surface,
    double? FarmX,
    double? FarmY
);

// Pełne info (np. do zakładki "Twój Profil")
// Zmieniliśmy nazwę z UserLongDto na UserDetailDto (bardziej profesjonalnie)
public record UserDetailDto(
    string Username,
    string Name,
    string Email,
    string? Phone,
    double? FarmX,
    double? FarmY
);