using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;

namespace Rain.Web.Services
{
    public class NoOpEmailSender : IEmailSender
    {
        private readonly ILogger<NoOpEmailSender> _logger;

        public NoOpEmailSender(ILogger<NoOpEmailSender> logger = null)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            _logger?.LogInformation("Email would be sent to {Email} with subject: {Subject}", email, subject);
            _logger?.LogDebug("Email content: {Content}", htmlMessage);
            
            // لا تفعل شيئًا - فقط لأغراض التطوير
            return Task.CompletedTask;
        }
    }
}
