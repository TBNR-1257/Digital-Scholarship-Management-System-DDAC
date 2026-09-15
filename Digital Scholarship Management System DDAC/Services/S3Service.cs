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
        private readonly IAmazonS3 _s3Client;
        private readonly string? _bucketName;
        private readonly string _region;

        public S3Service(IAmazonS3 s3Client, IConfiguration config)
        {
            _s3Client = s3Client;
            _bucketName = config["AWS:BucketName"];
            _region = config["AWS:Region"] ?? "us-east-1";
        }

        public async Task<string?> UploadFileAsync(IFormFile? file, string folderName = "uploads")
        {
            if (file == null || file.Length == 0) return null;

            if (string.IsNullOrEmpty(_bucketName))
            {
                throw new InvalidOperationException("AWS S3 BucketName is not configured.");
            }

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

                await _s3Client.PutObjectAsync(putRequest);
            }

            return $"https://{_bucketName}.s3.{_region}.amazonaws.com/{objectKey}";
        }

        public async Task<bool> DeleteFileAsync(string? fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl) || string.IsNullOrEmpty(_bucketName)) return false;

            try
            {
                Uri uri = new Uri(fileUrl);
                string objectKey = uri.AbsolutePath.TrimStart('/');

                var response = await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
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

        public Task<string?> GetViewUrlAsync(string? fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl) || string.IsNullOrEmpty(_bucketName))
                return Task.FromResult<string?>(null);

            try
            {
                Uri uri = new Uri(fileUrl);
                string objectKey = uri.AbsolutePath.TrimStart('/');

                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = objectKey,
                    Expires = DateTime.UtcNow.AddMinutes(15),
                    Verb = HttpVerb.GET
                };

                string url = _s3Client.GetPreSignedURL(request);
                return Task.FromResult<string?>(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3 GetViewUrl Warning]: {ex.Message}");
                return Task.FromResult<string?>(null);
            }
        }
    }
}