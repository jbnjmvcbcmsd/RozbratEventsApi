using Microsoft.EntityFrameworkCore;
using RozbratEventsApi.Models;
namespace RozbratEventsApi
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Event> Events { get; set; }
    }
}
