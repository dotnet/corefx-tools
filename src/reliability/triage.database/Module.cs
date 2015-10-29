// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace triage.database
{
    public class Module
    {
        public int ModuleId { get; set; }

        [Required]
        [Index(IsUnique = true)]
        [StringLength(450)]
        public string Name { get; set; }
    }
}