using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace ICL.Mediator.AzureFunction
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        private static readonly HttpClient _httpClient = new HttpClient();

        public Function1(ILogger<Function1> log)
        {
            _logger = log;
        }

        [FunctionName("Function1")]
        public async Task Run([ServiceBusTrigger("booking-demo", "bookings-test", Connection = "AzureWebJobsMyServiceBus")]string mySbMsg)
        {
            var requestContent = new StringContent("grant_type=password&username=fitexpress&password=FitExpress@2021");
            var response = await _httpClient.PostAsync("http://fitnewuat.hht.freightintime.com/token", requestContent);
            var responseBody = await response.Content.ReadAsAsync<SCMResponse>();
            var token = responseBody.access_token;
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var bookingRequestContent = new StringContent(mySbMsg);
            var bookingResponse = await _httpClient.PostAsync("http://fitnewuat.hht.freightintime.com/api/v1/Booking/ProcessBooking", bookingRequestContent);
            var responseContent = await bookingResponse.Content.ReadAsStringAsync();
            _logger.LogInformation($"C# ServiceBus topic trigger function processed message: {mySbMsg}");
        }
    }

    public class SCMResponse
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
    }
}
