using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace dm.YLD.Data.Models
{
    public enum PriceSource
    {
        CoinGecko = 0
    }

    public class Price
    {
        [JsonIgnore]
        public int PriceId { get; set; }
        public DateTime Date { get; set; }
        [JsonIgnore]
        public PriceSource Source { get; set; }
        [JsonIgnore]
        public Guid Group { get; set; }

        [Column(TypeName = "decimal(11, 6)")]
        public decimal PriceUSD { get; set; }
        public Change PriceUSDChange { get; set; }
        [Column(TypeName = "decimal(12, 8)")]
        public decimal PriceUSDChangePct { get; set; }
        [Column(TypeName = "decimal(16, 8)")]
        public decimal PriceETH { get; set; }
        public Change PriceETHChange { get; set; }
        [Column(TypeName = "decimal(12, 8)")]
        public decimal PriceETHChangePct { get; set; }
        [Column(TypeName = "decimal(16, 8)")]
        public decimal PriceBTC { get; set; }
        public Change PriceBTCChange { get; set; }
        [Column(TypeName = "decimal(12, 8)")]
        public decimal PriceBTCChangePct { get; set; }

        public int MarketCapUSD { get; set; }
        public Change MarketCapUSDChange { get; set; }
        [Column(TypeName = "decimal(12, 8)")]
        public decimal MarketCapUSDChangePct { get; set; }

        public int VolumeUSD { get; set; }
        public Change VolumeUSDChange { get; set; }
        [Column(TypeName = "decimal(12, 8)")]
        public decimal VolumeUSDChangePct { get; set; }
    }
}
