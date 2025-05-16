using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter {
    private readonly Dictionary<SpecialType, TypeReference> _knownTypes;
    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly AssemblyDefinition _assemblyDefinition;
    private readonly List<AssemblyDefinition> _assemblies;

    private ILEmitter(string moduleName, string[] references, BelteDiagnosticQueue diagnostics) {
        _diagnostics = diagnostics;

        _assemblies = [
            AssemblyDefinition.ReadAssembly(typeof(object).Assembly.Location), // System.Private.CoreLib
            AssemblyDefinition.ReadAssembly(typeof(Console).Assembly.Location), // System.Console
            AssemblyDefinition.ReadAssembly(typeof(List<>).Assembly.Location), // System.Collections.Generic
        ];

        foreach (var reference in references) {
            try {
                var assembly = AssemblyDefinition.ReadAssembly(reference);
                _assemblies.Add(assembly);
            } catch (BadImageFormatException) {
                _diagnostics.Push(Error.InvalidReference(reference));
            }
        }

        var builtInTypes = new List<(SpecialType type, string metadataName)>() {
            (SpecialType.Object, "System.Object"),
            (SpecialType.Bool, "System.Boolean"),
            (SpecialType.Int, "System.Int64"),
            (SpecialType.String, "System.String"),
            (SpecialType.Decimal, "System.Double"),
            (SpecialType.Nullable, "System.Nullable`1"),
            (SpecialType.Void, "System.Void"),
        };

        var assemblyName = new AssemblyNameDefinition(moduleName, new Version(1, 0));
        _assemblyDefinition = AssemblyDefinition.CreateAssembly(assemblyName, moduleName, ModuleKind.Console);
        _knownTypes = [];

        ResolveMethods();
    }

    internal static void Emit(
        BoundProgram program,
        string moduleName,
        string[] references,
        string outputPath,
        BelteDiagnosticQueue diagnostics) {
        var emitter = new ILEmitter(moduleName, references, diagnostics);
        //     var emitter = new ILEmitter(moduleName, references);
        //     return emitter.EmitToString(program, out diagnostics);
    }

    internal static string EmitToString(
        BoundProgram program,
        string moduleName,
        string[] references,
        BelteDiagnosticQueue diagnostics) {
        //     var emitter = new ILEmitter(moduleName, references);
        //     return emitter.EmitToString(program, out diagnostics);
        return "";
    }

    private TypeReference ResolveType(string name, string metadataName) {
        var foundTypes = _assemblies
            .SelectMany(a => a.Modules)
            .SelectMany(m => m.Types)
            .Where(t => t.FullName == metadataName)
            .ToArray();

        if (foundTypes.Length == 1) {
            return _assemblyDefinition.MainModule.ImportReference(foundTypes[0]);
        } else if (foundTypes.Length == 0) {
            throw new BelteInternalException($"Required type not found: {name} ({metadataName})");
        } else {
            throw new BelteInternalException(
                $"Required type ambiguous: {name} ({metadataName}); found {foundTypes.Length} candidates"
            );
        }
    }

    private MethodReference ResolveMethod(string typeName, string methodName, string[] parameterTypeNames) {
        var foundTypes = _assemblies
            .SelectMany(a => a.Modules)
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

                for (var i = 0; i < parameterTypeNames.Length; i++) {
                    if (method.Parameters[i].ParameterType.FullName != parameterTypeNames[i]) {
                        allParametersMatch = false;
                        break;
                    }
                }

                if (!allParametersMatch)
                    continue;

                return _assemblyDefinition.MainModule.ImportReference(method);
            }

            throw new BelteInternalException(
                $"Required method not found: {typeName} {methodName} {parameterTypeNames.Length}"
            );
        } else if (foundTypes.Length == 0) {
            throw new BelteInternalException($"Required type not found: {typeName}");
        } else {
            throw new BelteInternalException(
                $"Required type not found: {typeName}; found {foundTypes.Length} candidates"
            );
        }
    }

    private void ResolveMethods() {
        NetMethodReference.Object_Equals_OO = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.Console_Write_O = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.ObjectEquals = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.ObjectEquals = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.ObjectEquals = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.ObjectEquals = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.ObjectEquals = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.ObjectEquals = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.ObjectEquals = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.ObjectEquals = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.ObjectEquals = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.ObjectEquals = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.ObjectEquals = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
        NetMethodReference.ObjectEquals = ResolveMethod("System.Object", "Equals", ["System.Object", "System.Object"]);
    }
}
