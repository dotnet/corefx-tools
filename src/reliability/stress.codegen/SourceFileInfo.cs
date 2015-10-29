// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{
    // enum with action
    // Compile, Binplace, Resource
    public enum SourceFileAction
    {
        None,
        Binplace,
        Compile,
        Resource
    }

    public class SourceFileInfo
    {
        public string FileName { get; set; }
        public SourceFileAction SourceFileAction { get; set; }

        public SourceFileInfo()
        {
            FileName = "";
            SourceFileAction = SourceFileAction.None;
        }
        public SourceFileInfo(string fileName, SourceFileAction sourceFileAction)
        {
            FileName = fileName;
            SourceFileAction = sourceFileAction;
        }
    }
}
