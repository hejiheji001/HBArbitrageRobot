using Client.Models;
using Newtonsoft.Json;

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
    /// Fetch LOF arbitrage data from the server API
    /// </summary>
    /// <param name="baseUrl">The base URL of the server API</param>
    /// <returns>A tuple containing the LOF data (if any) and a flag indicating if the client should wait</returns>
    public async Task<(List<LofArbitrageData>?, bool shouldWait)> FetchLofDataFromApiAsync(string baseUrl)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{baseUrl}/api/data");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var dataResponse = JsonConvert.DeserializeObject<LofArbitrageDataResponse>(json);
                var data = dataResponse?.Data;

                // If data is empty or null, consider it as a signal to wait
                if (data == null || data.Count == 0)
                {
                    Console.WriteLine("Server returned empty data - entering standby mode");
                    return (null, true);
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
                    return (null, true);
                }
                
                return (null, false);
            }
            else
            {
                Console.WriteLine($"API returned status code: {response.StatusCode}");
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error content: {errorContent}");
                return (null, false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch data from API: {ex.Message}");
            return (null, false);
        }
    }
}
