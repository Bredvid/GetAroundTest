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
using System.Collections.Generic;

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

        public class Message{
            public int id { get; set; }
        }

        [Function("HttpTriggerGetAroundTest")]
        public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post",  Route = null)] HttpRequest req)
        {
            _logger.LogInformation("Function initialized.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string type = data?.type;

            string rental_id = data?.data?.rental_id;

            if(type == "rental.booked" && rental_id != null){
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("X-Getaround-Version", "2023-08-08.0");

                HttpResponseMessage messagesIdsResponse = await client.GetAsync($"owner/v1/rentals/{rental_id}/messages.json");

                if(messagesIdsResponse.IsSuccessStatusCode){
                    
                    string messagesIdsResponseBody = await messagesIdsResponse.Content.ReadAsStringAsync();
                    List<Message> messages = JsonConvert.DeserializeObject<List<Message>>(messagesIdsResponseBody);
                    
                    if(messages.Count > 0){ 
                        _logger.LogInformation("Booking message has already been sent");
                        return new OkObjectResult("Booking message has already been sent");
                    }
                    
                    var jsonData = "{\"content\": \"Hei, tusen takk for bestillingen. Bare å sende meg en melding her dersom du har noen spørsmål. God tur!\"}";

                    var confirmationMessageContent = new StringContent(jsonData, Encoding.UTF8, "application/json");

                    HttpResponseMessage sendMessageResponse = await client.PostAsync($"owner/v1/rentals/{rental_id}/messages.json", confirmationMessageContent);

                    if(sendMessageResponse.IsSuccessStatusCode){
                        _logger.LogInformation("Successfully sent message");
                        return new OkObjectResult("Successfully sent message");
                    }
                    else{
                        var statusCode = sendMessageResponse.StatusCode;
                        _logger.LogInformation($"Message sending failed, with status code: {statusCode}");
                        return new OkObjectResult($"Message sending failed, with status code: {statusCode}");
                    }
                }
            }

            return new OkObjectResult("Event is either not rental.booked or/and rental id is null");
        }


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
