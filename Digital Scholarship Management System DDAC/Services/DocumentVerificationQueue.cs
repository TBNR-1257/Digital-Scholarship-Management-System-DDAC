using Amazon.S3;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Text.Json;

namespace Digital_Scholarship_Management_System_DDAC.Services;

public class DocumentVerificationQueue : IDocumentVerificationQueue
{
    private readonly IAmazonSQS _sqsClient;
    private readonly string _queueUrl;

    public DocumentVerificationQueue(IAmazonSQS sqsClient, IConfiguration config)
    {
        _sqsClient = sqsClient;
        _queueUrl = config["Sqs:DocumentVerificationQueueUrl"]
            ?? throw new InvalidOperationException("Sqs:DocumentVerificationQueueUrl not configured");
    }

    public async Task SendForVerificationAsync(int documentId, string fileUrl, string documentType)
    {
        var payload = new { documentId, fileUrl, documentType };

        await _sqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = JsonSerializer.Serialize(payload)
        });
    }
}