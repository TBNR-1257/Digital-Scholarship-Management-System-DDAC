using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace PublishNotificationFunction;

public class Function
{
    private static readonly IAmazonSimpleNotificationService SnsClient = new AmazonSimpleNotificationServiceClient();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string TopicArn =>
        Environment.GetEnvironmentVariable("SNS_TOPIC_ARN")
        ?? throw new InvalidOperationException("SNS_TOPIC_ARN environment variable is not set.");

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            var body = JsonSerializer.Deserialize<PublishInput>(request.Body ?? "{}", JsonOptions);

            if (body == null || string.IsNullOrWhiteSpace(body.Subject) || string.IsNullOrWhiteSpace(body.Message))
                return Respond(400, new { error = "subject and message are required." });

            var result = await SnsClient.PublishAsync(new PublishRequest
            {
                TopicArn = TopicArn,
                Subject = body.Subject,
                Message = body.Message
            });

            context.Logger.LogInformation($"Published. MessageId={result.MessageId}");
            return Respond(200, new { messageId = result.MessageId });
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            return Respond(500, new { error = ex.Message });
        }
    }

    private static APIGatewayProxyResponse Respond(int statusCode, object body) => new()
    {
        StatusCode = statusCode,
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Body = JsonSerializer.Serialize(body)
    };

    private record PublishInput(string Subject, string Message);
}