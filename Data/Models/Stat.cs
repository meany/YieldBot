using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace dm.YLD.Data.Models
{
    public enum Change
    {
        None = 0,
        Down = 1,
        Up = 2
    }

    public class Stat
    {
        [JsonIgnore]
        public int StatId { get; set; }
        public DateTime Date { get; set; }
        [JsonIgnore]
        public Guid Group { get; set; }
        public int Transactions { get; set; }
        [Column(TypeName = "decimal(25, 18)")]
        public decimal Supply { get; set; }
        [Column(TypeName = "decimal(25, 18)")]
        public decimal FullCirculation { get; set; }
        [Column(TypeName = "decimal(25, 18)")]
        public decimal HolderCirculation { get; set; }
        [Column(TypeName = "decimal(25, 18)")]
        public decimal UniswapRFISupply { get; set; }
        [Column(TypeName = "decimal(25, 18)")]
        public decimal UniswapETHSupply { get; set; }
    }
}
