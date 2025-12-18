using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace WebApplication1.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;
        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(string toEmail, string token)
        {
            string verificationLink = $"https://localhost:7273/api/auth/verify?token={token}";

            using var message = new MailMessage();
            message.From = new MailAddress(_config["Smtp:User"]); // nadawca musi być tym samym kontem co logowanie
            message.To.Add(toEmail);
            message.Subject = "Weryfikacja konta";
            message.Body = $@"
                <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h3>Witaj!</h3>
                        <p>Dziękujemy za rejestrację. Aby aktywować konto, kliknij poniższy przycisk:</p>
                        <p style='margin: 10px;'>
                            <a href='{verificationLink}' 
                               style='background-color:#4CAF50;color:white;padding:10px 20px;
                                      text-decoration:none;border-radius:5px;'>
                                Aktywuj konto
                            </a>
                        </p>
                        <p>Lub skopiuj ten link do przeglądarki: <br/>
                            <a href='{verificationLink}'>{verificationLink}</a>
                        </p>
                    </body>
                </html>";
            message.IsBodyHtml = true;


            using var client = new SmtpClient(_config["Smtp:Host"], int.Parse(_config["Smtp:Port"]));
            client.UseDefaultCredentials = false; // ważne!
            client.Credentials = new NetworkCredential(_config["Smtp:User"], _config["Smtp:Pass"]);
            client.EnableSsl = true;

            try
            {
                await client.SendMailAsync(message); // <-- asynchronicznie
                _logger.LogInformation("Mail weryfikacyjny wysłany do {Email}", toEmail);
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "Błąd SMTP przy wysyłce maila weryfikacyjnego do {Email}", toEmail);
                throw new InvalidOperationException($"Błąd SMTP: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd przy wysyłce maila do {Email}", toEmail);
                throw new InvalidOperationException($"Nieoczekiwany błąd przy wysyłce maila: {ex.Message}", ex);
            }
        }
    }
}
