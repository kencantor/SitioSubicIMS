using Microsoft.AspNetCore.Identity;
using System;

namespace SitioSubicIMS.Web.Models
{
    public class ApplicationUser : IdentityUser
    {
        // Custom fields
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; } // Optional
        public DateTime? Birthdate { get; set; } // Nullable to allow for empty birthdates
        public string Address { get; set; }
        public DateTime? LastModifiedDate { get; set; } // Nullable in case it hasn't been modified
        public string ModifiedBy { get; set; } // Who modified the record
        public bool IsActive { get; set; } // Active status for the user
    }
}
