using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Buckle.CodeAnalysis.Emitting {
    internal static class Emitter {
        internal static DiagnosticQueue Emit(
            BoundProgram program, string moduleName, string[] references, string outputPath) {
            var diagnostics = new DiagnosticQueue();
            diagnostics.Move(program.diagnostics);

            if (diagnostics.Any())
                return diagnostics;

            var assemblies = new List<AssemblyDefinition>();

            foreach (var reference in references) {
                try {
                    var assembly = AssemblyDefinition.ReadAssembly(reference);
                    assemblies.Add(assembly);
                } catch (BadImageFormatException) {
                    diagnostics.Push(Error.InvalidReference(reference));
                }
            }

            if (diagnostics.Any())
                return diagnostics;

            // any      -> System.Object
            // int      -> System.Int32
            // string   -> System.String
            // bool     -> System.Bool
            // void     -> System.Void

            var builtinTypes = new List<(TypeSymbol type, string metadataName)>() {
                (TypeSymbol.Any, "System.Object"),
                (TypeSymbol.Bool, "System.Boolean"),
                (TypeSymbol.Int, "System.Int32"),
                (TypeSymbol.String, "System.String"),
                (TypeSymbol.Void, "System.Void"),
            };

            var assemblyName = new AssemblyNameDefinition(moduleName, new Version(0, 1));
            var assemblyDefinition = AssemblyDefinition.CreateAssembly(assemblyName, moduleName, ModuleKind.Console);
            var knownTypes = new Dictionary<TypeSymbol, TypeReference>();

            foreach (var (typeSymbol, metadataName) in builtinTypes) {
                var foundTypes = assemblies.SelectMany(a => a.Modules)
                    .SelectMany(m => m.Types)
                    .Where(t => t.FullName == metadataName)
                    .ToArray();

                if (foundTypes.Length == 1) {
                    var typeReference = assemblyDefinition.MainModule.ImportReference(foundTypes[0]);
                    knownTypes.Add(typeSymbol, typeReference);
                } else if (foundTypes.Length == 0)
                    diagnostics.Push(Error.BuiltinTypeNotFound(typeSymbol));
                else
                    diagnostics.Push(Error.BuiltinTypeAmbiguous(typeSymbol, foundTypes));
            }

            /*
            static class Program {
                void Main() {
                    System.Console.WriteLine("Hello, world!");
                }
            }
            */

            var objectType = knownTypes[TypeSymbol.Any];
            var typeDefinition = new TypeDefinition(
                "", "Program", TypeAttributes.Abstract | TypeAttributes.Sealed, objectType);
            assemblyDefinition.MainModule.Types.Add(typeDefinition);

            var voidType = knownTypes[TypeSymbol.Void];
            var mainMethod = new MethodDefinition("Main", MethodAttributes.Static | MethodAttributes.Private, voidType);
            typeDefinition.Methods.Add(mainMethod);

            var ilProcessor = mainMethod.Body.GetILProcessor();
            ilProcessor.Emit(OpCodes.Ret);

            assemblyDefinition.EntryPoint = mainMethod;
            assemblyDefinition.Write(outputPath);
            return diagnostics;
        }
    }
}
