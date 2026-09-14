using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Digital_Scholarship_Management_System_DDAC.Services;

public class S3LambdaClientService : IS3Service
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _documentServiceClient;

    public S3LambdaClientService(HttpClient documentServiceClient)
    {
        _documentServiceClient = documentServiceClient;
    }

    public async Task<string?> UploadFileAsync(IFormFile? file, string folderName = "uploads")
    {
        if (file == null || file.Length == 0) return null;

        var presignPayload = new
        {
            fileName = file.FileName,
            folderName,
            contentType = file.ContentType
        };

        using var presignRequestBody = new StringContent(
            JsonSerializer.Serialize(presignPayload), Encoding.UTF8, "application/json");

        var presignHttpResponse = await _documentServiceClient.PostAsync("presign-upload", presignRequestBody);
        if (!presignHttpResponse.IsSuccessStatusCode) return null;

        var presignJson = await presignHttpResponse.Content.ReadAsStringAsync();
        var presignResult = JsonSerializer.Deserialize<PresignUploadResult>(presignJson, JsonOptions);
        if (presignResult == null || string.IsNullOrEmpty(presignResult.UploadUrl)) return null;

        using var fileStream = file.OpenReadStream();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);

        using var s3HttpClient = new HttpClient();
        var putResponse = await s3HttpClient.PutAsync(presignResult.UploadUrl, fileContent);

        return putResponse.IsSuccessStatusCode ? presignResult.FileUrl : null;
    }

    public async Task<bool> DeleteFileAsync(string? fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl)) return false;

        var payload = new { fileUrl };
        using var requestBody = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _documentServiceClient.PostAsync("delete", requestBody);
        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<DeleteResult>(json, JsonOptions);
        return result?.Success ?? false;
    }

    // asks the Lambda for a short-lived GET URL so a stored document can actually be viewed.
    public async Task<string?> GetViewUrlAsync(string? fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl)) return null;

        var payload = new { fileUrl };
        using var requestBody = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _documentServiceClient.PostAsync("presign-download", requestBody);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ViewUrlResult>(json, JsonOptions);
        return result?.ViewUrl;
    }

    private record PresignUploadResult(string UploadUrl, string FileUrl);
    private record DeleteResult(bool Success);
    private record ViewUrlResult(string ViewUrl);
}