using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LambdaDocumentService;

public class Function
{
    private static readonly IAmazonS3 S3Client = new AmazonS3Client();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string BucketName =>
        Environment.GetEnvironmentVariable("S3_BUCKET_NAME")
        ?? throw new InvalidOperationException("S3_BUCKET_NAME environment variable is not set.");

    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            string method = request.HttpMethod ?? "GET";
            string path = request.PathParameters != null && request.PathParameters.TryGetValue("proxy", out var p)
                ? "/" + p
                : request.Path ?? "/";

            context.Logger.LogInformation($"{method} {path}");

            return (method, path) switch
            {
                ("GET", "/health") => Ok(new { status = "ok", service = "ScholarshipDocumentService" }),
                ("POST", "/presign-upload") => HandlePresignUpload(request),
                ("POST", "/delete") => await HandleDelete(request),
                _ => NotFound()
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            return Error(ex.Message);
        }
    }

    private APIGatewayProxyResponse HandlePresignUpload(APIGatewayProxyRequest request)
    {
        var body = JsonSerializer.Deserialize<PresignUploadRequest>(request.Body ?? "{}", JsonOptions);
        if (body == null || string.IsNullOrWhiteSpace(body.FileName))
            return BadRequest("fileName is required.");

        string folder = string.IsNullOrWhiteSpace(body.FolderName) ? "uploads" : body.FolderName;
        string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(body.FileName)}";
        string objectKey = $"{folder}/{uniqueFileName}";

        var presignRequest = new GetPreSignedUrlRequest
        {
            BucketName = BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(10),
            ContentType = string.IsNullOrWhiteSpace(body.ContentType) ? "application/octet-stream" : body.ContentType
        };

        string uploadUrl = S3Client.GetPreSignedURL(presignRequest);
        string region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
        string fileUrl = $"https://{BucketName}.s3.{region}.amazonaws.com/{objectKey}";

        return Ok(new { uploadUrl, fileUrl });
    }

    private async Task<APIGatewayProxyResponse> HandleDelete(APIGatewayProxyRequest request)
    {
        var body = JsonSerializer.Deserialize<DeleteRequest>(request.Body ?? "{}", JsonOptions);
        if (body == null || string.IsNullOrWhiteSpace(body.FileUrl))
            return BadRequest("fileUrl is required.");

        try
        {
            var uri = new Uri(body.FileUrl);
            string objectKey = uri.AbsolutePath.TrimStart('/');

            var response = await S3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = BucketName,
                Key = objectKey
            });

            bool success = response.HttpStatusCode == HttpStatusCode.NoContent
                || response.HttpStatusCode == HttpStatusCode.OK;

            return Ok(new { success });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    private static APIGatewayProxyResponse Ok(object body) => new()
    {
        StatusCode = 200,
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Body = JsonSerializer.Serialize(body)
    };

    private static APIGatewayProxyResponse BadRequest(string message) => new()
    {
        StatusCode = 400,
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Body = JsonSerializer.Serialize(new { error = message })
    };

    private static APIGatewayProxyResponse NotFound() => new()
    {
        StatusCode = 404,
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Body = JsonSerializer.Serialize(new { error = "not found" })
    };

    private static APIGatewayProxyResponse Error(string message) => new()
    {
        StatusCode = 500,
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Body = JsonSerializer.Serialize(new { error = message })
    };

    private record PresignUploadRequest(string FileName, string? FolderName, string? ContentType);
    private record DeleteRequest(string FileUrl);
}