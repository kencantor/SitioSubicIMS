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

        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Configuration> Configurations { get; set; }
        public DbSet<Meter> Meters { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Reading> Readings { get; set; }
        public DbSet<SMSAlert> SMSAlerts { get; set; }
        public DbSet<SMSLog> SMSLogs { get; set; }
        public DbSet<Billing> Billings { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Billing-Payment one-to-many relationship
            modelBuilder.Entity<Billing>()
                .HasMany(b => b.Payments)
                .WithOne(p => p.Billing)
                .HasForeignKey(p => p.BillingID)
                .OnDelete(DeleteBehavior.Cascade); // Optional: use .Restrict or .SetNull if you prefer
        }
    }
}
