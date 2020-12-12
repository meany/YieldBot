using System;

namespace dm.YLD.Data.Models
{
    public enum RequestType
    {
        Price
    }

    public enum RequestResponse
    {
        OK,
        RateLimited,
        Error
    }

    public class Request
    {
        public int RequestId { get; set; }
        public DateTime Date { get; set; }
        public string User { get; set; }
        public RequestType Type { get; set; }
        public RequestResponse Response { get; set; }
    }
}
