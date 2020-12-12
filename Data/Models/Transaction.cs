using System;
using System.Collections.Generic;
using System.Text;

namespace dm.YLD.Data.Models
{
    public class Transaction
    {
        public int TransactionId { get; set; }
        public string BlockNumber { get; set; }
        public DateTimeOffset TimeStamp { get; set; }
        public string Hash { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Value { get; set; }
    }
}
