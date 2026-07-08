using System.Net;
using System.Net.Mail;

namespace Filskane.Services
{
    /// <summary>
    /// Serwis infrastrukturalny odpowiedzialny za wysyłkę wiadomości e-mail (SMTP).
    /// </summary>
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Wysyła wiadomość e-mail z linkiem aktywacyjnym do nowo zarejestrowanego użytkownika.
        /// </summary>
        /// <param name="toEmail">Adres e-mail odbiorcy.</param>
        /// <param name="token">Unikalny token weryfikacyjny.</param>
        /// <exception cref="SmtpException">Rzucany w przypadku błędu połączenia z serwerem pocztowym.</exception>
        public async Task SendVerificationEmailAsync(string toEmail, string token)
        {
            string baseUrl = _config["AppUrl"];
            string verificationLink = $"{baseUrl}/api/auth/verify?token={Uri.EscapeDataString(token)}";

            using var message = new MailMessage();
            message.From = new MailAddress(_config["Smtp:User"]);
            message.To.Add(toEmail);
            message.Subject = "Weryfikacja konta - System Filskane";

            message.Body = $@"
            <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h3>Witaj w systemie Filskane!</h3>
                    <p>Kliknij poniższy link, aby aktywować konto:</p>
                    <a href='{verificationLink}'>Aktywuj konto</a>
                </body>
            </html>";
            message.IsBodyHtml = true;

            using var client = new SmtpClient(_config["Smtp:Host"], int.Parse(_config["Smtp:Port"]));
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_config["Smtp:User"], _config["Smtp:Pass"]);
            client.EnableSsl = true;

            try
            {
                await client.SendMailAsync(message);
                _logger.LogInformation("Wysłano e-mail weryfikacyjny do: {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd wysyłki e-maila do {Email}", toEmail);
                throw;
            }
        }

        public async Task SendEmailWithAttachmentAsync(string toEmail, string subject, string body, byte[] attachmentBytes, string attachmentFileName)
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_config["Smtp:User"]);
            message.To.Add(toEmail);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var attachmentStream = new MemoryStream(attachmentBytes);
            using var attachment = new Attachment(attachmentStream, attachmentFileName, "application/pdf");
            message.Attachments.Add(attachment);

            using var client = new SmtpClient(_config["Smtp:Host"], int.Parse(_config["Smtp:Port"]));
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_config["Smtp:User"], _config["Smtp:Pass"]);
            client.EnableSsl = true;

            try
            {
                await client.SendMailAsync(message);
                _logger.LogInformation("Wysłano e-mail z załącznikiem do: {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd wysyłki e-maila z załącznikiem do {Email}", toEmail);
                throw;
            }
        }
    }
}