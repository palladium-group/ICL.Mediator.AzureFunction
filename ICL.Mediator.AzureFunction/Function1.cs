using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
        public async Task Run([ServiceBusTrigger("asn", "asn-scm-booking", Connection = "AzureWebJobsMyServiceBus")]string mySbMsg)
        {
            //push message to scm-profit
            var requestContent = new StringContent("grant_type=password&username=fitexpress&password=FitExpress@2021");
            var response = await _httpClient.PostAsync("http://fitnewuat.hht.freightintime.com/token", requestContent);
            var responseBody = await response.Content.ReadAsAsync<SCMResponse>();
            var token = responseBody.access_token;
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var bookingRequestContent = new StringContent(mySbMsg);
            var bookingResponse = await _httpClient.PostAsync("http://fitnewuat.hht.freightintime.com/api/v1/Booking/ProcessBooking", bookingRequestContent);
            var responseContent = await bookingResponse.Content.ReadAsStringAsync();
            if (bookingResponse.IsSuccessStatusCode)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ArrayOfTransaction));
                using (StringReader reader = new StringReader(responseContent))
                {
                    var scmResponse = (ArrayOfTransaction)serializer.Deserialize(reader);
                    if (scmResponse.Transaction.Status == "Fail")
                    {
                        // update as failed
                        XmlSerializer xmlserializer = new XmlSerializer(typeof(Message));
                        using (StringReader xmlreader = new StringReader(mySbMsg))
                        {
                            var asn = (Message)xmlserializer.Deserialize(xmlreader);
                            var BookingNo = asn.Bookings.Booking.BasicDetails.BookingNo;
                            await _httpClient.GetAsync($"https://icl-dwh-backend.azurewebsites.net/api/PurchaseOrder/UpdatePurchaseOrderAsFailed/{BookingNo}/{responseContent}");
                        }
                    }
                    var transactionId = scmResponse.Transaction.TransactionId;
                    var bookingNo = scmResponse.Transaction.CutomerRefNo;
                    var updateDWHResponse = await _httpClient.GetAsync($"https://icl-dwh-backend.azurewebsites.net/api/PurchaseOrder/{bookingNo}/{transactionId}");
                    var dwhResponseContent = await updateDWHResponse.Content.ReadAsStringAsync();
                }
            }
        }

        [FunctionName("Function2")]
        public async Task Run2([ServiceBusTrigger("asn", "asn-warehouse-booking", Connection = "AzureWebJobsMyServiceBus")] string mySbMsg)
        {
            //push message to DWH
            XmlSerializer serializer = new XmlSerializer(typeof(Message));
            using (StringReader reader = new StringReader(mySbMsg))
            {
                var asn = (Message)serializer.Deserialize(reader);

                var BookingNo = asn.Bookings.Booking.BasicDetails.BookingNo;
                var BookingDate = DateTime.ParseExact(asn.Bookings.Booking.BasicDetails.BookingDate.ToString(), "yyyyMMdd", CultureInfo.InvariantCulture);
                var purchaseOrder = JsonConvert.SerializeObject(new
                {
                    Id = Guid.NewGuid(),
                    CreateDate = DateTime.Now,
                    BookingNo,
                    BookingDate = DateTime.Parse(BookingDate.ToString()),
                    AsnFile = mySbMsg,
                    Status = 0
                });
                var bookingRequestContent = new StringContent(purchaseOrder, Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", null);
                var dwhResponse = await _httpClient.PostAsync("https://icl-dwh-backend.azurewebsites.net/api/PurchaseOrder", bookingRequestContent);
                var responseContent = await dwhResponse.Content.ReadAsStringAsync();
            }
        }
    }

    public class SCMResponse
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
    }
}
