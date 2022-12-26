using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using static Buckle.Utilities.FunctionUtilities;

namespace Buckle.CodeAnalysis.Emitting;

/// <summary>
/// Emits a bound program into a .NET assembly.
/// </summary>
internal sealed class Emitter {
    private readonly List<AssemblyDefinition> _assemblies = new List<AssemblyDefinition>();
    private readonly List<(TypeSymbol type, string metadataName)> _builtinTypes;
    private readonly Dictionary<FunctionSymbol, MethodDefinition> _methods =
        new Dictionary<FunctionSymbol, MethodDefinition>();
    private readonly AssemblyDefinition _assemblyDefinition;
    private readonly Dictionary<TypeSymbol, TypeReference> _knownTypes;
    private readonly Dictionary<VariableSymbol, VariableDefinition> _locals =
        new Dictionary<VariableSymbol, VariableDefinition>();
    private readonly List<(int instructionIndex, BoundLabel target)> _fixups =
        new List<(int instructionIndex, BoundLabel target)>();
    private readonly Dictionary<BoundLabel, int> _labels = new Dictionary<BoundLabel, int>();
    private readonly Dictionary<NetMethodReference, MethodReference> _methodReferences;
    private readonly TypeReference _randomReference;
    private readonly TypeReference _nullableReference;
    private TypeDefinition _typeDefinition;
    private FieldDefinition _randomFieldDefinition;
    private Stack<MethodDefinition> _methodStack = new Stack<MethodDefinition>();

    private Emitter(string moduleName, string[] references) {
        diagnostics = new BelteDiagnosticQueue();
        // ? Do not know why this code was here, should always be empty at this point
        // if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
        //     return;

        foreach (var reference in references) {
            try {
                var assembly = AssemblyDefinition.ReadAssembly(reference);
                _assemblies.Add(assembly);
            } catch (BadImageFormatException) {
                diagnostics.Push(Error.InvalidReference(reference));
            }
        }

        if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return;

        _builtinTypes = new List<(TypeSymbol type, string metadataName)>() {
            (TypeSymbol.Any, "System.Object"),
            (TypeSymbol.Bool, "System.Boolean"),
            (TypeSymbol.Int, "System.Int32"),
            (TypeSymbol.Decimal, "System.Double"),
            (TypeSymbol.String, "System.String"),
            (TypeSymbol.Void, "System.Void"),
        };

        var assemblyName = new AssemblyNameDefinition(moduleName, new Version(1, 0));
        _assemblyDefinition = AssemblyDefinition.CreateAssembly(assemblyName, moduleName, ModuleKind.Console);
        _knownTypes = new Dictionary<TypeSymbol, TypeReference>();

        foreach (var (typeSymbol, metadataName) in _builtinTypes) {
            var typeReference = ResolveType(typeSymbol.name, metadataName);
            _knownTypes.Add(typeSymbol, typeReference);
        }

        _methodReferences = new Dictionary<NetMethodReference, MethodReference>() {
            {
                NetMethodReference.ConsoleWrite,
                ResolveMethod("System.Console", "Write", new [] { "System.Object" })
            }, {
                NetMethodReference.ConsoleWriteLine,
                ResolveMethod("System.Console", "WriteLine", new [] { "System.Object" })
            }, {
                NetMethodReference.ConsoleReadLine,
                ResolveMethod("System.Console", "ReadLine", Array.Empty<string>())
            }, {
                NetMethodReference.StringConcat2,
                ResolveMethod("System.String", "Concat", new [] { "System.String", "System.String" })
            }, {
                NetMethodReference.StringConcat3,
                ResolveMethod("System.String", "Concat", new [] { "System.String", "System.String", "System.String" })
            }, {
                NetMethodReference.StringConcat4,
                ResolveMethod("System.String", "Concat",
                    new [] { "System.String", "System.String", "System.String", "System.String" })
            }, {
                NetMethodReference.StringConcatArray,
                ResolveMethod("System.String", "Concat", new [] { "System.String[]" })
            }, {
                NetMethodReference.ConvertToBoolean,
                ResolveMethod("System.Convert", "ToBoolean", new [] { "System.Object" })
            }, {
                NetMethodReference.ConvertToInt32,
                ResolveMethod("System.Convert", "ToInt32", new [] { "System.Object" })
            }, {
                NetMethodReference.ConvertToString,
                ResolveMethod("System.Convert", "ToString", new [] { "System.Object" })
            }, {
                NetMethodReference.ConvertToDouble,
                ResolveMethod("System.Convert", "ToDouble", new [] { "System.Object" })
            }, {
                NetMethodReference.ObjectEquals,
                ResolveMethod("System.Object", "Equals", new [] { "System.Object", "System.Object" })
            }, {
                NetMethodReference.RandomCtor,
                ResolveMethod("System.Random", ".ctor", Array.Empty<string>())
            }, {
                NetMethodReference.RandomNext,
                ResolveMethod("System.Random", "Next", new [] { "System.Int32" })
            }, {
                NetMethodReference.NullableCtor,
                ResolveMethod("System.Nullable`1", ".ctor", null)
            }, {
                NetMethodReference.NullableValue,
                ResolveMethod("System.Nullable`1", "get_Value", null)
            }, {
                NetMethodReference.NullableHasValue,
                ResolveMethod("System.Nullable`1", "get_HasValue", null)
            },
        };

        _randomReference = ResolveType(null, "System.Random");
        _nullableReference = ResolveType(null, "System.Nullable`1");
    }

    /// <summary>
    /// Diagnostics produced by <see cref="Emitter" />.
    /// These diagnostics are fatal, as all error checking has been done already.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    private enum NetMethodReference {
        ConsoleWrite,
        ConsoleWriteLine,
        ConsoleReadLine,
        StringConcat2,
        StringConcat3,
        StringConcat4,
        StringConcatArray,
        ConvertToBoolean,
        ConvertToInt32,
        ConvertToString,
        ConvertToDouble,
        ObjectEquals,
        RandomNext,
        RandomCtor,
        NullableCtor,
        NullableValue,
        NullableHasValue,
    }

    /// <summary>
    /// Emits a program to a .NET assembly.
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" /> to emit.</param>
    /// <param name="moduleName">Name of emitted assembly/application.</param>
    /// <param name="references">All external .NET references.</param>
    /// <param name="outputPath">Where to put the emitted assembly.</param>
    /// <returns>Diagnostics.</returns>
    internal static BelteDiagnosticQueue Emit(
        BoundProgram program, string moduleName, string[] references, string outputPath) {
        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return program.diagnostics;

        var emitter = new Emitter(moduleName, references);
        return emitter.Emit(program, outputPath);
    }

    /// <summary>
    /// Emits a program to a .NET assembly.
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" /> to emit.</param>
    /// <param name="outputPath">Where to put the emitted assembly.</param>
    /// <returns>Diagnostics.</returns>
    internal BelteDiagnosticQueue Emit(BoundProgram program, string outputPath) {
        diagnostics.Move(program.diagnostics);

        if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return diagnostics;

        var objectType = _knownTypes[TypeSymbol.Any];
        _typeDefinition = new TypeDefinition(
            "", "<Program>$", TypeAttributes.Abstract | TypeAttributes.Sealed, objectType);
        _assemblyDefinition.MainModule.Types.Add(_typeDefinition);

        foreach (var functionWithBody in program.functionBodies)
            EmitFunctionDeclaration(functionWithBody.Key);

        foreach (var functionWithBody in program.functionBodies) {
            // _currentFunction = functionWithBody.Key;
            EmitFunctionBody(functionWithBody.Key, functionWithBody.Value);
        }

        if (program.mainFunction != null)
            _assemblyDefinition.EntryPoint = LookupMethod(_methods, program.mainFunction);

        _assemblyDefinition.Write(outputPath);

        return diagnostics;
    }

    private TypeReference ResolveType(string buckleName, string metadataName) {
        var foundTypes = _assemblies.SelectMany(a => a.Modules)
            .SelectMany(m => m.Types)
            .Where(t => t.FullName == metadataName)
            .ToArray();

        if (foundTypes.Length == 1) {
            var typeReference = _assemblyDefinition.MainModule.ImportReference(foundTypes[0]);
            return typeReference;
        } else if (foundTypes.Length == 0)
            diagnostics.Push(Error.RequiredTypeNotFound(buckleName, metadataName));
        else
            diagnostics.Push(Error.RequiredTypeAmbiguous(buckleName, metadataName, foundTypes));

        return null;
    }

    private MethodReference ResolveMethod(
        string typeName, string methodName, string[] parameterTypeNames) {

        var foundTypes = _assemblies.SelectMany(a => a.Modules)
            .SelectMany(m => m.Types)
            .Where(t => t.FullName == typeName)
            .ToArray();

        if (foundTypes.Length == 1) {
            var foundType = foundTypes[0];
            var methods = foundType.Methods.Where(m => m.Name == methodName);

            if (methods.ToArray().Length == 1 && parameterTypeNames == null)
                return _assemblyDefinition.MainModule.ImportReference(methods.Single());

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

                return _assemblyDefinition.MainModule.ImportReference(method);
            }

            diagnostics.Push(Error.RequiredMethodNotFound(typeName, methodName, parameterTypeNames));
            return null;
        } else if (foundTypes.Length == 0)
            diagnostics.Push(Error.RequiredTypeNotFound(null, typeName));
        else
            diagnostics.Push(Error.RequiredTypeAmbiguous(null, typeName, foundTypes));

        return null;
    }

    private void EmitFunctionBody(FunctionSymbol function, BoundBlockStatement body) {
        var method = _methods[function];
        _locals.Clear();
        _labels.Clear();
        _fixups.Clear();
        var iLProcessor = method.Body.GetILProcessor();

        _methodStack.Push(method);

        foreach (var statement in body.statements)
            EmitStatement(iLProcessor, statement);

        _methodStack.Pop();

        foreach (var fixup in _fixups) {
            var targetLabel = fixup.target;
            var targetInstructionIndex = _labels[targetLabel];
            var targetInstruction = iLProcessor.Body.Instructions[targetInstructionIndex];
            var instructionFix = iLProcessor.Body.Instructions[fixup.instructionIndex];
            instructionFix.Operand = targetInstruction;
        }

        method.Body.OptimizeMacros();
    }

    private void EmitStatement(ILProcessor iLProcessor, BoundStatement statement) {
        switch (statement.kind) {
            case BoundNodeKind.NopStatement:
                EmitNopStatement(iLProcessor, (BoundNopStatement)statement);
                break;
            case BoundNodeKind.ExpressionStatement:
                // EmitExpressionStatement(iLProcessor, (BoundExpressionStatement)statement);
                break;
            case BoundNodeKind.VariableDeclarationStatement:
                // EmitVariableDeclarationStatement(iLProcessor, (BoundVariableDeclarationStatement)statement);
                break;
            case BoundNodeKind.GotoStatement:
                EmitGotoStatement(iLProcessor, (BoundGotoStatement)statement);
                break;
            case BoundNodeKind.LabelStatement:
                EmitLabelStatement(iLProcessor, (BoundLabelStatement)statement);
                break;
            case BoundNodeKind.ConditionalGotoStatement:
                // EmitConditionalGotoStatement(iLProcessor, (BoundConditionalGotoStatement)statement);
                break;
            case BoundNodeKind.ReturnStatement:
                // EmitReturnStatement(iLProcessor, (BoundReturnStatement)statement);
                break;
            case BoundNodeKind.TryStatement:
                EmitTryStatement(iLProcessor, (BoundTryStatement)statement);
                break;
            default:
                throw new BelteInternalException($"EmitStatement: unexpected node '{statement.kind}'");
        }
    }

    private void EmitTryStatement(ILProcessor iLProcessor, BoundTryStatement statement) {
        var method = _methodStack.Last();

        if (statement.catchBody == null) {
            var tryBody = statement.body.statements;
            var end = iLProcessor.Create(OpCodes.Nop);
            var tryStart = iLProcessor.Create(OpCodes.Nop);
            var tryEnd = iLProcessor.Create(OpCodes.Leave_S, end);

            var finallyBody = statement.finallyBody.statements;
            var handlerStart = iLProcessor.Create(OpCodes.Nop);
            var handlerEnd = iLProcessor.Create(OpCodes.Endfinally);

            iLProcessor.Append(tryStart);

            foreach (var node in tryBody)
                EmitStatement(iLProcessor, node);

            iLProcessor.Append(tryEnd);
            iLProcessor.Append(handlerStart);

            foreach (var node in finallyBody)
                EmitStatement(iLProcessor, node);

            iLProcessor.Append(handlerEnd);
            iLProcessor.Append(end);

            var handler = new ExceptionHandler(ExceptionHandlerType.Finally) {
                TryStart = tryStart,
                TryEnd = handlerStart,
                HandlerStart = handlerStart,
                HandlerEnd = end,
            };

            method.Body.ExceptionHandlers.Add(handler);
        } else if (statement.finallyBody == null) {
            var tryBody = statement.body.statements;
            var end = iLProcessor.Create(OpCodes.Nop);
            var tryStart = iLProcessor.Create(OpCodes.Nop);
            var tryEnd = iLProcessor.Create(OpCodes.Leave_S, end);

            var catchBody = statement.catchBody.statements;
            var handlerStart = iLProcessor.Create(OpCodes.Nop);
            var handlerEnd = iLProcessor.Create(OpCodes.Leave_S, end);

            iLProcessor.Append(tryStart);

            foreach (var node in tryBody)
                EmitStatement(iLProcessor, node);

            iLProcessor.Append(tryEnd);
            iLProcessor.Append(handlerStart);

            foreach (var node in catchBody)
                EmitStatement(iLProcessor, node);

            iLProcessor.Append(handlerEnd);
            iLProcessor.Append(end);

            var handler = new ExceptionHandler(ExceptionHandlerType.Catch) {
                TryStart = tryStart,
                TryEnd = handlerStart,
                HandlerStart = handlerStart,
                HandlerEnd = end,
                CatchType = _knownTypes[TypeSymbol.Any],
            };

            method.Body.ExceptionHandlers.Add(handler);
        } else {
            var innerTryBody = statement.body.statements;
            var end = iLProcessor.Create(OpCodes.Nop);
            var innerTryStart = iLProcessor.Create(OpCodes.Nop);
            var innerTryEnd = iLProcessor.Create(OpCodes.Leave_S, end);

            var innerCatchBody = statement.catchBody.statements;
            var innerHandlerStart = iLProcessor.Create(OpCodes.Nop);
            var innerHandlerEnd = iLProcessor.Create(OpCodes.Leave_S, end);

            var finallyBody = statement.finallyBody.statements;
            var finallyStart = iLProcessor.Create(OpCodes.Nop);
            var finallyEnd = iLProcessor.Create(OpCodes.Endfinally);

            iLProcessor.Append(innerTryStart);

            foreach (var node in innerTryBody)
                EmitStatement(iLProcessor, node);

            iLProcessor.Append(innerTryEnd);
            iLProcessor.Append(innerHandlerStart);

            foreach (var node in innerCatchBody)
                EmitStatement(iLProcessor, node);

            iLProcessor.Append(innerHandlerEnd);
            iLProcessor.Append(finallyStart);

            var innerHandler = new ExceptionHandler(ExceptionHandlerType.Catch) {
                TryStart = innerTryStart,
                TryEnd = innerHandlerStart,
                HandlerStart = innerHandlerStart,
                HandlerEnd = finallyStart,
                CatchType = _knownTypes[TypeSymbol.Any],
            };

            foreach (var node in finallyBody)
                EmitStatement(iLProcessor, node);

            iLProcessor.Append(finallyEnd);
            iLProcessor.Append(end);

            var handler = new ExceptionHandler(ExceptionHandlerType.Finally) {
                TryStart = innerTryStart,
                TryEnd = finallyStart,
                HandlerStart = finallyStart,
                HandlerEnd = end,
            };

            method.Body.ExceptionHandlers.Add(innerHandler);
            method.Body.ExceptionHandlers.Add(handler);
        }
    }

    private void EmitNopStatement(ILProcessor iLProcessor, BoundNopStatement statement) {
        iLProcessor.Emit(OpCodes.Nop);
    }

    private void EmitLabelStatement(ILProcessor iLProcessor, BoundLabelStatement statement) {
        _labels.Add(statement.label, iLProcessor.Body.Instructions.Count);
    }

    private void EmitGotoStatement(ILProcessor iLProcessor, BoundGotoStatement statement) {
        _fixups.Add((iLProcessor.Body.Instructions.Count, statement.label));
        iLProcessor.Emit(OpCodes.Br, Instruction.Create(OpCodes.Nop));
    }

    private void EmitRandomField() {
        _randomFieldDefinition = new FieldDefinition(
                                "$randInt", FieldAttributes.Static | FieldAttributes.Private, _randomReference);
        _typeDefinition.Fields.Add(_randomFieldDefinition);
        var staticConstructor = new MethodDefinition(
            ".cctor",
            MethodAttributes.Static | MethodAttributes.Private |
            MethodAttributes.RTSpecialName | MethodAttributes.SpecialName,
            _knownTypes[TypeSymbol.Void]
        );
        _typeDefinition.Methods.Insert(0, staticConstructor);

        var iLProcessor = staticConstructor.Body.GetILProcessor();
        iLProcessor.Emit(OpCodes.Newobj, _methodReferences[NetMethodReference.RandomCtor]);
        iLProcessor.Emit(OpCodes.Stsfld, _randomFieldDefinition);
        iLProcessor.Emit(OpCodes.Ret);
    }

    private void EmitEmptyExpression(ILProcessor iLProcessor, BoundEmptyExpression expression) {
        // TODO Breaks control flow, debug why this does not work
        // iLProcessor.Emit(OpCodes.Nop);
    }

    private MethodReference GetNullableCtor(BoundType type) {
        var genericArgumentType = _assemblyDefinition.MainModule.ImportReference(_knownTypes[type.typeSymbol]);
        var methodReference =
            _assemblyDefinition.MainModule.ImportReference(_methodReferences[NetMethodReference.NullableCtor]);

        methodReference.DeclaringType = new GenericInstanceType(_nullableReference);
        (methodReference.DeclaringType as GenericInstanceType).GenericArguments.Add(genericArgumentType);
        methodReference.Resolve();

        return methodReference;
    }

    private MethodReference GetNullableValue(BoundType type) {
        var genericArgumentType = _assemblyDefinition.MainModule.ImportReference(_knownTypes[type.typeSymbol]);
        var methodReference =
            _assemblyDefinition.MainModule.ImportReference(_methodReferences[NetMethodReference.NullableValue]);

        methodReference.DeclaringType = new GenericInstanceType(_nullableReference);
        (methodReference.DeclaringType as GenericInstanceType).GenericArguments.Add(genericArgumentType);
        methodReference.Resolve();

        return methodReference;
    }

    private MethodReference GetNullableHasValue(BoundType type) {
        var genericArgumentType = _assemblyDefinition.MainModule.ImportReference(_knownTypes[type.typeSymbol]);
        var methodReference =
            _assemblyDefinition.MainModule.ImportReference(_methodReferences[NetMethodReference.NullableHasValue]);

        methodReference.DeclaringType = new GenericInstanceType(_nullableReference);
        (methodReference.DeclaringType as GenericInstanceType).GenericArguments.Add(genericArgumentType);
        methodReference.Resolve();

        return methodReference;
    }

    private MethodReference GetConvertTo(BoundType from, BoundType to, bool isImplicit) {
        if (!from.isNullable || isImplicit) {
            if (to.typeSymbol == TypeSymbol.Any)
                return null;
            else if (to.typeSymbol == TypeSymbol.Bool)
                return _methodReferences[NetMethodReference.ConvertToBoolean];
            else if (to.typeSymbol == TypeSymbol.Int)
                return _methodReferences[NetMethodReference.ConvertToInt32];
            else if (to.typeSymbol == TypeSymbol.String)
                return _methodReferences[NetMethodReference.ConvertToString];
            else if (to.typeSymbol == TypeSymbol.Decimal)
                return _methodReferences[NetMethodReference.ConvertToDouble];
            else
                throw new BelteInternalException($"GetConvertTo: unexpected cast from '{from}' to '{to}'");
        }

        throw new BelteInternalException("GetConvertTo: cannot convert nullable types");
    }

    private void EmitFunctionDeclaration(FunctionSymbol function) {
        var functionType = GetType(function.type);
        var method = new MethodDefinition(
            function.name, MethodAttributes.Static | MethodAttributes.Private, functionType);

        foreach (var parameter in function.parameters) {
            var parameterType = GetType(parameter.type);
            var parameterAttributes = ParameterAttributes.None;
            var parameterDefinition = new ParameterDefinition(parameter.name, parameterAttributes, parameterType);
            method.Parameters.Add(parameterDefinition);
        }

        _typeDefinition.Methods.Add(method);
        _methods.Add(function, method);
    }

    private TypeReference GetType(
        BoundType type, bool overrideNullability = false, bool ignoreReference = false) {
        if ((type.dimensions == 0 && !type.isNullable && !overrideNullability) ||
            type.typeSymbol == TypeSymbol.Void)
            return _knownTypes[type.typeSymbol];

        var genericArgumentType = _assemblyDefinition.MainModule.ImportReference(_knownTypes[type.typeSymbol]);
        var typeReference = new GenericInstanceType(_nullableReference);
        typeReference.GenericArguments.Add(genericArgumentType);
        var referenceType = new ByReferenceType(typeReference);

        if (type.dimensions == 0) {
            if (type.isReference && !ignoreReference) {
                referenceType.Resolve();
                return referenceType;
            } else {
                typeReference.Resolve();
                return typeReference;
            }
        } else {
            ArrayType arrayType;

            if (type.isReference && !ignoreReference)
                arrayType = referenceType.MakeArrayType(type.dimensions);
            else
                arrayType = typeReference.MakeArrayType(type.dimensions);

            arrayType.Resolve();
            return arrayType;
        }
    }
}
