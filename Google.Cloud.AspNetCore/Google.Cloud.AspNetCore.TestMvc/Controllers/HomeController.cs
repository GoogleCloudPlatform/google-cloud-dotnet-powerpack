using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Google.Cloud.AspNetCore.TestMvc.Models;
using Microsoft.AspNetCore.Http;

namespace Google.Cloud.AspNetCore.TestMvc.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            HttpContext.Session.SetString("LastPage", "Index");
            return View();
        }

        public IActionResult Privacy()
        {
            HttpContext.Session.GetString("LastPage");
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
