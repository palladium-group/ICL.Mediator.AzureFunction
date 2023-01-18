using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICL.Mediator.AzureFunction
{
    public class MiddlewareResponse
    {
        public string BookingNo { get; set; }
        public string ErrorString { get; set; }
        public string DeliveryStatus { get; set; }
        public string SCMID { get; set; }
    }
}
