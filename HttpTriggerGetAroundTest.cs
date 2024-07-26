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
using Microsoft.Extensions.ObjectPool;

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
        private static readonly string editorToken = "skn8xGKOBi2oKlzxvEzcsHQZ8iswfOZq1gfheUIFBj7RWoaYjwnFBxRNf8tbPDnynwTWMTNjgGd0mnu5KDl1Os0daBsMOwv5HtG3Ejr5FhJ9kJgzWcFwTS1uZS1SR89JbgeStWCiVTGQR4ejjSv8xzvsDnWTRJJE2fMPepRNI0SyJ4nlrKUH";
        public HttpTriggerGetAroundTest(ILogger<HttpTriggerGetAroundTest> logger)
        {
            _logger = logger;
        }

        public class Message{
            public int id { get; set; }
        }

        public class Invoice{
            public int id { get; set; }
        }

        [Function("HttpTriggerGetAroundTest")]
        public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post",  Route = null)] HttpRequest req)
        {
            _logger.LogInformation("Function initialized.");
            
            return await InvoiceDataTransferAsync(_logger);
            // string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // dynamic data = JsonConvert.DeserializeObject(requestBody);

            // string type = data?.type;

            // string rental_id = data?.data?.rental_id;

            // if(rental_id != null){
            //     JToken carId = await GetCarIdAsync(rental_id);
            //     if(type == "rental.booked") return await BookingMessageAsync(_logger, rental_id, carId.ToString());
            //     else if(type == "rental.car_checked_in") return await CheckInMessageAsync(_logger, rental_id, carId.ToString());
            //     else if(type == "rental.car_checked_out") return await CheckoutMessageAsync(_logger, rental_id, carId.ToString());
            // }

            //return new OkObjectResult("Event is either not rental.booked,  or/and rental id is null");
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

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Invoice migration til sanity for testing purposes
        private static async Task<OkObjectResult> InvoiceDataTransferAsync( ILogger<HttpTriggerGetAroundTest> _logger){
            // 1. Get invoice ids for a month from GA
            List<Invoice> invoiceIdsFormatted = await GetInvoiceIds(_logger); 
             
            // 2. Map array with IDs
            invoiceIdsFormatted.ForEach(async invoice =>
                {
                    _logger.LogInformation($"Processing Invoice ID: {invoice.id}");
                    // 3. Get invoice based on ID
                    JToken invoiceData = await GetInvoice(invoice.id.ToString()); 
                    // 4. Generate Sanity invoice data based on invoice from GA
                    JToken invoiceSanityFormate = GenerateSanityInvoiceSchema(_logger, invoiceData);
                    // 5. Send invoice to sanity
                    StringContent  invoiceSanityFormateString = new StringContent(invoiceSanityFormate.ToString(), Encoding.UTF8, "application/json");

                    string url = $"https://{projectId}.api.sanity.io/v1/data/mutate/{dataset}";
                    httpSanityClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", editorToken);

                    HttpResponseMessage response = await httpSanityClient.PostAsync(url, invoiceSanityFormateString);
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorResponse = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"Error sending data to Sanity: {response.ReasonPhrase} - {errorResponse}");
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Response from Sanity: {responseBody}");
                });
            return new OkObjectResult("All done!:)");
        }

        private static async Task<List<Invoice>> GetInvoiceIds(ILogger<HttpTriggerGetAroundTest> _logger){
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Getaround-Version", "2023-08-08.0");

            HttpResponseMessage invoiceIds = await client.GetAsync($"owner/v1/invoices.json?start_date= 2024-01-01T00:00:00Z&end_date= 2024-01-31T23:59:59Z&page=1&per_page=40");
         
             if(invoiceIds.IsSuccessStatusCode){ 
                 string responseBody = await invoiceIds.Content.ReadAsStringAsync();
                 List<Invoice> invoiceIdsFormatted = JsonConvert.DeserializeObject<List<Invoice>>(responseBody);
                 return invoiceIdsFormatted ?? null;
                 }
                 return null; 
        }

        private static async Task<JToken> GetInvoice(string invoice_id){
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Getaround-Version", "2023-08-08.0");

            HttpResponseMessage invoiceResponse = await client.GetAsync($"owner/v1/invoices/{invoice_id}.json");
            if(invoiceResponse.IsSuccessStatusCode){ 
                string responseBody = await invoiceResponse.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<JObject>(responseBody);
                return data ?? null;
                }
                return null;
        }

         static JObject GenerateSanityInvoiceSchema( ILogger<HttpTriggerGetAroundTest> _logger, JToken invoice){
            var chargeNameMapping = new Dictionary<string, string>
            {
                { "driver_rental_payment", "Betaling for sjåførleie" },
                { "self_insurance_payment", "Betaling for sjåførleie" },
                { "additional_self_insurance_payment", "Ekstra egenforsikringsbetaling" },
                { "mileage_package", "Betaling for kilometerpakke" },
                { "mileage_package_insurance", "Betaling for kilometerpakke frosikring" },
                { "extra_distance_payment", "Betaling for ekstra avstandsbetaling" },
                { "driver_compensation", "Betaling for førerkompensasjon" },
                { "driver_cancellation_fee", "Avbestillingsgebyr for sjåføren" },
                { "driver_late_return_fee", "Gebyr for sen retur av sjåføren" },
                { "driver_gas_refill_fee", "Påfyllingsgebyr for sjåfør" },
                { "driver_recharging_fee", "Ladegebyr for sjåfør" },
                { "drivy_cancellation_fee", "Avbestillingsgebyr" },
                { "claims_owner_fee_cg", "Krav eieravgift cg" },
                { "repatriation_fee", "Hjemtransportgebyr" },
                { "driver_infraction_fee", "Førerovertredelsesgebyr" },
                { "driver_mess_fee", "Rotegebyr" },
                { "insurance_fee", "Forsikringsavgift" },
                { "assistance_fee", "Assistanseavgift" },
                { "drivy_unfulfillment_fee", "Uoppfyllelsesgebyr" },
                { "drivy_service_fee", "Serviceavgift" },
                { "drivy_breakdown_management_fee", "Administrasjonsgebyr" },
                { "driver_gas_compensation", "Kompensasjon for drivstoff" },
                { "driver_toll_compensation", "Kompensasjon for tollavgift" },
                { "driver_compensation_for_offsite_payment", "Sjåførkompensasjon for offsite betaling" },
                { "owner_infraction_compensation", "Eierovertredelseserstatning" },
                { "drivy_gas_compensation", "Drivgasskompensasjon" },
                { "exceptional_event_compensation", "Kompensasjon for ekstraordinære hendelser" },
                { "damage_compensation", "Skadeserstatning" },
                { "other_compensation", "Annen kompensasjon" },
                { "guarantee_earning", "Garanti inntjening" },
            };

            var newToken = new JObject
            {
                ["mutations"]=new JArray(
                      new JObject
                    {
							["create"] =  new JObject{
                            ["_type"] = "invoice",
                                ["charges"] = new JArray(
                                    invoice["charges"].Select(c => new JObject
                                    {
                                        ["_key"] = Guid.NewGuid().ToString("N"),
                                        ["_type"] = "charge",
                                        ["amount"] = c["amount"].ToString(),
                                        ["chargeName"] = chargeNameMapping[c["type"].ToString()],
                                        ["type"] = c["type"]
                                    })
                                ),
                                ["currency"] = invoice["currency"],
                                ["emittedAt"] = DateTime.Parse(invoice["emitted_at"].ToString()).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                ["entityType"] = invoice["entity_type"],
                                ["invoiceId"] = invoice["id"].ToString(),
                                ["pdfUrl"] = invoice["pdf_url"].Type == JTokenType.Null ? "Ingen link oppgitt fra GetAround" : invoice["pdf_url"],
                                ["productId"] = invoice["product_id"].ToString(),
                                ["productType"] = invoice["product_type"],
                                ["totalPrice"] = invoice["total_price"].ToString()}
                })
        };

            return newToken;
        }
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

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
