using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using SitioSubicIMS.Web.Models;
using SitioSubicIMS.Web.Data;

public interface ISmsService
{
    Task<bool> SendSmsAsync(string phoneNumber, string message, string createdBy);
}

public class SmsService : ISmsService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly HttpClient _httpClient;

    public SmsService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
        _httpClient = new HttpClient();
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
            string formattedNumber = NormalizePhoneNumber(phoneNumber);
            string formattedMessage = $"{config.MessageHeader}: " + message;

            var postData = new Dictionary<string, string>
            {
                { "api_key", config.APIKey },   // Vonage API Key
                { "api_secret", config.Token }, // Vonage API Secret
                { "to", formattedNumber },
                { "from", "Vonage" },                     // Or your Vonage virtual number
                { "text", formattedMessage }
            };

            var content = new FormUrlEncodedContent(postData);
            var response = await _httpClient.PostAsync("https://rest.nexmo.com/sms/json", content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && responseBody.Contains("\"status\":\"0\""))
            {
                _dbContext.SMSLogs.Add(new SMSLog
                {
                    PhoneNumber = phoneNumber,
                    Message = message,
                    Status = "Success",
                    ErrorMessage = "",
                    DateSent = DateTime.UtcNow,
                    CreatedBy = createdBy
                });

                await _dbContext.SaveChangesAsync();
                return true;
            }
            else
            {
                _dbContext.SMSLogs.Add(new SMSLog
                {
                    PhoneNumber = phoneNumber,
                    Message = message,
                    Status = "Failed",
                    ErrorMessage = responseBody,
                    DateSent = DateTime.UtcNow,
                    CreatedBy = createdBy
                });

                await _dbContext.SaveChangesAsync();
                return false;
            }
        }
        catch (Exception ex)
        {
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
            return "63" + phoneNumber.Substring(1); // Vonage expects no '+'

        if (phoneNumber.StartsWith("+639") && phoneNumber.Length == 13)
            return phoneNumber.Substring(1); // remove '+' sign

        if (phoneNumber.StartsWith("639") && phoneNumber.Length == 12)
            return phoneNumber;

        return phoneNumber;
    }
}
