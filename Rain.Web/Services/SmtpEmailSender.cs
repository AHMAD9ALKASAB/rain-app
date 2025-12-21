using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;

namespace Rain.Web.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        public SmtpEmailSender(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var host = _config["Smtp:Host"] ?? "";
            var portStr = _config["Smtp:Port"] ?? "587";
            var username = _config["Smtp:Username"] ?? "";
            var password = _config["Smtp:Password"] ?? "";
            var fromEmail = _config["Smtp:FromEmail"] ?? username;
            var useStartTls = (_config["Smtp:UseStartTls"] ?? "true").ToLowerInvariant() == "true";

            using var client = new SmtpClient(host, int.TryParse(portStr, out var port) ? port : 587)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = useStartTls
            };

            var mail = new MailMessage
            {
                From = new MailAddress(fromEmail),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            mail.To.Add(email);

            await client.SendMailAsync(mail);
        }
    }
}
