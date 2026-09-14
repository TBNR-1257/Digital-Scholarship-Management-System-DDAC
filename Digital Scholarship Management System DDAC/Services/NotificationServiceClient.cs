using System.Text;
using System.Text.Json;

namespace Digital_Scholarship_Management_System_DDAC.Services;

public class NotificationServiceClient : INotificationService
{
    private readonly HttpClient _httpClient;

    public NotificationServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient; // BaseAddress = notification API's invoke URL
    }

    public async Task<bool> PublishAsync(string subject, string message)
    {
        var payload = new { subject, message };
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("publish", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}