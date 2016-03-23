// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using stress.execution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{
    public static class HelixProjectFileGenerator
    {
        public static void GenerateProjectFile(LoadTestInfo loadTest)
        {
            string refSnippet = GenerateTestReferencesSnippet(loadTest);

            string fxSnippet = GenerateFrameworkReferencesSnippet(loadTest);

            string itemSnippet = GenerateSourceFileItemsSnippet(loadTest);

            string propertySnippet = GenerateTestPropertiesSnippet(loadTest);

            //format the project template {0} source files, {1} framework references, {2} test references, {3} test properties
            string projFileContent = string.Format(PROJECT_TEMPLATE, itemSnippet, fxSnippet, refSnippet, propertySnippet);

            File.WriteAllText(Path.Combine(loadTest.SourceDirectory, loadTest.TestName + ".csproj"), projFileContent);
        }

        private static string GenerateTestPropertiesSnippet(LoadTestInfo loadTest)
        {
            //timeout = test duration + 5 minutes for dump processing ect.
            string timeoutInSeconds = Convert.ToInt32((loadTest.Duration + TimeSpan.FromMinutes(5)).TotalSeconds).ToString();

            string propertyString = $@"
    <TestAssembly>stress.execution</TestAssembly>
    <TimeoutInSeconds>{timeoutInSeconds}</TimeoutInSeconds>";

            return propertyString;
        }

        private static string GenerateSourceFileItemsSnippet(LoadTestInfo loadTest)
        {
            StringBuilder snippet = new StringBuilder();

            foreach (var file in loadTest.SourceFiles)
            {
                string itemSnippet = string.Empty;
                if (file.SourceFileAction == SourceFileAction.Compile)
                {
                    itemSnippet = $@"
    <Compile Include='{Path.GetFileName(file.FileName)}'/>";
                }
                else if (file.SourceFileAction == SourceFileAction.Binplace)
                {
                    itemSnippet = $@"
    <None Include='{Path.GetFileName(file.FileName)}'>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None> ";
                }

                snippet.Append(itemSnippet);
            }

            return snippet.ToString();
        }

        private static string GenerateTestReferencesSnippet(LoadTestInfo loadTest)
        {
            HashSet<string> uniqueAssemblies = new HashSet<string>();

            StringBuilder snippet = new StringBuilder();
            foreach (var test in loadTest.UnitTests)
            {
                if (uniqueAssemblies.Add(test.AssemblyName))
                {
                    string refSnippet = $@"
    <Reference Include='{test.AssemblyName}'>
      <HintPath>$(MSBuildThisFileDirectory)\refs\{test.AssemblyName}</HintPath>
      <Aliases>{UnitTestInfo.GetAssemblyAlias(test.AssemblyName)}</Aliases>
      <NotForTests>true</NotForTests>
    </Reference>";

                    snippet.Append(refSnippet);
                }

                foreach (var assmref in test.ReferenceInfo.ReferencedAssemblies)
                {
                    if (uniqueAssemblies.Add(assmref.Name))
                    {
                        string refSnippet = $@"
    <Reference Include='{assmref.Name}'>
      <HintPath>$(MSBuildThisFileDirectory)\refs\{assmref.Name}</HintPath>
      <Aliases>{UnitTestInfo.GetAssemblyAlias(assmref.Name)}</Aliases>
      <NotForTests>true</NotForTests>
    </Reference>";

                        snippet.Append(refSnippet);
                    }
                }
            }

            return snippet.ToString();
        }

        private static string GenerateFrameworkReferencesSnippet(LoadTestInfo loadTest)
        {
            HashSet<string> uniqueAssemblies = new HashSet<string>();

            StringBuilder snippet = new StringBuilder();

            AssemblyReferenceSet fxRefSet = new AssemblyReferenceSet();

            fxRefSet.UnionWith(s_infraFxRefs);

            foreach (var testfxRefs in loadTest.UnitTests.Select(t => t.ReferenceInfo.FrameworkReferences))
            {
                fxRefSet.UnionWith(testfxRefs);
            }

            foreach (var fxref in fxRefSet)
            {
                string refSnippet;

                if (fxref.Version.StartsWith("4.0"))
                {
                    refSnippet = $@"
    <CLRTestContractReference Include='{Path.GetFileNameWithoutExtension(fxref.Name)}'/>";
                }
                else
                {
                    refSnippet = $@"
    <CLRTestContractReference Include='{Path.GetFileNameWithoutExtension(fxref.Name)}'>
      <Version>{fxref.Version}</Version>
    </CLRTestContractReference>";
                }

                snippet.Append(refSnippet);
            }
            snippet.Append(@"
    <CLRTestContractReference Include='Microsoft.DotNet.stress.execution'>
      <SkipSupportVerification>true</SkipSupportVerification>
      <Version>1.0.0-alpha-00031</Version>
    </CLRTestContractReference>");

            return snippet.ToString();
        }
        private const string PROJECT_TEMPLATE = @"<?xml version='1.0' encoding='utf-8'?>
<Project ToolsVersion = '4.0' DefaultTargets='Build' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
  <Import Project = '$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory), CLRTest.Common.props))\CLRTest.Common.props' />
  <PropertyGroup>
    <SignAssembly>false</SignAssembly>
  </PropertyGroup>
  <PropertyGroup>
    <CLRTestOwner>sschaab</CLRTestOwner>
    <CLRTestKind>BuildAndRun</CLRTestKind>
    <CLRTestSuite>Weekly</CLRTestSuite>
  </PropertyGroup>  
  <!-- Test Properties -->
  <PropertyGroup>{3}
  </PropertyGroup>
  <!-- Source Code Files -->
  <ItemGroup>{0}
  </ItemGroup>
  <!-- Framework References -->
  <ItemGroup>{1}
  </ItemGroup>
  <!-- Test Assembly References -->
  <ItemGroup>{2}
  </ItemGroup>
  <Import Project='$(CLRTestRoot)\CLRTest.targets' />
</Project>";

        private static readonly AssemblyReference[] s_infraFxRefs = new AssemblyReference[]
        {
            new AssemblyReference () { Path = "System.Runtime.dll", Version = "4.0.0.0" },
            new AssemblyReference () { Path = "System.Runtime.Extensions.dll", Version = "4.0.0.0" },
            new AssemblyReference () { Path = "System.Linq.dll", Version = "4.0.0.0" },
            new AssemblyReference () { Path = "System.Threading.dll", Version = "4.0.0.0" },
            new AssemblyReference () { Path = "System.Threading.Tasks.dll", Version = "4.0.0.0" },
            new AssemblyReference () { Path = "System.Collections.dll", Version = "4.0.0.0" },
            new AssemblyReference () { Path = "System.Reflection.dll", Version = "4.0.0.0" },
        };

        private static readonly string[] s_systemRefs = new string[] { "System.Runtime", "System.Runtime.Extensions", "System.Linq", "System.Threading", "System.Threading.Tasks", "System.Collections" };
    }
}
