using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rain.Infrastructure.Persistence;

namespace Rain.Web.Controllers
{
    [ApiController]
    public class SitemapController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public SitemapController(ApplicationDbContext db) { _db = db; }

        [HttpGet]
        [Route("sitemap.xml")]
        public async Task<IActionResult> Sitemap()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            void AddUrl(string path)
            {
                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{baseUrl}{path}</loc>");
                sb.AppendLine("  </url>");
            }

            AddUrl("/");
            AddUrl("/Products");

            var productIds = await _db.Products.AsNoTracking().Select(p => p.Id).Take(1000).ToListAsync();
            foreach (var id in productIds)
            {
                AddUrl($"/Products/Details/{id}");
            }

            sb.AppendLine("</urlset>");
            return Content(sb.ToString(), "application/xml", Encoding.UTF8);
        }
    }
}
