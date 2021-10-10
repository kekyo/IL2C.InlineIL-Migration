/////////////////////////////////////////////////////////////////////////////////////////////////
//
// IL2C - A translator for ECMA-335 CIL/MSIL to C language.
// Copyright (c) 2016-2019 Kouji Matsui (@kozy_kekyo, @kekyo2)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//	http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using Mono.Cecil;
using System;
using System.IO;
using System.Linq;

namespace Extractor
{
    class Program
    {
        static void Main(string[] args)
        {
            // ..\..\..\..\..\before\IL2C.Core.Test.ILConverters.dll
            // ..\..\..\..\..\merged\IL2C.Core.Test.ILConverters.dll
            // ..\..\..\..\..\src\IL2C.Core.Test.ILConverters
            // output

            var beforeAssemblyPath = Path.GetFullPath(args[0]);
            var mergedAssemblyPath = Path.GetFullPath(args[1]);
            var il2cSourceBasePath = Path.GetFullPath(args[2]);
            var outputBasePath = Path.GetFullPath(args[3]);

            var beforeAssembly = AssemblyDefinition.ReadAssembly(
                beforeAssemblyPath,
                new ReaderParameters
                {
                    AssemblyResolver = new BasePathAssemblyResolver(Path.GetDirectoryName(beforeAssemblyPath)),
                    ReadSymbols = true,
                    ReadingMode = ReadingMode.Immediate,
                    ReadWrite = false
                });

            var mergedAssembly = AssemblyDefinition.ReadAssembly(
                mergedAssemblyPath, new ReaderParameters
                {
                    AssemblyResolver = new BasePathAssemblyResolver(Path.GetDirectoryName(mergedAssemblyPath)),
                    ReadSymbols = true,
                    ReadingMode = ReadingMode.Immediate,
                    ReadWrite = false
                });

            // Step 1: Before assembly contains `methodref` attribute.
            // <example>
            // .method public hidebysig static
            //    uint32 Byte(
            //       uint8 'value'
            // ) cil managed forwardref 
            // {
            // } // end of method Conv_u4::Byte
            // </example>

            var targetBeforeMethods = beforeAssembly.Modules.
                SelectMany(md => md.Types).
                Where(t => t.IsPublic && t.IsClass).
                SelectMany(t => t.Methods).
                Where(m => (m.ImplAttributes & MethodImplAttributes.ForwardRef) == MethodImplAttributes.ForwardRef).
                ToArray();

            // Step 2: Resolve merged assembly real method by found methods.
            var mergedMethods = mergedAssembly.Modules.
                SelectMany(md => md.Types).
                Where(t => t.IsPublic && t.IsClass).
                SelectMany(t => t.Methods).
                ToDictionary(m => m.FullName);

            //var missingMethods = targetBeforeMethods.
            //    Where(m => !mergedMethods.ContainsKey(m.FullName)).
            //    ToArray();

            var targetMergedMethods = targetBeforeMethods.
                Select(m => (before: m, merged: mergedMethods[m.FullName])).
                ToArray();

            // Step 3: Patch source code.

        }
    }
}
