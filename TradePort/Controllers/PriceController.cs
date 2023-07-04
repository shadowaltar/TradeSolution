using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TradePort.Controllers;
[ApiController]
[Route("[controller]")]
public class PriceController : Controller
{
    // GET: PriceController
    [HttpGet("Index")]
    public ActionResult Index()
    {
        return View();
    }

    // GET: PriceController/Details/5
    [HttpGet("Details")]
    public ActionResult Details(int id)
    {
        return View();
    }

    // GET: PriceController/Create
    [HttpGet("Create")]
    public ActionResult Create()
    {
        return View();
    }

    // POST: PriceController/Create
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public ActionResult Create(IFormCollection collection)
    {
        try
        {
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            return View();
        }
    }

    // GET: PriceController/Edit/5
    [HttpGet("Edit/{id}")]
    public ActionResult Edit(int id)
    {
        return View();
    }

    // POST: PriceController/Edit/5
    [HttpPost("Edit")]    
    [ValidateAntiForgeryToken]
    public ActionResult Edit(int id, IFormCollection collection)
    {
        try
        {
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            return View();
        }
    }
}
