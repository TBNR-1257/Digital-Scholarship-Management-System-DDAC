using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Digital_Scholarship_Management_System_DDAC.Services
{
    public class S3Service : IS3Service
    {
        private readonly IConfiguration _config;
        private readonly string _bucketName;
        private readonly string _region;

        public S3Service(IConfiguration config)
        {
            _config = config;
            _bucketName = _config["AWS:BucketName"] ?? throw new ArgumentNullException("AWS BucketName not configured");
            _region = _config["AWS:Region"] ?? "us-east-1";
        }

        private IAmazonS3 GetS3Client()
        {
            var accessKey = _config["AWS:AccessKey"];
            var secretKey = _config["AWS:SecretKey"];
            var sessionToken = _config["AWS:SessionToken"];
            var regionEndpoint = RegionEndpoint.GetBySystemName(_region);

            if (!string.IsNullOrEmpty(sessionToken))
            {
                return new AmazonS3Client(accessKey, secretKey, sessionToken, regionEndpoint);
            }

            if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
            {
                return new AmazonS3Client(accessKey, secretKey, regionEndpoint);
            }

            return new AmazonS3Client(regionEndpoint);
        }

        public async Task<string?> UploadFileAsync(IFormFile? file, string folderName = "uploads")
        {
            if (file == null || file.Length == 0) return null;

            using var client = GetS3Client();
            string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            string objectKey = $"{folderName}/{uniqueFileName}";

            using (var stream = file.OpenReadStream())
            {
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey,
                    InputStream = stream,
                    ContentType = file.ContentType
                };

                await client.PutObjectAsync(putRequest);
            }

            return $"https://{_bucketName}.s3.{_region}.amazonaws.com/{objectKey}";
        }

        public async Task<bool> DeleteFileAsync(string? fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl)) return false;

            try
            {
                Uri uri = new Uri(fileUrl);
                string objectKey = uri.AbsolutePath.TrimStart('/');

                using var client = GetS3Client();
                var response = await client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey
                });

                return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent ||
                       response.HttpStatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3 Delete Warning]: Could not delete file: {ex.Message}");
                return false;
            }
        }

        // NEW: for parity with S3LambdaClientService, in case you ever flip the feature flag back.
        public async Task<string?> GetViewUrlAsync(string? fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl)) return null;

            try
            {
                Uri uri = new Uri(fileUrl);
                string objectKey = uri.AbsolutePath.TrimStart('/');

                using var client = GetS3Client();
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey,
                    Verb = HttpVerb.GET,
                    Expires = DateTime.UtcNow.AddMinutes(15)
                };

                return await Task.FromResult(client.GetPreSignedURL(request));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3 GetViewUrl Warning]: {ex.Message}");
                return null;
            }
        }
    }
}