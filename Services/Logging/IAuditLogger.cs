namespace SitioSubicIMS.Web.Services.Logging
{
    public interface IAuditLogger
    {
        Task LogAsync(string actionType, string description, string performedBy);
    }

}
