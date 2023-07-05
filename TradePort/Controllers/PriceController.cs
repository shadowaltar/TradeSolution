using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.Utils;

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
    // GET: PriceController
    [HttpGet("{exchange}/{ticker}/prices")]
    public ActionResult Prices([FromQuery(Name = "interval")] string intervalStr,
        [FromQuery(Name = "start")] string? startStr,
        [FromQuery(Name = "end")] string? endStr)
    {
        if (intervalStr.IsBlank())
            return BadRequest("Invalid interval string.");
        var interval = IntervalTypeConverter.Parse(intervalStr);
        if (interval == IntervalType.Unknown)
            return BadRequest("Invalid interval string.");
        if (startStr == null)
            return BadRequest("Missing start date-time.");
        var start = startStr.ParseDate();
        if (start == DateTime.MinValue)
            return BadRequest("Invalid start date-time.");
        
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
