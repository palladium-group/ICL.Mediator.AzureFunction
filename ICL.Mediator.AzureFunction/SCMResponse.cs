using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICL.Mediator.AzureFunction
{
    public class Transaction
    {
        public string ID { get; set; }
        public string Status { get; set; }
        public string CutomerRefNo { get; set; }
        public string TransactionId { get; set; }
    }

    public class ArrayOfTransaction
    {
        public Transaction Transaction { get; set; }
        public string I { get; set; }
        public string Text { get; set; }
    }
}
