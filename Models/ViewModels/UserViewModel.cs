namespace SitioSubicIMS.Web.Models.ViewModels
{
    public class UserViewModel
    {
        public string Id { get; set; }
        public string FullName
        {
            get
            {
                var middleInitial = !string.IsNullOrWhiteSpace(MiddleName)
                    ? $"{MiddleName.Trim()[0]}."
                    : string.Empty;

                return $"{LastName}, {FirstName} {middleInitial}".Replace("  ", " ").Trim();
            }
        }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }

        public string Username { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }

        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
    }

}
