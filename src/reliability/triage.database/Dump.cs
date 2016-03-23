// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace triage.database
{
    public class Dump
    {
        public Dump()
        {
            this.Threads = new HashSet<Thread>();
        }

        public int DumpId { get; set; }

        public string DumpPath { get; set; }

        public string Origin { get; set; }

        public int? BucketId { get; set; }

        [Required]
        [Index]
        public DateTime DumpTime { get; set; }

        public virtual ICollection<Thread> Threads { get; set; }

        public virtual ICollection<Property> Properties { get; set; }

        [ForeignKey("BucketId")]
        public virtual Bucket Bucket { get; set; }
    }
}
