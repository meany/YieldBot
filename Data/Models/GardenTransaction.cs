using System;

namespace dm.YLD.Data.Models
{
    public class GardenTransaction
    {
        public int GardenTransactionId { get; set; }
        public string BlockNumber { get; set; }
        public DateTimeOffset TimeStamp { get; set; }
        public string Hash { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Value { get; set; }
        public LPPair Pair { get; set; }
    }
}
