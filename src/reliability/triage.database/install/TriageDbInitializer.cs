using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace triage.database.install
{
    internal class TriageDbInitializer : CreateDatabaseIfNotExists<TriageDbContext>
    {
        public override void InitializeDatabase(TriageDbContext context)
        {
            //call install scripts
        }
    }
}
