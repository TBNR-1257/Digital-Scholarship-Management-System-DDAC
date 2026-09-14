namespace Digital_Scholarship_Management_System_DDAC.Services;

public interface INotificationService
{
    Task<bool> PublishAsync(string subject, string message);
}