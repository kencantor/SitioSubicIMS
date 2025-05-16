using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Microsoft.EntityFrameworkCore;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Data;

public interface ISmsService
{
    Task<bool> SendSmsAsync(string phoneNumber, string message, string createdBy);
}

public class SmsService : ISmsService
{
    private readonly ApplicationDbContext _dbContext;

    public SmsService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> SendSmsAsync(string phoneNumber, string message, string createdBy)
    {
        var config = await _dbContext.SMSAlerts
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.DateCreated)
            .FirstOrDefaultAsync();

        if (config == null)
            throw new InvalidOperationException("No active SMS configuration found.");

        try
        {
            TwilioClient.Init(config.TwilioAccountSID, config.TwilioAuthToken);
            string formattedNumber = NormalizePhoneNumber(phoneNumber);
            var msg = await MessageResource.CreateAsync(
                to: new PhoneNumber(formattedNumber),
                from: new PhoneNumber(config.TwilioFromPhoneNumber),
                //body: $"{config.MessageHeader} {message}"
                body: $"{message}"
            );

            // Log success
            _dbContext.SMSLogs.Add(new SMSLog
            {
                PhoneNumber = phoneNumber,
                Message = message,
                Status = "Success",
                ErrorMessage = null,
                DateSent = DateTime.UtcNow,
                CreatedBy = createdBy
            });

            await _dbContext.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            // Log failure
            _dbContext.SMSLogs.Add(new SMSLog
            {
                PhoneNumber = phoneNumber,
                Message = message,
                Status = "Failed",
                ErrorMessage = ex.Message,
                DateSent = DateTime.UtcNow,
                CreatedBy = createdBy
            });

            await _dbContext.SaveChangesAsync();

            return false;
        }
    }
    private static string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return phoneNumber;

        phoneNumber = phoneNumber.Trim();

        if (phoneNumber.StartsWith("09") && phoneNumber.Length == 11)
        {
            // Replace leading 0 with +63
            return "+63" + phoneNumber.Substring(1);
        }
        else if (phoneNumber.StartsWith("639") && phoneNumber.Length == 12)
        {
            // Add plus sign if missing
            return "+" + phoneNumber;
        }
        else if (phoneNumber.StartsWith("+639") && phoneNumber.Length == 13)
        {
            // Already normalized
            return phoneNumber;
        }

        // If none of the above, return as-is or handle differently if needed
        return phoneNumber;
    }

}
