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

namespace Extractor
{
    class Program
    {
        static void Main(string[] args)
        {
            var beforeAssemblyPath = Path.GetFullPath(args[0]);
            var mergedAssemblyPath = Path.GetFullPath(args[1]);
            var il2cSourceBasePath = Path.GetFullPath(args[2]);
            var outputBasePath = Path.GetFullPath(args[3]);

            var beforeAssembly = AssemblyDefinition.ReadAssembly(
                mergedAssemblyPath,
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


        }
    }
}
