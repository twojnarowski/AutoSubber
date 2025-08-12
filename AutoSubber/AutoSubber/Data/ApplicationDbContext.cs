using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AutoSubber.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        /// <summary>
        /// YouTube channel subscriptions
        /// </summary>
        public DbSet<Subscription> Subscriptions { get; set; } = null!;

        /// <summary>
        /// YouTube webhook events from PubSubHubbub
        /// </summary>
        public DbSet<WebhookEvent> WebhookEvents { get; set; } = null!;
    }
}
