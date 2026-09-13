using Microsoft.AspNetCore.Http;

namespace Digital_Scholarship_Management_System_DDAC.Services;

public interface IS3Service
{
    Task<string?> UploadFileAsync(IFormFile? file, string folderName = "uploads");
    Task<bool> DeleteFileAsync(string? fileUrl);
}