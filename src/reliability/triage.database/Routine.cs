// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace triage.database
{
    public class Routine
    {
        public int RoutineId { get; set; }

        [Required]
        [Index(IsUnique = true)]
        [StringLength(450)]
        public string Name { get; set; }
    }
}