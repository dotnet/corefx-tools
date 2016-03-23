// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

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
            this.FrameworkReferences = new AssemblyReferenceSet();

            this.ReferencedAssemblies = new AssemblyReferenceSet();
        }

        public AssemblyReferenceSet FrameworkReferences { get; set; }

        public AssemblyReferenceSet ReferencedAssemblies { get; set; }
    }

    [Serializable]
    public class AssemblyReferenceSet : HashSet<AssemblyReference>
    {
        public AssemblyReferenceSet() : base(new RefComparer())
        {
        }

        public AssemblyReferenceSet(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        //private static RefComparer s_Comparer = new RefComparer();

        [Serializable]
        private class RefComparer : IEqualityComparer<AssemblyReference>
        {
            public bool Equals(AssemblyReference x, AssemblyReference y)
            {
                return x.Name == y.Name;
            }

            public int GetHashCode(AssemblyReference obj)
            {
                return obj.Name.GetHashCode();
            }
        }
    }

    [Serializable]
    public class AssemblyReference
    {
        public string Name { get { return System.IO.Path.GetFileName(this.Path); } }

        public string Path { get; set; }

        public string Version { get; set; }
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

        public DateTime AssemblyLastModified { get; set; }

        public string AssemblyName { get { return Path.GetFileName(this.AssemblyPath); } }

        public string AssemblyAlias
        {
            get
            {
                return GetAssemblyAlias(this.AssemblyName);
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



        public static string GetAssemblyAlias(string assemblyName)
        {
            string alias = null;

            lock (s_aliasLock)
            {
                if (!s_aliasTable.TryGetValue(assemblyName, out alias))
                {
                    alias = "ASSM_" + s_aliasIdx++.ToString("X8");

                    s_aliasTable[Path.GetFileName(assemblyName)] = alias;
                }
            }

            return alias;
        }
    }
}