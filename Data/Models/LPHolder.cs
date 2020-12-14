using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;

namespace dm.YLD.Data.Models
{
    public enum LPPair
    {
        RFI_YLD = 0,
        ETH_YLD = 1
    }

    public class LPHolder
    {
        [JsonIgnore]
        public int LPHolderId { get; set; }
        public LPPair Pair { get; set; }
        public string Address { get; set; }
        public string Value { get; set; }
        public string FirstBlockNumber { get; set; }
        public DateTimeOffset FirstTimeStamp { get; set; }

        [JsonIgnore]
        public virtual BigInteger ValueBigInt {
            get {
                return BigInteger.Parse(Value);
            }
        }
    }
}
