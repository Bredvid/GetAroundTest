using System.IO;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;

namespace GetAroundBredvid.Function
{
    public class HttpTriggerGetAroundTest
    {

        private static readonly string SecretToken = "9f1ea4ed0ba4fcf9f6f0196439cd75e6"; //Environment.GetEnvironmentVariable("WEBHOOK_SECRET_TOKEN")
        private static readonly string BearerToken = "bb8818bb0fb7aa8b581a005bdfe684fc"; //Environment.GetEnvironmentVariable("BEARER_TOKEN")
        private readonly ILogger<HttpTriggerGetAroundTest> _logger;
        private static readonly HttpClient client = new HttpClient
            {
                BaseAddress = new Uri("https://api-eu.getaround.com/")
            };

        public HttpTriggerGetAroundTest(ILogger<HttpTriggerGetAroundTest> logger)
        {
            _logger = logger;
        }

        [Function("HttpTriggerGetAroundTest")]
        public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post",  Route = null)] HttpRequest req)
        {
            _logger.LogInformation("Function initialized.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (VerifySignature(req, requestBody) == false){
                return new StatusCodeResult(500);
            }

            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string type = data?.type;

            string rental_id = data?.data?.rental_id;

            if(type == "rental.booked" && rental_id != null){
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("X-Getaround-Version", "2023-08-08.0");

                var jsonData = "{\"content\": \"Hei, tusen takk for bestillingen! Ikke nøl med å gi tilbakemeldinger eller spørsmål om du har noen. Ønsker deg en fantastisk tur!\"}";

                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync($"owner/v1/rentals/{rental_id}/messages.json", content);

                 string responseBody = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Response: " + responseBody);

                return new OkObjectResult("Response: " + responseBody);
            }

            return new OkObjectResult("Request not type rental.booked");
        }
        public static bool VerifySignature(HttpRequest req, string payloadBody)
        {   
            if (string.IsNullOrEmpty(SecretToken))
            {
                return false; // Internal Server Error if SECRET_TOKEN is not set
            }

            using (var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(SecretToken)))
            {
                var payloadBytes = Encoding.UTF8.GetBytes(payloadBody);
                var computedSignatureBytes = hmac.ComputeHash(payloadBytes);
                var computedSignature = "sha1=" + BitConverter.ToString(computedSignatureBytes).Replace("-", "").ToLower();

                req.Headers.TryGetValue("X-Drivy-Signature", out var expectedSignature);

                if (string.IsNullOrEmpty(expectedSignature))
                {
                    return false;
                }

                if (SecureCompare(computedSignature, expectedSignature))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private static bool SecureCompare(string a, string b)
        {
            uint diff = (uint)a.Length ^ (uint)b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                diff |= (uint)a[i] ^ (uint)b[i];
            }
            return diff == 0;
        }
    }
}
