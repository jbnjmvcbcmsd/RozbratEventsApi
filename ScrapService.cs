using HtmlAgilityPack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

using RozbratEventsApi.Models;
namespace RozbratEventsApi
{
    public class ScrapService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _url = "https://www.rozbrat.org/wydarzenia";
        public ScrapService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await ScrapeAndSave();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);

                    await ScrapeAndSave();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Scraper error: {ex.Message}");
                }
            }
        }
        public async Task<List<Event>> Scrape()
        {
            using var client = new HttpClient();

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("limit", "0"),
                new KeyValuePair<string, string>("limitstart", "0")
            });

            var response = await client.PostAsync(_url, content);
            var html = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var rows = doc.DocumentNode
                .SelectNodes("//table[contains(@class,'eventlist')]//tbody/tr")
                ?.ToList() ?? new List<HtmlNode>();

            var list = new List<Event>();

            foreach (var row in rows)
            {
                var cols = row.SelectNodes("./td");
                if (cols == null || cols.Count < 4)
                    continue;

                var dateText = cols[1].InnerText.Trim();
                var timeText = cols[2].InnerText.Trim();

                var cleanDate = dateText.Split(',')[1].Trim();

                var culture = new System.Globalization.CultureInfo("pl-PL");

                var date = DateTime.Parse(cleanDate, culture);
                var time = TimeSpan.Parse(timeText);

                var localDateTime = date.Add(time);

                var startDateTime = TimeZoneInfo.ConvertTimeToUtc(
                    localDateTime,
                    TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time")
                );

                list.Add(new Event
                {
                    Name = cols[0].InnerText.Trim(),
                    Date = startDateTime,
                    Location = cols[3].InnerText.Trim()
                });
            }

            return list;
        }
        private async Task ScrapeAndSave()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            List<Event> scraped;

            try
            {
                scraped = await Scrape();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Scrape failed: {ex.Message}");
                return;
            }

            if (scraped == null || scraped.Count == 0)
                return;

            var existing = await db.Events.ToListAsync();

            var existingMap = existing.ToDictionary(x =>
                $"{x.Name}|{x.Date}|{x.Location}");

            foreach (var ev in scraped)
            {
                var key = $"{ev.Name}|{ev.Date}|{ev.Location}";

                if (!existingMap.ContainsKey(key))
                {
                    db.Events.Add(ev);
                }
            }
            var now = DateTime.UtcNow.AddDays(-7);
            var oldEvents = db.Events
                .Where(e => e.Date < now);

            db.Events.RemoveRange(oldEvents);

            await db.SaveChangesAsync();
        }
    }
}
