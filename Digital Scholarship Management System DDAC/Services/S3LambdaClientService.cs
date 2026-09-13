using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Digital_Scholarship_Management_System_DDAC.Services;

// Implements the same IS3Service contract as the old S3Service, but delegates the actual
// S3 permission logic to the ScholarshipDocumentService Lambda (via API Gateway)
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

        // 1. Ask the Document microservice for a presigned S3 upload URL.
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

        // 2. Upload the actual bytes straight to S3 using the presigned URL (does not go through Lambda).
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

    private record PresignUploadResult(string UploadUrl, string FileUrl);
    private record DeleteResult(bool Success);
}