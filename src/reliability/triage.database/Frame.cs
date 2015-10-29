// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using System.ComponentModel.DataAnnotations.Schema;

namespace triage.database
{
    public class Frame
    {
        public int FrameId { get; set; }

        public int ThreadId { get; set; }

        public int ModuleId { get; set; }

        public int RoutineId { get; set; }

        public int Index { get; set; }

        public string Offset { get; set; }

        public bool Inlined { get; set; }

        [ForeignKey("ThreadId")]
        public virtual Thread Thread { get; set; }

        [ForeignKey("ModuleId")]
        public virtual Module Module { get; set; }

        [ForeignKey("RoutineId")]
        public virtual Routine Routine { get; set; }
    }
}