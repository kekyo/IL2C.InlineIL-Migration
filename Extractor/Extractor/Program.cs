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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extractor
{
    class Program
    {
        static async Task Main(string[] args)
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
                Select(m => mergedMethods[m.FullName]).
                GroupBy(m => m.DeclaringType.Name).
                ToDictionary(g => g.Key, g => g.ToDictionary(m => m.Name));

            // Step 3: Read all source code.
            var sourceCodes = await Task.WhenAll(
                Directory.EnumerateFiles(
                    il2cSourceBasePath, "*.cs", SearchOption.AllDirectories).
                Select(async path =>
                {
                    var sourceCode = await File.ReadAllTextAsync(path, Encoding.UTF8);
                    var testCaseIndex = sourceCode.IndexOf("[TestCase(");
                    var testClassIndex = sourceCode.IndexOf("class ", testCaseIndex + 1);
                    var testClassNameStartIndex = testClassIndex + 6;
                    var testClassNameEndIndex = sourceCode.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }, testClassNameStartIndex);
                    var testClassName = sourceCode.Substring(testClassNameStartIndex, testClassNameEndIndex - testClassNameStartIndex);
                    var testClassBodyIndex = sourceCode.IndexOf('{', testClassNameEndIndex + 1);
                    return (relativePath: path.Substring(il2cSourceBasePath.Length + 1), testClassName, index: testClassBodyIndex + 1, sourceCode);
                }));

            // Step 4: Patch source code.
            var a = sourceCodes.Select(entry =>
            {
                var methods = targetMergedMethods[entry.testClassName];

                var forwardRefLine = "[MethodImpl(MethodImplOptions.ForwardRef)]";
                var staticExternWord = " static extern ";

                var sb = new StringBuilder(entry.sourceCode.Substring(0, entry.index));
                var index = entry.index;
                while (true)
                {
                    var forwardRefIndex = entry.sourceCode.IndexOf(forwardRefLine, index);
                    if (forwardRefIndex == -1)
                    {
                        break;
                    }

                    var externIndex = entry.sourceCode.IndexOf(staticExternWord, forwardRefIndex + forwardRefLine.Length);

                    var returnTypeEndIndex = entry.sourceCode.IndexOf(" ", externIndex + staticExternWord.Length);
                    var methodNameEndIndex = entry.sourceCode.IndexOf("(", returnTypeEndIndex + 1);

                    var methodName = entry.sourceCode.Substring(
                        returnTypeEndIndex, methodNameEndIndex - returnTypeEndIndex);

                    var signatureEndIndex = entry.sourceCode.IndexOf(");", returnTypeEndIndex + methodName.Length + 1);

                    var signatureHead = entry.sourceCode.Substring(
                        forwardRefIndex + forwardRefLine.Length, externIndex - forwardRefIndex - forwardRefLine.Length);
                    sb.Append(signatureHead);
                    sb.Append(" static ");

                    var signature = entry.sourceCode.Substring(
                        externIndex + staticExternWord.Length, signatureEndIndex - (externIndex + staticExternWord.Length));
                    sb.Append(signature);
                    sb.AppendLine(")");
                    sb.Append("        {");

                    var body = methods[methodName.Trim()].Body;

                    foreach (var instruction in body.Instructions)
                    {
                        sb.AppendFormat("            {0}({1});", instruction.OpCode, instruction.Operand);
                        sb.AppendLine();
                    }

                    sb.AppendLine("            return default;");
                    sb.Append("        }");

                    index = signatureEndIndex + 2;
                }

                sb.Append(entry.sourceCode.Substring(index));

                return sb.ToString();
            }).ToArray();
        }
    }
}
