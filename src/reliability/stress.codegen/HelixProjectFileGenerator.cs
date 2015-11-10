// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            //format the project template {0} source files, {1} framework references, {2} test references
            string projFileContent = string.Format(PROJECT_TEMPLATE, itemSnippet, fxSnippet, refSnippet);

            File.WriteAllText(Path.Combine(loadTest.SourceDirectory, loadTest.TestName + ".csproj"), projFileContent);
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
            foreach (var fxref in loadTest.UnitTests.SelectMany(t => t.ReferenceInfo.FrameworkReferences).Union(s_systemRefs).Distinct())
            {
                string refSnippet = $@"
     <CLRTestContractReference Include='{fxref}' />";
                snippet.Append(refSnippet);
            }

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

        private static readonly string[] s_systemRefs = new string[] { "System.Runtime", "System.Runtime.Extensions", "System.Linq", "System.Threading", "System.Threading.Tasks", "System.Collections", "Microsoft.DotNet.stress.execution" };
    }
}
