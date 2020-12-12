﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using System.Text;

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
        [IgnoreDataMember]
        public int StatId { get; set; }
        public DateTime Date { get; set; }
        [IgnoreDataMember]
        public Guid Group { get; set; }
        public int Transactions { get; set; }
        [Column(TypeName = "decimal(25, 18)")]
        public decimal Supply { get; set; }
        [Column(TypeName = "decimal(25, 18)")]
        public decimal Circulation { get; set; }
    }
}