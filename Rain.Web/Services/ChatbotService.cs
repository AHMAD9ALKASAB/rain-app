using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Rain.Web.Services;

public sealed record ChatbotReply(string Message, bool FromProvider, string Source);
public sealed record ChatMessage(string Role, string Content);

public interface IChatbotService
{
    Task<ChatbotReply> GetReplyAsync(IReadOnlyList<ChatMessage> history, string? culture, CancellationToken cancellationToken = default);
}

public class ChatbotService : IChatbotService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatbotService> _logger;
    private static readonly (string[] Keywords, string Ar, string En)[] LocalIntents =
    {
        (new[] { "مرحبا", "مرحبا", "hello", "hi", "السلام" },
            "أهلًا بك! يسعدني مساعدتك في أي شيء يخص منصة رين. اسألني عن التسجيل، الدفع، الحسابات أو أي نقطة تدور في ذهنك.",
            "Hello! I'm here to help you with anything about Rain. Ask me about onboarding, payments, accounts, or anything else."),
        (new[] { "مورد", "supplier", "عرض منتجات", "توريد", "اعتماد" },
            "لتصبح موردًا: أنشئ حسابًا، أرسل طلب الاعتماد عبر لوحة الموردين، وبعد الموافقة يمكنك رفع منتجاتك وإدارة عروضك وتتبع أرباحك.",
            "To become a supplier: create an account, submit the supplier approval form, and once approved you can publish products, manage offers, and track revenue."),
        (new[] { "تسجيل", "register", "حساب", "sign up" },
            "اختَر نوع الحساب (فرد، محل، مورد)، ثم أكمل بياناتك، فعّل بريدك الإلكتروني، وبعدها ستتمكن من تصفح المنتجات أو إدارة متجرك.",
            "Pick the account type (individual, shop, supplier), fill in the details, confirm your email, and you’ll be ready to browse products or manage your storefront."),
        (new[] { "دفع", "مدفوعات", "payment", "stripe" },
            "نستخدم Stripe بواجهات دفع مشفّرة، إشعارات Webhook موقعة، وتتبع كامل لحالات الدفع (نجاح، فشل، استرداد).",
            "We rely on Stripe with encrypted checkout, signed webhook notifications, and full tracking of payment states (success, failure, refunds)."),
        (new[] { "شحن", "توصيل", "delivery", "shipping" },
            "يمكنك متابعة الطلبات والشحن من لوحة التحكم؛ الإشعارات تخبرك بحالة الطلب لحظة بلحظة، ويمكن تحديث العناوين بسهولة.",
            "You can track orders and shipping from the dashboard, get instant notifications for status changes, and adjust delivery addresses anytime."),
        (new[] { "مشكلة", "support", "مساعدة", "help", "دعم" },
            "لأي مشكلة تقنية أو تجارية استخدم نموذج التواصل في الصفحة الرئيسية أو أرسل رسالة إلى فريق الدعم وسيتم الرد خلال وقت قصير.",
            "For any technical or business issue, use the contact form on the home page or message the support team and we’ll get back shortly."),
        (new[] { "منتج", "product", "offers", "عرض", "كتالوج" },
            "يمكنك استعراض المنتجات حسب الفئات أو البحث المباشر، مع إمكانية فلترة العروض بناءً على السعر أو الحالة أو المورد.",
            "Browse products by category or search directly, then filter offers by price, status, or supplier as needed."),
        (new[] { "سلة", "cart", "order", "طلب" },
            "أضف العناصر إلى السلة، حدّد الكميات، ثم تابع الدفع الآمن. تتوفر إشعارات لحالة الطلب ويمكنك مراجعة السجّل في أي وقت.",
            "Add items to your cart, choose quantities, and continue to secure checkout. You’ll receive notifications for each order state and can review history anytime.")
    };

    public ChatbotService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ChatbotService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ChatbotReply> GetReplyAsync(IReadOnlyList<ChatMessage> history, string? culture, CancellationToken cancellationToken = default)
    {
        if (history == null || history.Count == 0)
        {
            return new ChatbotReply(LocalDefault("ar", string.Empty), false, "local-default");
        }

        var latestUser = history.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;
        var lang = string.IsNullOrWhiteSpace(culture)
            ? (_configuration["Chatbot:DefaultCulture"] ?? "ar")
            : culture;

        var provider = _configuration["Chatbot:Provider"] ?? "Local";
        if (string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            var apiKey = _configuration["Chatbot:OpenAI:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    var reply = await CallOpenAiAsync(history, lang, apiKey, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(reply))
                    {
                        return new ChatbotReply(reply!, true, "openai");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch ChatGPT response.");
                }
            }
        }

        return new ChatbotReply(BuildLocalAnswer(lang, latestUser), false, "local-default");
    }

    private async Task<string?> CallOpenAiAsync(IReadOnlyList<ChatMessage> history, string culture, string apiKey, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        var systemPrompt = _configuration["Chatbot:OpenAI:SystemPrompt"]
                           ?? "You are Rain, an AI assistant helping users understand an Arabic/English commerce platform. Reply in the same language of the question and keep answers concise.";
        var model = _configuration["Chatbot:OpenAI:Model"] ?? "gpt-3.5-turbo";

        var convertedMessages = new List<object>();
        convertedMessages.Add(new { role = "system", content = systemPrompt });
        foreach (var message in history)
        {
            if (string.IsNullOrWhiteSpace(message.Content)) continue;
            var role = message.Role?.ToLowerInvariant();
            if (role is not ("user" or "assistant" or "system"))
            {
                role = "user";
            }
            convertedMessages.Add(new { role, content = message.Content });
        }

        var payload = new
        {
            model,
            temperature = 0.3,
            max_tokens = 220,
            messages = convertedMessages
        };

        request.Content = JsonContent.Create(payload);
        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Chatbot provider returned {Status}: {Body}", response.StatusCode, body);
            return null;
        }

        var data = await response.Content.ReadFromJsonAsync<OpenAiResponse>(cancellationToken: cancellationToken);
        return data?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
    }

    private static string BuildLocalAnswer(string? culture, string userMessage)
    {
        userMessage ??= string.Empty;
        var isArabic = (culture ?? "ar").StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        var normalized = userMessage.ToLowerInvariant();

        foreach (var intent in LocalIntents)
        {
            if (intent.Keywords.Any(k => normalized.Contains(k)))
            {
                return isArabic ? intent.Ar : intent.En;
            }
        }

        var quoted = string.IsNullOrWhiteSpace(userMessage) ? (isArabic ? "سؤالك" : "your request") : $"\"{userMessage}\"";
        return isArabic
            ? $"أفهم أنك تسأل عن {quoted}. أخبرني بالمزيد عمّا تحتاجه (مثلاً التسجيل، الدفع، الشحن، أو الدعم) وسأوجّهك بالخطوات المناسبة."
            : $"I understand you’re asking about {quoted}. Let me know if it relates to onboarding, payments, shipping, or support and I’ll guide you through the steps.";
    }

    private sealed class OpenAiResponse
    {
        [JsonPropertyName("choices")] public Choice[]? Choices { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")] public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}
