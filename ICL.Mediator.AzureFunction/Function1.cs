using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Xml;
using System.Xml.Linq;
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
        public async Task Run([ServiceBusTrigger("asn", "asn-scm-booking", Connection = "AzureWebJobsMyServiceBus")]string mySbMsg, ILogger log)
        {
            try
            {
                log.LogInformation("Started at " + DateTime.Now);
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
                    log.LogInformation("SCM response at " + DateTime.Now);
                    log.LogInformation(responseContent);
                    XDocument doc = XDocument.Parse(responseContent.ToString());
                    XDocument asnDoc = XDocument.Parse(mySbMsg.ToString());
                    string jsonText = JsonConvert.SerializeXNode(doc);
                    string asnJSONText = JsonConvert.SerializeXNode(asnDoc);
                    dynamic dyn = JsonConvert.DeserializeObject<ExpandoObject>(jsonText);
                    dynamic asndyn = JsonConvert.DeserializeObject<ExpandoObject>(asnJSONText);

                    MiddlewareResponse mwresponse = new MiddlewareResponse();
                    if (dyn.ArrayOfTransaction.Transaction.Status == "Fail")
                    {
                        var errorMessage = string.Empty;
                        foreach (var error in dyn.ArrayOfTransaction.Transaction.Errors.Error)
                        {
                            errorMessage += ", " + error.Description;
                        }
                        mwresponse.BookingNo = asndyn.Message.Bookings.Booking.BasicDetails.BookingNo;
                        //mwresponse.SCMID = "";
                        mwresponse.ErrorString = errorMessage;
                        mwresponse.DeliveryStatus = "Failed";
                    }
                    else
                    {
                        if (((IDictionary<String, object>)dyn.ArrayOfTransaction.Transaction).ContainsKey("CutomerRefNo"))
                        {
                            mwresponse.BookingNo = dyn.ArrayOfTransaction.Transaction.CutomerRefNo;
                        }
                        if (((IDictionary<String, object>)dyn.ArrayOfTransaction.Transaction).ContainsKey("TransactionId"))
                        {
                            mwresponse.SCMID = dyn.ArrayOfTransaction.Transaction.TransactionId;
                        }
                        mwresponse.DeliveryStatus = "Delivered";
                    }

                    var respnsedata = JsonConvert.SerializeObject(mwresponse);
                    var content = new StringContent(respnsedata, Encoding.UTF8, "application/json");
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", null);
                    var res = await _httpClient.PostAsync($"https://icl-dwh-backend.azurewebsites.net/api/PurchaseOrder/UpdatePurchaseOrderStatus", content);
                    var dwhResponseContent = await res.Content.ReadAsStringAsync();
                    log.LogInformation("DEH.Backend response at " + DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Error : {ex.Message}");
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
