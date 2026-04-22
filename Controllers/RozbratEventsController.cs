using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RozbratEventsApi.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RozbratEventsApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RozbratEventsController : ControllerBase
    {
        
        private readonly AppDbContext _db;
        private readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

        public RozbratEventsController(AppDbContext db)
        {
            _db = db;
        }

        // GET: RozbratEvents
        [HttpGet]
        public async Task<ActionResult<List<Event>>> GetAll()
        {
            var events = await _db.Events
                .OrderBy(e => e.Date)
                .ToListAsync();
            events.ForEach(e => e.Date = TimeZoneInfo.ConvertTimeFromUtc(e.Date, _tz));

            return Ok(events);
        }

        // GET: RozbratEvents/search?name=
        [HttpGet("search")]
        public async Task<ActionResult<List<Event>>> SearchByName([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return await GetAll();
            }
            if (name.Length > 100)
            {
                return BadRequest("Query too long");
            }
            name = name.Replace("%", "").Replace("_", "");

            var events = await _db.Events
                .Where(e => EF.Functions.ILike(e.Name, $"%{name}%"))
                .OrderBy(e => e.Date)
                .ToListAsync();
            events.ForEach(e => e.Date = TimeZoneInfo.ConvertTimeFromUtc(e.Date, _tz));

            return Ok(events);
        }
    }
}
