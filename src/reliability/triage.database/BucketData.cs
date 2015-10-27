using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace triage.database
{
    public class BucketData : Bucket
    {
        public int HitCount { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }
        
    }
}
