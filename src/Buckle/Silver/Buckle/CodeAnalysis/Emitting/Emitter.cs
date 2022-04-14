using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Buckle.CodeAnalysis.Emitting {
    internal sealed class Emitter {
        public DiagnosticQueue diagnostics = new DiagnosticQueue();
        private readonly AssemblyDefinition assemblyDefinition_;
        private readonly Dictionary<TypeSymbol, TypeReference> knownTypes_;
        private readonly MethodReference consoleWriteLineReference_;

        private Emitter(string moduleName, string[] references) {
            var assemblies = new List<AssemblyDefinition>();

            if (diagnostics.Any())
                return;

            foreach (var reference in references) {
                try {
                    var assembly = AssemblyDefinition.ReadAssembly(reference);
                    assemblies.Add(assembly);
                } catch (BadImageFormatException) {
                    diagnostics.Push(Error.InvalidReference(reference));
                }
            }

            if (diagnostics.Any())
                return;

            var builtinTypes = new List<(TypeSymbol type, string metadataName)>() {
                (TypeSymbol.Any, "System.Object"),
                (TypeSymbol.Bool, "System.Boolean"),
                (TypeSymbol.Int, "System.Int32"),
                (TypeSymbol.String, "System.String"),
                (TypeSymbol.Void, "System.Void"),
            };

            var assemblyName = new AssemblyNameDefinition(moduleName, new Version(0, 1));
            assemblyDefinition_ = AssemblyDefinition.CreateAssembly(assemblyName, moduleName, ModuleKind.Console);
            knownTypes_ = new Dictionary<TypeSymbol, TypeReference>();

            foreach (var (typeSymbol, metadataName) in builtinTypes) {
                var typeReference = ResolveType(typeSymbol.name, metadataName);
                knownTypes_.Add(typeSymbol, typeReference);
            }

            TypeReference ResolveType(string buckleName, string metadataName) {
                var foundTypes = assemblies.SelectMany(a => a.Modules)
                    .SelectMany(m => m.Types)
                    .Where(t => t.FullName == metadataName)
                    .ToArray();

                if (foundTypes.Length == 1) {
                    var typeReference = assemblyDefinition_.MainModule.ImportReference(foundTypes[0]);
                    return typeReference;
                } else if (foundTypes.Length == 0)
                    diagnostics.Push(Error.RequiredTypeNotFound(buckleName, metadataName));
                else
                    diagnostics.Push(Error.RequiredTypeAmbiguous(buckleName, metadataName, foundTypes));

                return null;
            }

            MethodReference ResolveMethod(string typeName, string methodName, string[] parameterTypeNames) {
                var foundTypes = assemblies.SelectMany(a => a.Modules)
                    .SelectMany(m => m.Types)
                    .Where(t => t.FullName == typeName)
                    .ToArray();

                if (foundTypes.Length == 1) {
                    var foundType = foundTypes[0];
                    var methods = foundType.Methods.Where(m => m.Name == methodName);

                    foreach (var method in methods) {
                        if (method.Parameters.Count != parameterTypeNames.Length)
                            continue;

                        var allParametersMatch = true;

                        for (int i=0; i<parameterTypeNames.Length; i++) {
                            if (method.Parameters[i].ParameterType.FullName != parameterTypeNames[i]) {
                                allParametersMatch = false;
                                break;
                            }
                        }

                        if (!allParametersMatch)
                            continue;

                        return assemblyDefinition_.MainModule.ImportReference(method);
                    }

                    diagnostics.Push(Error.RequiredMethodNotFound(typeName, methodName, parameterTypeNames));
                    return null;
                } else if (foundTypes.Length == 0)
                    diagnostics.Push(Error.RequiredTypeNotFound(null, typeName));
                else
                    diagnostics.Push(Error.RequiredTypeAmbiguous(null, typeName, foundTypes));

                return null;
            }

            consoleWriteLineReference_ = ResolveMethod("System.Console", "WriteLine", new [] {"System.String"});
        }

        public DiagnosticQueue Emit(BoundProgram program, string outputPath) {
            if (diagnostics.Any())
                return diagnostics;

            var objectType = knownTypes_[TypeSymbol.Any];
            var typeDefinition = new TypeDefinition(
                "", "Program", TypeAttributes.Abstract | TypeAttributes.Sealed, objectType);
            assemblyDefinition_.MainModule.Types.Add(typeDefinition);

            var voidType = knownTypes_[TypeSymbol.Void];
            var mainMethod = new MethodDefinition("Main", MethodAttributes.Static | MethodAttributes.Private, voidType);
            typeDefinition.Methods.Add(mainMethod);

            var ilProcessor = mainMethod.Body.GetILProcessor();
            ilProcessor.Emit(OpCodes.Ldstr, "Hello world from BELTE-Buckle!");
            ilProcessor.Emit(OpCodes.Call, consoleWriteLineReference_);
            ilProcessor.Emit(OpCodes.Ret);

            assemblyDefinition_.EntryPoint = mainMethod;
            assemblyDefinition_.Write(outputPath);

            return diagnostics;
        }

        public static DiagnosticQueue Emit(
            BoundProgram program, string moduleName, string[] references, string outputPath) {
            DiagnosticQueue diagnostics = new DiagnosticQueue();
            diagnostics.Move(program.diagnostics);

            var emitter = new Emitter(moduleName, references);
            return emitter.Emit(program, outputPath);
        }
    }
}
