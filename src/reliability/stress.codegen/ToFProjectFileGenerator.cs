// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen
{
    public static class ToFProjectFileGenerator
    {
        public static void GenerateProjectFile(LoadTestInfo loadTest)
        {
            string refSnippet = GenerateReferencesSnippet(loadTest);
            string fxSnippet = GenerateFrameworkReferencesSnippet(loadTest);
            string itemSnippet = GenerateSourceFileItemsSnippet(loadTest);
            string projFileContent = string.Format(PROJECT_TEMPLATE, refSnippet, itemSnippet); //, fxSnippet);

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

        private static string GenerateReferencesSnippet(LoadTestInfo loadTest)
        {
            HashSet<string> uniqueAssemblies = new HashSet<string>();

            StringBuilder snippet = new StringBuilder();
            foreach (var test in loadTest.UnitTests)
            {
                if (uniqueAssemblies.Add(test.AssemblyPath))
                {
                    string refSnippet = $@"
    <Reference Include='{test.AssemblyName}'>
      <HintPath>$(MSBuildThisFileDirectory)\refs\{test.AssemblyName}</HintPath>
      <Aliases>{UnitTestInfo.GetAssemblyAlias(test.AssemblyPath)}</Aliases>
      <NotForTests>true</NotForTests>
    </Reference>";

                    snippet.Append(refSnippet);
                }

                foreach (var assmref in test.ReferenceInfo.ReferencedAssemblies)
                {
                    if (uniqueAssemblies.Add(assmref.Path))
                    {
                        string refSnippet = $@"
    <Reference Include='{assmref.Name}'>
      <HintPath>$(MSBuildThisFileDirectory)\refs\{assmref.Name}</HintPath>
      <Aliases>{UnitTestInfo.GetAssemblyAlias(assmref.Path)}</Aliases>
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

            foreach (var testfxRefs in loadTest.UnitTests.Select(t => t.ReferenceInfo.FrameworkReferences))
            {
                fxRefSet.UnionWith(testfxRefs);
            }

            foreach (var fxref in fxRefSet)
            {
                string refSnippet;

                if (fxref.Version != "4.0.0.0")
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
      <Version>1.0.0-alpha-00003</Version>
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
  <ItemGroup>
    {1}
  </ItemGroup>
  <ItemGroup>
    <CLRTestContractReference Include='System.Runtime' />
    <CLRTestContractReference Include='System.Runtime.Extensions' />
    <CLRTestContractReference Include='System.Linq' />
    <CLRTestContractReference Include='System.Collections' />
    <CLRTestContractReference Include='System.Threading' />
    <CLRTestContractReference Include='System.Threading.Tasks' />
  </ItemGroup>
  <ItemGroup>{0}
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include='$(CLRTestStoreRoot)\stress\common\stress.execution.csproj'>
      <Project>{{7E6BD405-347A-4DDE-AE19-43372FA7D697}}</Project>
    </ProjectReference>
  </ItemGroup>
  <Import Project='$(CLRTestRoot)\CLRTest.targets' />
</Project>";

        private static readonly string[] s_systemRefs = new string[] { "System.Runtime", "System.Runtime.Extensions", "System.Linq", "System.Threading", "System.Threading.Tasks", "System.Collections" };
    }
}
