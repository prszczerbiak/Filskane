using System.Net.Http.Headers;
using System.Text.Json;

namespace WebApplication1.Services
{
    public class SentinelHubService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public SentinelHubService(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
        }

        public async Task<byte[]> GetNDVIAsync(double[] bbox, string start, string end)
        {
            string token = await GetAccessTokenAsync();

            var evalscript = @"
                //VERSION=3
                function setup() {
                  return { input: ['B04','B03','B02'], output: { bands: 3, sampleType: ""FLOAT32"" }};
                }
                function evaluatePixel(sample) {
                  return [sample.B04, sample.B03, sample.B02];
                }";

            var requestObject = new
            {
                input = new
                {
                    bounds = new
                    {
                        bbox = bbox,
                        properties = new { crs = "http://www.opengis.net/def/crs/EPSG/0/4326" }
                    },
                    data = new[]
{
                new
                {
                    type = "sentinel-2-l2a",
                    dataFilter = new
                    {
                        timeRange = new
                        {
                            from = $"{start}T00:00:00Z",
                            to = $"{end}T23:59:59Z"
                        }
                    }
                }
            }
                },
                output = new
                {
                    width = 256,
                    height = 256,
                    responses = new[]
{
                new
                {
                    identifier = "default",
                    format = new { type = "image/tiff" }
                }
            }
                },
                evalscript
            };

            string body = JsonSerializer.Serialize(requestObject);

            var req = new HttpRequestMessage(HttpMethod.Post, "https://services.sentinel-hub.com/api/v1/process");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(req);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync();
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var clientId = _config["SentinelHub:ClientId"];
            var clientSecret = _config["SentinelHub:ClientSecret"];

            var content = new FormUrlEncodedContent(new Dictionary<string, string> {
                {"grant_type", "client_credentials"},
                {"client_id", clientId!},
                {"client_secret", clientSecret!}
            });

            var response = await _httpClient.PostAsync("https://services.sentinel-hub.com/oauth/token", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("access_token").GetString()!;
        }
    }
}
