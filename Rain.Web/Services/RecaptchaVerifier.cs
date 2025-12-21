using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Rain.Web.Services
{
    public interface IRecaptchaVerifier
    {
        Task<bool> VerifyAsync(string token, string remoteIp);
        bool Enabled { get; }
        string? SiteKey { get; }
    }

    public class RecaptchaVerifier : IRecaptchaVerifier
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _cfg;
        public RecaptchaVerifier(IHttpClientFactory http, IConfiguration cfg)
        { _http = http; _cfg = cfg; }
        public bool Enabled => !string.IsNullOrWhiteSpace(_cfg["Recaptcha:SecretKey"]) && !string.IsNullOrWhiteSpace(_cfg["Recaptcha:SiteKey"]);
        public string? SiteKey => _cfg["Recaptcha:SiteKey"];        
        public async Task<bool> VerifyAsync(string token, string remoteIp)
        {
            if (!Enabled) return true;
            if (string.IsNullOrWhiteSpace(token)) return false;
            var secret = _cfg["Recaptcha:SecretKey"] ?? string.Empty;
            var client = _http.CreateClient();
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("secret", secret),
                new KeyValuePair<string,string>("response", token),
                new KeyValuePair<string,string>("remoteip", remoteIp ?? string.Empty)
            });
            var resp = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", form);
            if (!resp.IsSuccessStatusCode) return false;
            var data = await resp.Content.ReadFromJsonAsync<RecaptchaResponse>();
            return data?.Success == true;
        }
        private class RecaptchaResponse
        {
            [JsonPropertyName("success")] public bool Success { get; set; }
        }
    }
}
