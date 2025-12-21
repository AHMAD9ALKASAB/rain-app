using System.Text;
using Microsoft.Extensions.Localization;

namespace Rain.Web.Services
{
    public static class HtmlEmailBuilder
    {
        public static string Build(IStringLocalizer<SharedResource> L, string subject, string contentHtml)
        {
            // Prefer PNG for email clients (SVG not supported everywhere)
            var logo = "/images/logo.png";
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"ar\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>");
            sb.Append(System.Net.WebUtility.HtmlEncode(subject));
            sb.Append("</title>");
            sb.Append("<style>body{background:#f6f7fb;margin:0;padding:24px;font-family:Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#222} .container{max-width:640px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.06)} .header{display:flex;align-items:center;gap:12px;padding:16px 20px;border-bottom:1px solid #eee} .brand{font-weight:700} .content{padding:20px;font-size:15px;line-height:1.7} .footer{padding:12px 20px;color:#666;font-size:12px;border-top:1px solid #f0f0f0;text-align:center}</style>");
            sb.Append("</head><body><div class=\"container\"><div class=\"header\">");
            sb.Append($"<img src='{logo}' alt='{L["AppName"]}' height='28'>");
            sb.Append($"<div class='brand'>{L["AppName"]}</div>");
            sb.Append("</div><div class=\"content\">");
            sb.Append(contentHtml);
            sb.Append("</div><div class=\"footer\">© ");
            sb.Append(System.DateTime.UtcNow.Year);
            sb.Append(" ");
            sb.Append(L["AppName"]);
            sb.Append("</div></div></body></html>");
            return sb.ToString();
        }
    }
}
