using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SitioSubicIMS.Web.Models;

namespace SitioSubicIMS.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Add your DbSets here (e.g., public DbSet<Consumer> Consumers { get; set; })
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Configuration> Configurations { get; set; }
        public DbSet<Meter> Meters { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Reading> Readings { get; set; }
        public DbSet<SMSAlert> SMSAlerts { get; set; }
        public DbSet<SMSLog> SMSLogs { get; set; }
        public DbSet<Billing> Billings { get; set; }
    }
}
