using SitioSubicIMS.Web.Data;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Services.Logging;
using System.Threading.Tasks;

public class AuditLogger : IAuditLogger
{
    private readonly ApplicationDbContext _context;

    public AuditLogger(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string actionType, string description, string performedBy)
    {
        var log = new AuditLog
        {
            ActionType = actionType,
            Description = description,
            PerformedBy = performedBy
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}