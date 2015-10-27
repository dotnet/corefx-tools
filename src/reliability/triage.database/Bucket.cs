using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace triage.database
{
    public class Bucket
    {
        public Bucket()
        {
        }

        public int BucketId { get; set; }

        [Required]
        public string Name { get; set; }

        public string BugUrl { get; set; }
    }
}
