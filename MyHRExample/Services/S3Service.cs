using Amazon.S3;
using Amazon.S3.Model;

namespace MyHRExample.Services
{
    public class S3Service
    {
        private readonly IAmazonS3 _s3;
        private readonly string _bucketName;

        public S3Service(IAmazonS3 s3, string bucketName) { _s3 = s3; _bucketName = bucketName; }

        // Upload
        public async Task UploadAsync(Stream stream, string key, string? contentType = null, Dictionary<string, string>? metadata = null)
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = stream,
                ContentType = contentType
            };

            if (metadata != null)
            {
                foreach (var item in metadata)
                {
                    request.Metadata.Add(item.Key, Uri.EscapeDataString(item.Value));
                }
            }

            await _s3.PutObjectAsync(request);
        }

        // Read
        public async Task<List<S3Object>> ReadAsync(string prefix = "")
        {
            var response = await _s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            });

            return response.S3Objects ?? new List<S3Object>();
        }

        // Metadata
        public async Task<Dictionary<string, string>> GetMetadataAsync(string key)
        {
            var response = await _s3.GetObjectMetadataAsync(_bucketName, key);
            var result = new Dictionary<string, string>();

            foreach (var name in response.Metadata.Keys)
            {
                result[name.Replace("x-amz-meta-", "")] = Uri.UnescapeDataString(response.Metadata[name]);
            }

            return result;
        }

        // Text
        public async Task SaveTextAsync(string key, string text)
        {
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                ContentBody = text,
                ContentType = "application/json"
            });
        }

        public async Task<string?> ReadTextAsync(string key)
        {
            try
            {
                using var response = await _s3.GetObjectAsync(_bucketName, key);
                using var reader = new StreamReader(response.ResponseStream);
                return await reader.ReadToEndAsync();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        // Download
        public async Task DownloadAsync(string key, string filePath)
        {
            using var response = await _s3.GetObjectAsync(_bucketName, key);
            await response.WriteResponseStreamToFileAsync(filePath, false, default);
        }

        // Delete
        public async Task DeleteAsync(string key)
        {
            await _s3.DeleteObjectAsync(_bucketName, key);
        }

        // Presigned URL
        public async Task<string> GetPresignedUrlAsync(string key)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(5)
            };

            return await _s3.GetPreSignedURLAsync(request);
        }
    }
}
