using Client.Models;
using Newtonsoft.Json;
using Amazon.Runtime;
using System.Text;

namespace Client.Services;

/// <summary>
/// Service for communicating with the Server API
/// </summary>
public class ApiClientService
{
    // Use static HttpClient for proper socket management and performance
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>
    /// Checks if the URL is a localhost URL (development environment)
    /// </summary>
    private static bool IsLocalhostUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;
            
        var uri = new Uri(url);
        return uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1";
    }

    /// <summary>
    /// Signs an HTTP request with AWS Sigv4 for IAM authentication
    /// </summary>
    private static async Task<HttpRequestMessage> SignRequestWithIAM(HttpRequestMessage request, string region)
    {
        try
        {
            // Get AWS credentials from environment or default credential chain
            #pragma warning disable CS0618 // FallbackCredentialsFactory is deprecated but still functional
            var credentials = FallbackCredentialsFactory.GetCredentials();
            #pragma warning restore CS0618
            var immutableCredentials = credentials.GetCredentials();
            
            // Prepare request for signing
            var requestUri = request.RequestUri!;
            var httpMethod = request.Method.Method;
            
            // Get content for signing if present
            string contentHash;
            if (request.Content != null)
            {
                var content = await request.Content.ReadAsByteArrayAsync();
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha256.ComputeHash(content);
                contentHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            else
            {
                // Empty payload hash
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = sha256.ComputeHash(Array.Empty<byte>());
                contentHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
            
            // Add date header
            var dateTime = DateTime.UtcNow;
            var dateStamp = dateTime.ToString("yyyyMMdd");
            var amzDate = dateTime.ToString("yyyyMMdd'T'HHmmss'Z'");
            
            // Build canonical headers
            var headers = new SortedDictionary<string, string>
            {
                ["host"] = requestUri.Host,
                ["x-amz-content-sha256"] = contentHash,
                ["x-amz-date"] = amzDate
            };
            
            // Add security token if present
            if (!string.IsNullOrEmpty(immutableCredentials.Token))
            {
                headers["x-amz-security-token"] = immutableCredentials.Token;
            }
            
            var canonicalHeaders = string.Join("\n", headers.Select(h => $"{h.Key}:{h.Value.Trim()}")) + "\n";
            var signedHeaders = string.Join(";", headers.Keys);
            
            // Build canonical request
            var canonicalUri = requestUri.AbsolutePath;
            var canonicalQueryString = requestUri.Query.TrimStart('?');
            var canonicalRequest = $"{httpMethod}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{contentHash}";
            
            // Hash canonical request
            using var sha256Canon = System.Security.Cryptography.SHA256.Create();
            var canonicalRequestHash = BitConverter.ToString(sha256Canon.ComputeHash(Encoding.UTF8.GetBytes(canonicalRequest))).Replace("-", "").ToLowerInvariant();
            
            // Build string to sign
            var credentialScope = $"{dateStamp}/{region}/lambda/aws4_request";
            var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{canonicalRequestHash}";
            
            // Calculate signature
            var kSecret = Encoding.UTF8.GetBytes($"AWS4{immutableCredentials.SecretKey}");
            var kDate = HmacSha256(kSecret, dateStamp);
            var kRegion = HmacSha256(kDate, region);
            var kService = HmacSha256(kRegion, "lambda");
            var kSigning = HmacSha256(kService, "aws4_request");
            var signature = BitConverter.ToString(HmacSha256(kSigning, stringToSign)).Replace("-", "").ToLowerInvariant();
            
            // Build authorization header
            var authorizationHeader = $"AWS4-HMAC-SHA256 Credential={immutableCredentials.AccessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
            
            // Add authentication headers to request (don't clear existing headers)
            request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
            request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
            request.Headers.TryAddWithoutValidation("x-amz-content-sha256", contentHash);
            if (!string.IsNullOrEmpty(immutableCredentials.Token))
            {
                request.Headers.TryAddWithoutValidation("x-amz-security-token", immutableCredentials.Token);
            }
            
            return request;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to sign request with IAM: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// HMAC-SHA256 helper function
    /// </summary>
    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    /// <summary>
    /// Fetch LOF arbitrage data from the server API
    /// </summary>
    /// <param name="baseUrl">The base URL of the server API</param>
    /// <returns>A tuple containing the LOF data (if any) and a flag indicating if the client should wait</returns>
    public static async Task<(List<LofArbitrageData>, bool shouldWait)> FetchLofDataFromApiAsync(string baseUrl, string? apiToken = null)
    {
        try
        {
            var url = $"{baseUrl}/api/data";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(apiToken))
            {
                request.Headers.TryAddWithoutValidation("X-Api-Token", apiToken);
            }
            
            // If not localhost, sign the request with AWS IAM
            if (!IsLocalhostUrl(baseUrl))
            {
                // Extract region from AWS Lambda URL if present
                // Format: https://xxx.lambda-url.{region}.on.aws/
                // Default region can be overridden via AWS_REGION environment variable
                var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-northeast-1";
                var uri = new Uri(baseUrl);
                if (uri.Host.Contains("lambda-url"))
                {
                    var parts = uri.Host.Split('.');
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (parts[i] == "lambda-url" && i + 1 < parts.Length)
                        {
                            region = parts[i + 1];
                            break;
                        }
                    }
                }
                
                Console.WriteLine($"Using IAM authentication for AWS Lambda URL (region: {region})");
                request = await SignRequestWithIAM(request, region);
            }
            
            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var dataResponse = JsonConvert.DeserializeObject<LofArbitrageDataResponse>(json);
                var data = dataResponse?.Data;

                // If data is empty or null, consider it as a signal to wait
                if (data == null || data.Count == 0)
                {
                    Console.WriteLine("Server returned empty data - entering standby mode");
                    return ([], true);
                }
                
                return (data, false);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // 404 with "Data fetch in progress" message
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Server response: {errorContent}");
                
                // Check if it's the "fetch in progress" message
                if (errorContent.Contains("Data fetch in progress", StringComparison.OrdinalIgnoreCase))
                {
                    return ([], true);
                }
                
                return ([], false);
            }
            else
            {
                Console.WriteLine($"API returned status code: {response.StatusCode}");
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error content: {errorContent}");
                return ([], false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch data from API: {ex.Message}");
            return ([], false);
        }
    }
}
