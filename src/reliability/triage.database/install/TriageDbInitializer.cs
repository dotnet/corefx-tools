// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            context.Database.ExecuteSqlCommand(InstallScripts.Create_Module_Insert);
            context.Database.ExecuteSqlCommand(InstallScripts.Create_Routine_Insert);
        }
    }
}
