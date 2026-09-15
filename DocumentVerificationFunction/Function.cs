using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Model;
using MySqlConnector;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace DocumentVerificationFunction;

public class Function
{
    private static readonly IAmazonS3 S3Client = new AmazonS3Client();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string BucketName =>
        Environment.GetEnvironmentVariable("S3_BUCKET_NAME")
        ?? throw new InvalidOperationException("S3_BUCKET_NAME environment variable is not set.");

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
        ?? throw new InvalidOperationException("DB_CONNECTION_STRING environment variable is not set.");

    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        foreach (var record in sqsEvent.Records)
        {
            try
            {
                await ProcessMessageAsync(record.Body, context);
            }
            catch (Exception ex)
            {
                // Rethrowing tells SQS this message failed, so it gets retried automatically
                // instead of silently disappearing.
                context.Logger.LogError($"Failed processing message {record.MessageId}: {ex.Message}");
                throw;
            }
        }
    }

    private async Task ProcessMessageAsync(string body, ILambdaContext context)
    {
        var message = JsonSerializer.Deserialize<VerificationMessage>(body, JsonOptions);
        if (message == null)
        {
            context.Logger.LogWarning("Could not parse message body, skipping.");
            return;
        }

        context.Logger.LogInformation($"Verifying document {message.DocumentId} ({message.DocumentType})");

        string status = await CheckDocumentAsync(message.FileUrl);
        await UpdateVerificationStatusAsync(message.DocumentId, status);

        context.Logger.LogInformation($"Document {message.DocumentId} marked as {status}");
    }

    private async Task<string> CheckDocumentAsync(string fileUrl)
    {
        try
        {
            var uri = new Uri(fileUrl);
            string objectKey = uri.AbsolutePath.TrimStart('/');

            var metadata = await S3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = BucketName,
                Key = objectKey
            });

            bool looksValid = metadata.ContentLength > 0 &&
                (metadata.Headers.ContentType?.Contains("pdf") == true ||
                 metadata.Headers.ContentType?.Contains("image") == true);

            return looksValid ? "Verified" : "Flagged";
        }
        catch
        {
            return "Flagged";
        }
    }

    private async Task UpdateVerificationStatusAsync(int documentId, string status)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Documents SET VerificationStatus = @status WHERE DocumentId = @id";
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@id", documentId);

        await command.ExecuteNonQueryAsync();
    }

    private record VerificationMessage(int DocumentId, string FileUrl, string DocumentType);
}