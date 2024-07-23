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

        private static readonly HttpClient httpSanityClient = new HttpClient();
        private static readonly string projectId = "euo3cwv8";
        private static readonly string dataset = "production";
        private static readonly string token = "skFOpC9C07IsMGXRcq7hzvhywAHVZEx3mgA5h5UbVqRvEacgbZbXGl2FGxNk5snXKWL26GDTIWi1528JhTSnSOabO1fCeGeE1ysy6YCO1BDgQ5H0cmy454NdJHlZBeXv6f22u9v6CLdMZWyjdwwY1SFmjxYb932ngRZA09QgN21GWqcWF93n";

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

            if(rental_id != null){
                JToken carId = await GetCarIdAsync(rental_id);
                if(type == "rental.booked") return await BookingMessageAsync(_logger, rental_id, carId.ToString());
                else if(type == "rental.car_checked_in") return await CheckInMessageAsync(_logger, rental_id, carId.ToString());
                else if(type == "rental.car_checked_out") return await CheckoutMessageAsync(_logger, rental_id, carId.ToString());
            }

            return new OkObjectResult("Event is either not rental.booked,  or/and rental id is null");
        }

        private static async Task<JToken> GetCarIdAsync(string rental_id){
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Getaround-Version", "2023-08-08.0");

            HttpResponseMessage rentalPropsResponse = await client.GetAsync($"owner/v1/rentals/{rental_id}.json");
            if(rentalPropsResponse.IsSuccessStatusCode){ 
                string responseBody = await rentalPropsResponse.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<JObject>(responseBody);
                var carId = data["car_id"];
                return carId == null ? null : carId;
                }
                return null;
        }

        private static async Task<OkObjectResult> BookingMessageAsync( ILogger<HttpTriggerGetAroundTest> _logger, string rental_id, string carId){
            var data = await SanityMessage(_logger, carId, "bookingMessage");
            var bookingMessage = data["result"]["bookingMessage"];

            HttpResponseMessage messagesIdsResponse = await client.GetAsync($"owner/v1/rentals/{rental_id}/messages.json");

            if(messagesIdsResponse.IsSuccessStatusCode){
                
                string messagesIdsResponseBody = await messagesIdsResponse.Content.ReadAsStringAsync();
                List<Message> messages = JsonConvert.DeserializeObject<List<Message>>(messagesIdsResponseBody);
                        
                // if(messages.Count > 0){ 
                //     _logger.LogInformation("Booking message has already been sent");
                //     return new OkObjectResult("Booking message has already been sent");
                // }
                string messageContent = JsonConvert.SerializeObject(new { content = bookingMessage["bookingMessageContent"]?.ToString() });
                
                return await SendGetAroundMessage(_logger, messageContent, rental_id);
            }
            return new OkObjectResult($"messagesIdsResponse.IsSuccessStatusCode is false");
        }

        private static async Task<OkObjectResult> CheckInMessageAsync( ILogger<HttpTriggerGetAroundTest> _logger, string rental_id, string carId){
            var data = await SanityMessage(_logger, carId, "checkInMessage"); 
            var checkInMessage = data["result"]["checkInMessage"];
            string messageContent = JsonConvert.SerializeObject(new { content = checkInMessage["checkInMessageContent"]?.ToString() });
    
            return await SendGetAroundMessage(_logger, messageContent, rental_id);
        }

        private static async Task<OkObjectResult> CheckoutMessageAsync( ILogger<HttpTriggerGetAroundTest> _logger, string rental_id, string carId){
            var data = await SanityMessage(_logger, carId, "checkoutMessage"); 
            var checkoutMessage = data["result"]["checkoutMessage"]["checkoutMessageContent"];
            string messageContent = JsonConvert.SerializeObject(new { content = checkoutMessage?.ToString() });
            
            return await SendGetAroundMessage(_logger, messageContent, rental_id);
        }

        private static async Task<JObject> SanityMessage( ILogger<HttpTriggerGetAroundTest> _logger, string carId, string messageType){
            string query = $"*[_type == 'car' && carId == '{carId}'][0]{{{messageType}->}}"; 
            string url = $"https://{projectId}.api.sanity.io/v1/data/query/{dataset}?query={Uri.EscapeDataString(query)}";

            httpSanityClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage response = await httpSanityClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Error fetching data from Sanity: {response.ReasonPhrase}");
            }


            string responseBody = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<JObject>(responseBody);
            return data;
        }

        private static async Task<OkObjectResult> SendGetAroundMessage( ILogger<HttpTriggerGetAroundTest> _logger, string  messageContent, string rental_id){
                _logger.LogInformation(messageContent);

                // // Actual sending of message should be commented when testing since we dont have a testing environment!!!!
                //  var jsonMessage = new StringContent(messageContent, Encoding.UTF8, "application/json");
                // HttpResponseMessage sendMessageResponse = await client.PostAsync($"owner/v1/rentals/{rental_id}/messages.json", jsonMessage);

                // if(sendMessageResponse.IsSuccessStatusCode){
                //     _logger.LogInformation("Successfully sent message");
                //     return new OkObjectResult("Successfully sent message");
                // }
                // else{
                //     var statusCode = sendMessageResponse.StatusCode;
                //     _logger.LogInformation($"Message sending failed, with status code: {statusCode}");
                //    return new OkObjectResult($"Message sending failed, with status code: {statusCode}"); 
                // }
                _logger.LogInformation("Message sending failed, with status code: All the way!!");
                return new OkObjectResult($"Message sending failed, with status code: All the way!!");
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
