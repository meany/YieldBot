using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace dm.YLD.Data.Models
{
    public class Holder
    {
        [JsonIgnore]
        public int HolderId { get; set; }
        public string Address { get; set; }
        public string Value { get; set; }
        public string FirstBlockNumber { get; set; }
        public DateTimeOffset FirstTimeStamp { get; set; }
    }
}
