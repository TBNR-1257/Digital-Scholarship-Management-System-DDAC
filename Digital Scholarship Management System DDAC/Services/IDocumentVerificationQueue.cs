namespace Digital_Scholarship_Management_System_DDAC.Services;

public interface IDocumentVerificationQueue
{
    Task SendForVerificationAsync(int documentId, string fileUrl, string documentType);
}