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

            // COde that sends a message to the client who rented a car
            // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
            // client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // client.DefaultRequestHeaders.Add("X-Getaround-Version", "2023-08-08.0");

            // var jsonData = "{\"content\": \"Hei, tusen takk for bestillingen! Ikke nøl med å gi tilbakemeldinger eller spørsmål om du har noen. Ønsker deg en fantastisk tur!\"}";

            // var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

            // HttpResponseMessage response = await client.PostAsync("owner/v1/rentals/8305555/messages.json", content);

            //  string responseBody = await response.Content.ReadAsStringAsync();

            // _logger.LogInformation("Response: " + responseBody);

            return new OkObjectResult("Request done!:)");
        }

        // string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        // string signature = req.Headers["X-Drivy-Signature"];

        // _logger.LogInformation("x-Drivy-Signature: " + signature);
        // _logger.LogInformation("requestBody: " + requestBody);

        // if (!VerifySignature(requestBody, signature))
        // {
        //     _logger.LogWarning("Signature mismatch. Possible tampering detected.");
        //     return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        // }

        // var payload = JObject.Parse(requestBody);

        // _logger.LogInformation($"Payload received: {payload.ToString()}");




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
