﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace triage.database
{
    public class Property
    {
        public int PropertyId { get; set; }

        [Required]
        [Index]
        [StringLength(450)]
        public string Name { get; set; }

        public string Value { get; set; }

        public int DumpId { get; set; }

        [ForeignKey("DumpId")]
        public virtual Dump Dump { get; set; }
    }
}
