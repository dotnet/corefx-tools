// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace triage.database
{
    internal class TriageDbContext : DbContext
    {
        public TriageDbContext(string connStr) : base(connStr)
        {
        }

        public DbSet<Bucket> Buckets { get; set; }
        public DbSet<Dump> Dumps { get; set; }
        public DbSet<Thread> Threads { get; set; }
        public DbSet<Frame> Frames { get; set; }
        public DbSet<Routine> Routines { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Property> Properties { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Routine>().MapToStoredProcedures();
        }
    }
}
