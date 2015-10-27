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

        [ForeignKey("BucketId")]
        public virtual Bucket Bucket { get; set; }
    }
}
