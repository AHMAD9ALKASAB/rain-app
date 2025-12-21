using Microsoft.AspNetCore.Mvc;
using Rain.Web.Services;

namespace Rain.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbot;
    private readonly ILogger<ChatbotController> _logger;

    public ChatbotController(IChatbotService chatbot, ILogger<ChatbotController> logger)
    {
        _chatbot = chatbot;
        _logger = logger;
    }

    public sealed class ChatbotRequest
    {
        public string? Message { get; init; }
        public List<ChatMessageDto>? History { get; init; }
    }

    public sealed class ChatMessageDto
    {
        public string? Role { get; init; }
        public string? Content { get; init; }
    }

    public sealed class ChatbotResponse
    {
        public required string Message { get; init; }
        public bool FromProvider { get; init; }
        public required string Source { get; init; }
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatbotRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        var culture = System.Globalization.CultureInfo.CurrentUICulture?.TwoLetterISOLanguageName ?? "ar";

        var history = request.History?
            .Where(m => !string.IsNullOrWhiteSpace(m?.Content))
            .Select(m => new ChatMessage(
                string.IsNullOrWhiteSpace(m!.Role) ? "user" : m.Role!,
                m.Content!.Trim()))
            .ToList() ?? new List<ChatMessage>();

        history.Add(new ChatMessage("user", request.Message.Trim()));

        var reply = await _chatbot.GetReplyAsync(history, culture, cancellationToken);

        _logger.LogInformation("Chatbot reply served via {Source}", reply.Source);

        return Ok(new ChatbotResponse
        {
            Message = reply.Message,
            FromProvider = reply.FromProvider,
            Source = reply.Source
        });
    }
}
