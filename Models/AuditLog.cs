using System;
using System.ComponentModel.DataAnnotations;

namespace SitioSubicIMS.Web.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ActionType { get; set; } // e.g., Create, Update, Delete, Login

        public string Description { get; set; } // Summary or explanation of what happened

        public string PerformedBy { get; set; } // Username or User ID

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
