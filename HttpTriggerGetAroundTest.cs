using System.IO;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Functions.Worker;

namespace GetAroundBredvid.Function
{
    public class HttpTriggerGetAroundTest
    {

        private static readonly string SecretToken = Environment.GetEnvironmentVariable("WEBHOOK_SECRET_TOKEN");
        private static readonly string BearerToken = "bb8818bb0fb7aa8b581a005bdfe684fc";
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

            // string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            // string signature = req.Headers["X-Drivy-Signature"];

            // _logger.LogInformation("x-Drivy-Signature: " + signature);
            // _logger.LogInformation("requestBody: " + requestBody);

            // HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
            // client.DefaultRequestHeaders.Add("Authorization", "Bearer bb8818bb0fb7aa8b581a005bdfe684fc");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Getaround-Version", "2023-08-08.0");
            // client.DefaultRequestHeaders.Add("X-Car-by-Name", "true");

            HttpResponseMessage response = client.GetAsync("owner/v1/rentals/8338525.json").Result;

             string responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Response: " + responseBody);

            // if (!VerifySignature(requestBody, signature))
            // {
            //     _logger.LogWarning("Signature mismatch. Possible tampering detected.");
            //     return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            // }

            // var payload = JObject.Parse(requestBody);

            // _logger.LogInformation($"Payload received: {payload.ToString()}");



            return new OkObjectResult("Request done!:)" + responseBody);
        }






        // private static bool VerifySignature(string payloadBody, string signature)
        // {
        //     using (var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(SecretToken)))
        //     {
        //         var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signature));
        //         var hashString = "sha1=" + BitConverter.ToString(hash).Replace("-", "").ToLower();

        //         return SlowEquals(hashString, signature);
        //     }
        // }

        // private static bool SlowEquals(string a, string b)
        // {
        //     uint diff = (uint)a.Length ^ (uint)b.Length;
        //     for (int i = 0; i < a.Length && i < b.Length; i++)
        //     {
        //         diff |= (uint)(a[i] ^ b[i]);
        //     }
        //     return diff == 0;
        // }

    }
}
