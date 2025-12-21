using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Rain.Web.Models;

namespace Rain.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;

    public HomeController(ILogger<HomeController> logger, IEmailSender emailSender, IConfiguration config)
    {
        _logger = logger;
        _emailSender = emailSender;
        _config = config;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ContactLead([FromForm] ContactLeadViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide valid contact details.";
            return RedirectToAction(nameof(Index));
        }
        var adminEmail = _config["Admin:Email"] ?? "admin@rain.local";
        var subject = $"New Business Lead - {model.Company ?? model.Name}";
        var body = $"Name: {model.Name}\nEmail: {model.Email}\nCompany: {model.Company}\nMessage:\n{model.Message}";
        await _emailSender.SendEmailAsync(adminEmail, subject, body);
        TempData["Success"] = "تم استلام رسالتك وسيتم التواصل معك قريباً.";
        return RedirectToAction(nameof(Index));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
