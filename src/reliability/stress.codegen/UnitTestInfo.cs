// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace stress.codegen
{
    public class TestAssemblyInfo
    {
        public Assembly Assembly { get; set; }

        public TestReferenceInfo ReferenceInfo { get; set; }
    }

    [Serializable]
    public class TestReferenceInfo
    {
        public TestReferenceInfo()
        {
            this.FrameworkReferences = new List<string>();

            this.ReferencedAssemblies = new List<AssemblyReference>();
        }

        public List<string> FrameworkReferences { get; set; }

        public List<AssemblyReference> ReferencedAssemblies { get; set; }
    }

    [Serializable]
    public class AssemblyReference
    {
        public string Name { get { return System.IO.Path.GetFileName(this.Path); } }

        public string Path { get; set; }
    }

    [Serializable]
    public class TestClassInfo
    {
        public bool HasDefaultCtor { get; set; }
        public bool IsPublic { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsGenericType { get; set; }
        public string FullName { get; set; }
    }

    [Serializable]
    public class TestMethodInfo
    {
        public bool IsStatic { get; set; }

        public bool IsPublic { get; set; }

        public bool IsAbstract { get; set; }

        public bool IsVoidReturn { get; set; }

        public bool IsGenericMethodDefinition { get; set; }

        public string Name { get; set; }
    }

    [Serializable]
    public class UnitTestInfo
    {
        private static int s_aliasIdx = 0;
        private static object s_aliasLock = new object();
        private static Dictionary<string, string> s_aliasTable = new Dictionary<string, string>();

        //public Assembly TestAssembly { get; set; }

        public TestClassInfo Class { get; set; }

        public TestMethodInfo Method { get; set; }

        public TestReferenceInfo ReferenceInfo { get; set; }

        public string AssemblyPath { get; set; }

        public string AssemblyName { get { return Path.GetFileName(this.AssemblyPath); } }

        public string AssemblyAlias
        {
            get
            {
                return GetAssemblyAlias(this.AssemblyPath);
            }
        }

        public string QualifiedTypeStr
        {
            get
            {
                return $"{this.AssemblyAlias}::{this.Class.FullName.Replace('+', '.')}";
            }
        }

        public string QualifiedMethodStr
        {
            get
            {
                string typeName = this.Method.IsStatic ? this.QualifiedTypeStr + "." : string.Empty;

                return $"{typeName}{this.Method.Name}";
            }
        }

        public string SkipReason { get; set; }

        public bool IsLoadTestCandidate()
        {
            if (this.SkipReason != null)
            {
                return false;
            }

            if ((this.Class == null) || !this.Class.IsPublic || this.Class.IsAbstract || this.Class.IsGenericType || !this.Class.HasDefaultCtor)
            {
                this.SkipReason = "Test class not accessible";

                return false;
            }

            if ((this.Method == null) || !this.Method.IsPublic || this.Method.IsAbstract || this.Method.IsGenericMethodDefinition)
            {
                this.SkipReason = "Test method not accessible";

                return false;
            }

            return true;
        }



        public static string GetAssemblyAlias(string assemblyPath)
        {
            string alias = null;

            lock (s_aliasLock)
            {
                if (!s_aliasTable.TryGetValue(assemblyPath, out alias))
                {
                    alias = "ASSM_" + s_aliasIdx++.ToString("X8");

                    s_aliasTable[assemblyPath] = alias;
                }
            }

            return alias;
        }
    }
}