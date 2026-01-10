using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UltimateEnd.Utils
{
    public static class NetworkHelper
    {
        public static async Task<bool> IsInternetAvailableAsync(CancellationToken ct = default)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var response = await client.GetAsync("https://www.google.com/generate_204", ct);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}