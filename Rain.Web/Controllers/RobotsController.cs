using Microsoft.AspNetCore.Mvc;

namespace Rain.Web.Controllers
{
    [ApiController]
    public class RobotsController : ControllerBase
    {
        [HttpGet]
        [Route("robots.txt")]
        public IActionResult Robots()
        {
            var content = "User-agent: *\nAllow: /\nSitemap: /sitemap.xml\n";
            return Content(content, "text/plain");
        }
    }
}
