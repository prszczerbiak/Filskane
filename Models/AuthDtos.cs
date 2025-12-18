using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models; // File-scoped namespace (mniej klamerek)

// ==========================================
// AUTH REQUESTS
// ==========================================

public record LoginRequest(string Username, string Password);

public record RegisterRequest(string Name, string Username, string Email, string Password);

public record DeleteAccountRequest(string Password);

// Zmiana nazwy z EmailRequest na CheckEmailRequest (bardziej precyzyjne)
public record CheckEmailRequest(
    [property: Required(ErrorMessage = "Email jest wymagany.")]
    [property: EmailAddress(ErrorMessage = "Nieprawidłowy format adresu e-mail.")]
    string Email
);


// ==========================================
// AUTH RESPONSES
// ==========================================

// LoginResult jest super nazwą dla wyniku operacji serwisu.
public record LoginResult(bool Success, string? Token = null, string? ErrorMessage = null);