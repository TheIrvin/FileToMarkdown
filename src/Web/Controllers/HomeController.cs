using Microsoft.AspNetCore.Mvc;

namespace MarkItDownWeb.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Conversion");
}
