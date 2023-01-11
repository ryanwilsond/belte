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

// TODO This entire file is spaghetti code, need to rewrite with a better understanding of when to use:
// ldarg vs ldarga vs ldarga.s, newobj vs initobj vs call

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class _ILEmitter {
    internal BelteDiagnosticQueue diagnostics = new BelteDiagnosticQueue();

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
    private bool _useNullRef = false;
    private FunctionSymbol _currentFunction;

    private enum _NetMethodReference {
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
        ConvertToSingle,
        ObjectEquals,
        RandomNext,
        RandomCtor,
        NullableCtor,
        NullableValue,
        NullableHasValue,
    }

    private readonly Dictionary<_NetMethodReference, MethodReference> _methodReferences;

    private readonly TypeReference _randomReference;
    private readonly TypeReference _nullableReference;

    private TypeDefinition _typeDefinition;
    private FieldDefinition _randomFieldDefinition;

    private _ILEmitter(string moduleName, string[] references) {
        if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return;

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
            (TypeSymbol.Decimal, "System.Single"),
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

        _methodReferences = new Dictionary<_NetMethodReference, MethodReference>() {
            {
                _NetMethodReference.ConsoleWrite,
                ResolveMethod("System.Console", "Write", new [] { "System.Object" })
            }, {
                _NetMethodReference.ConsoleWriteLine,
                ResolveMethod("System.Console", "WriteLine", new [] { "System.Object" })
            }, {
                _NetMethodReference.ConsoleReadLine,
                ResolveMethod("System.Console", "ReadLine", Array.Empty<string>())
            }, {
                _NetMethodReference.StringConcat2,
                ResolveMethod("System.String", "Concat", new [] { "System.String", "System.String" })
            }, {
                _NetMethodReference.StringConcat3,
                ResolveMethod("System.String", "Concat", new [] { "System.String", "System.String", "System.String" })
            }, {
                _NetMethodReference.StringConcat4,
                ResolveMethod("System.String", "Concat",
                    new [] { "System.String", "System.String", "System.String", "System.String" })
            }, {
                _NetMethodReference.StringConcatArray,
                ResolveMethod("System.String", "Concat", new [] { "System.String[]" })
            }, {
                _NetMethodReference.ConvertToBoolean,
                ResolveMethod("System.Convert", "ToBoolean", new [] { "System.Object" })
            }, {
                _NetMethodReference.ConvertToInt32,
                ResolveMethod("System.Convert", "ToInt32", new [] { "System.Object" })
            }, {
                _NetMethodReference.ConvertToString,
                ResolveMethod("System.Convert", "ToString", new [] { "System.Object" })
            }, {
                _NetMethodReference.ConvertToSingle,
                ResolveMethod("System.Convert", "ToSingle", new [] { "System.Object" })
            }, {
                _NetMethodReference.ObjectEquals,
                ResolveMethod("System.Object", "Equals", new [] { "System.Object", "System.Object" })
            }, {
                _NetMethodReference.RandomCtor,
                ResolveMethod("System.Random", ".ctor", Array.Empty<string>())
            }, {
                _NetMethodReference.RandomNext,
                ResolveMethod("System.Random", "Next", new [] { "System.Int32" })
            }, {
                _NetMethodReference.NullableCtor,
                ResolveMethod("System.Nullable`1", ".ctor", null)
            }, {
                _NetMethodReference.NullableValue,
                ResolveMethod("System.Nullable`1", "get_Value", null)
            }, {
                _NetMethodReference.NullableHasValue,
                ResolveMethod("System.Nullable`1", "get_HasValue", null)
            },
        };

        _randomReference = ResolveType(null, "System.Random");
        _nullableReference = ResolveType(null, "System.Nullable`1");
    }

    private TypeReference ResolveType(string buckleName, string metadataName) {
        var foundTypes = _assemblies.SelectMany(a => a.Modules)
            .SelectMany(m => m.Types)
            .Where(t => t.FullName == metadataName)
            .ToArray();

        if (foundTypes.Length == 1) {
            var typeReference = _assemblyDefinition.MainModule.ImportReference(foundTypes[0]);
            return typeReference;
        } else if (foundTypes.Length == 0) {
            ThrowRequiredTypeNotFound(buckleName, metadataName);
        } else {
            ThrowRequiredTypeAmbiguous(buckleName, metadataName, foundTypes);
        }

        // Unreachable
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

            ThrowRequiredMethodNotFound(typeName, methodName, parameterTypeNames);
        } else if (foundTypes.Length == 0) {
            ThrowRequiredTypeNotFound(null, typeName);
        } else {
            ThrowRequiredTypeAmbiguous(null, typeName, foundTypes);
        }

        // Unreachable
        return null;
    }

    private void ThrowRequiredMethodNotFound(string typeName, object methodName, string[] parameterTypeNames) {
        string message;

        if (parameterTypeNames == null) {
            message = $"could not resolve method '{typeName}.{methodName}' with the given references";
        } else {
            var parameterList = string.Join(", ", parameterTypeNames);
            message =
                $"could not resolve method '{typeName}.{methodName}({parameterList})' with the given references";
        }

        throw new BelteInternalException($"ThrowRequiredMethodNotFound: {message}");
    }

    private void ThrowRequiredTypeNotFound(string buckleName, string metadataName) {
        var message = buckleName != null
            ? $"could not resolve type '{buckleName}' ('{metadataName}') with the given references"
            : $"could not resolve type '{metadataName}' with the given references";

        throw new BelteInternalException($"ThrowRequiredTypeNotFound: {message}");
    }

    private void ThrowRequiredTypeAmbiguous(string buckleName, string metadataName, TypeDefinition[] foundTypes) {
        var assemblyNames = foundTypes.Select(t => t.Module.Assembly.Name.Name);
        var nameList = string.Join(", ", assemblyNames);

        var message = buckleName != null
            ? $"could not resolve type '{buckleName}' ('{metadataName}') with the given references"
            : $"could not resolve type '{metadataName}' with the given references";

        throw new BelteInternalException($"ThrowRequiredTypeAmbiguous: {message}");
    }

    internal static BelteDiagnosticQueue Emit(
        BoundProgram program, string moduleName, string[] references, string outputPath) {
        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return program.diagnostics;

        var _Emitter = new _ILEmitter(moduleName, references);
        return _Emitter.Emit(program, outputPath);
    }

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
            _currentFunction = functionWithBody.Key;
            EmitFunctionBody(functionWithBody.Key, functionWithBody.Value);
        }

        if (program.mainFunction != null)
            _assemblyDefinition.EntryPoint = LookupMethod(_methods, program.mainFunction);

        _assemblyDefinition.Write(outputPath);

        return diagnostics;
    }

    private void EmitFunctionBody(FunctionSymbol function, BoundBlockStatement body) {
        var method = _methods[function];
        _locals.Clear();
        _labels.Clear();
        _fixups.Clear();
        var iLProcessor = method.Body.GetILProcessor();

        foreach (var statement in body.statements)
            EmitStatement(iLProcessor, statement, method);

        foreach (var fixup in _fixups) {
            var targetLabel = fixup.target;
            var targetInstructionIndex = _labels[targetLabel];
            var targetInstruction = iLProcessor.Body.Instructions[targetInstructionIndex];
            var instructionFix = iLProcessor.Body.Instructions[fixup.instructionIndex];
            instructionFix.Operand = targetInstruction;
        }

        method.Body.OptimizeMacros();
    }

    private void EmitStatement(ILProcessor iLProcessor, BoundStatement statement, MethodDefinition method) {
        switch (statement.kind) {
            case BoundNodeKind.NopStatement:
                EmitNopStatement(iLProcessor, (BoundNopStatement)statement);
                break;
            case BoundNodeKind.ExpressionStatement:
                EmitExpressionStatement(iLProcessor, (BoundExpressionStatement)statement);
                break;
            case BoundNodeKind.VariableDeclarationStatement:
                EmitVariableDeclarationStatement(iLProcessor, (BoundVariableDeclarationStatement)statement);
                break;
            case BoundNodeKind.GotoStatement:
                EmitGotoStatement(iLProcessor, (BoundGotoStatement)statement);
                break;
            case BoundNodeKind.LabelStatement:
                EmitLabelStatement(iLProcessor, (BoundLabelStatement)statement);
                break;
            case BoundNodeKind.ConditionalGotoStatement:
                EmitConditionalGotoStatement(iLProcessor, (BoundConditionalGotoStatement)statement);
                break;
            case BoundNodeKind.ReturnStatement:
                EmitReturnStatement(iLProcessor, (BoundReturnStatement)statement);
                break;
            case BoundNodeKind.TryStatement:
                EmitTryStatement(iLProcessor, (BoundTryStatement)statement, method);
                break;
            default:
                throw new BelteInternalException($"EmitStatement: unexpected node '{statement.kind}'");
        }
    }

    private void EmitTryStatement(ILProcessor iLProcessor, BoundTryStatement statement, MethodDefinition method) {
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
                EmitStatement(iLProcessor, node, method);

            iLProcessor.Append(tryEnd);
            iLProcessor.Append(handlerStart);

            foreach (var node in finallyBody)
                EmitStatement(iLProcessor, node, method);

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
                EmitStatement(iLProcessor, node, method);

            iLProcessor.Append(tryEnd);
            iLProcessor.Append(handlerStart);

            foreach (var node in catchBody)
                EmitStatement(iLProcessor, node, method);

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
                EmitStatement(iLProcessor, node, method);

            iLProcessor.Append(innerTryEnd);
            iLProcessor.Append(innerHandlerStart);

            foreach (var node in innerCatchBody)
                EmitStatement(iLProcessor, node, method);

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
                EmitStatement(iLProcessor, node, method);

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

    private void EmitReturnStatement(ILProcessor iLProcessor, BoundReturnStatement statement) {
        if (statement.expression != null)
            EmitExpression(iLProcessor, statement.expression, nullable: statement.expression.type.isNullable);

        iLProcessor.Emit(OpCodes.Ret);
    }

    private void EmitConditionalGotoStatement(ILProcessor iLProcessor, BoundConditionalGotoStatement statement) {
        EmitExpression(iLProcessor, statement.condition);

        var opcode = statement.jumpIfTrue
            ? OpCodes.Brtrue
            : OpCodes.Brfalse;
        _fixups.Add((iLProcessor.Body.Instructions.Count, statement.label));
        iLProcessor.Emit(opcode, Instruction.Create(OpCodes.Nop));
    }

    private void EmitLabelStatement(ILProcessor iLProcessor, BoundLabelStatement statement) {
        _labels.Add(statement.label, iLProcessor.Body.Instructions.Count);
    }

    private void EmitGotoStatement(ILProcessor iLProcessor, BoundGotoStatement statement) {
        _fixups.Add((iLProcessor.Body.Instructions.Count, statement.label));
        iLProcessor.Emit(OpCodes.Br, Instruction.Create(OpCodes.Nop));
    }

    private void EmitVariableDeclarationStatement(
        ILProcessor iLProcessor, BoundVariableDeclarationStatement statement) {
        var typeReference = GetType(statement.variable.type);
        var variableDefinition = new VariableDefinition(typeReference);
        _locals.Add(statement.variable, variableDefinition);
        iLProcessor.Body.Variables.Add(variableDefinition);

        if (statement.variable.type.isReference) {
            if (statement.variable is ParameterSymbol parameter) {
                iLProcessor.Emit(OpCodes.Ldarga_S, parameter.ordinal);
            } else {
                var referenceVariable = _locals[((BoundReferenceExpression)statement.initializer).variable];
                iLProcessor.Emit(OpCodes.Ldloca_S, referenceVariable);
            }

            iLProcessor.Emit(OpCodes.Stloc, variableDefinition);
            return;
        }

        if (statement.variable.type.dimensions == 0 && statement.variable.type.isNullable &&
            statement.initializer.kind != BoundNodeKind.CallExpression)
            iLProcessor.Emit(OpCodes.Ldloca_S, variableDefinition);

        EmitExpression(
            iLProcessor, statement.initializer, nullable: statement.variable.type.isNullable, stack: false);

        if (statement.variable.type.dimensions > 0 || !statement.variable.type.isNullable ||
            statement.initializer.kind == BoundNodeKind.CallExpression)
            iLProcessor.Emit(OpCodes.Stloc, variableDefinition);
    }

    private void EmitExpressionStatement(ILProcessor iLProcessor, BoundExpressionStatement statement) {
        EmitExpression(iLProcessor, statement.expression);

        if (statement.expression.type?.typeSymbol != TypeSymbol.Void && !_useNullRef)
            iLProcessor.Emit(OpCodes.Pop);

        _useNullRef = false;
    }

    private void EmitExpression(
        ILProcessor iLProcessor, BoundExpression expression, bool referenceAssign = false,
        bool nullable = true, bool stack = true, bool handleAssignment = true) {
        if (expression.constantValue != null) {
            EmitConstantExpression(iLProcessor, expression, referenceAssign, nullable, stack, handleAssignment);
            return;
        }

        switch (expression.kind) {
            case BoundNodeKind.LiteralExpression:
                if (expression is BoundInitializerListExpression il) {
                    EmitInitializerListExpression(iLProcessor, il);
                    break;
                } else {
                    goto default;
                }
            case BoundNodeKind.UnaryExpression:
                EmitUnaryExpression(iLProcessor, (BoundUnaryExpression)expression);
                break;
            case BoundNodeKind.BinaryExpression:
                EmitBinaryExpression(iLProcessor, (BoundBinaryExpression)expression);
                break;
            case BoundNodeKind.VariableExpression:
                EmitVariableExpression(iLProcessor, (BoundVariableExpression)expression, nullable);
                break;
            case BoundNodeKind.AssignmentExpression:
                EmitAssignmentExpression(iLProcessor, (BoundAssignmentExpression)expression);
                break;
            case BoundNodeKind.EmptyExpression:
                EmitEmptyExpression(iLProcessor, (BoundEmptyExpression)expression);
                break;
            case BoundNodeKind.CallExpression:
                EmitCallExpression(iLProcessor, (BoundCallExpression)expression);
                break;
            case BoundNodeKind.IndexExpression:
                EmitIndexExpression(iLProcessor, (BoundIndexExpression)expression);
                break;
            case BoundNodeKind.CastExpression:
                EmitCastExpression(iLProcessor, (BoundCastExpression)expression);
                break;
            default:
                throw new BelteInternalException($"EmitExpression: unexpected node '{expression.kind}'");
        }
    }

    private void EmitIndexExpression(ILProcessor iLProcessor, BoundIndexExpression expression) {
        EmitExpression(iLProcessor, expression.operand);
        iLProcessor.Emit(OpCodes.Ldc_I4, (int)expression.index.constantValue.value);

        var type = expression.operand.type;

        if (type.ChildType().dimensions == 0) {
            iLProcessor.Emit(OpCodes.Ldelem_Any, GetType(type.BaseType()));
        } else {
            iLProcessor.Emit(OpCodes.Ldelem_Ref);
        }
    }

    private void EmitInitializerListExpression(ILProcessor iLProcessor, BoundInitializerListExpression expression) {
        iLProcessor.Emit(OpCodes.Ldc_I4, expression.items.Length);
        iLProcessor.Emit(OpCodes.Newarr, GetType(expression.type.ChildType()));

        for (int i=0; i<expression.items.Length; i++) {
            var item = expression.items[i];
            iLProcessor.Emit(OpCodes.Dup);
            iLProcessor.Emit(OpCodes.Ldc_I4, i);
            EmitExpression(iLProcessor, item);

            if (item.type.dimensions == 0) {
                iLProcessor.Emit(OpCodes.Newobj, GetNullableCtor(item.type));
                iLProcessor.Emit(OpCodes.Stelem_Any, GetType(item.type, true));
            } else {
                iLProcessor.Emit(OpCodes.Stelem_Ref);
            }
        }
    }

    private void EmitCastExpression(ILProcessor iLProcessor, BoundCastExpression expression) {
        if (expression.expression is BoundLiteralExpression le && le.constantValue.value == null) {
            EmitExpression(iLProcessor, new BoundLiteralExpression(le.value, expression.type));
            return;
        }

        EmitExpression(iLProcessor, expression.expression, handleAssignment: false);
        var subExpressionType = expression.expression.type;
        var expressionType = expression.type;

        var needsBoxing = subExpressionType.typeSymbol == TypeSymbol.Int ||
            subExpressionType.typeSymbol == TypeSymbol.Bool ||
            subExpressionType.typeSymbol == TypeSymbol.Decimal;

        if (needsBoxing)
            iLProcessor.Emit(OpCodes.Box, GetType(subExpressionType, ignoreReference: true));

        if (expressionType.typeSymbol != TypeSymbol.Any)
            iLProcessor.Emit(OpCodes.Call, GetConvertTo(subExpressionType, expressionType, true));

        if (expression.type.isNullable)
            iLProcessor.Emit(OpCodes.Call, GetNullableCtor(expression.type));
    }

    private void EmitCallExpression(ILProcessor iLProcessor, BoundCallExpression expression) {
        if (expression.function.MethodMatches(BuiltinFunctions.RandInt)) {
            if (_randomFieldDefinition == null)
                EmitRandomField();

            iLProcessor.Emit(OpCodes.Ldsfld, _randomFieldDefinition);
        }

        foreach (var argument in expression.arguments)
            EmitExpression(iLProcessor, argument);

        if (expression.function.MethodMatches(BuiltinFunctions.RandInt)) {
            iLProcessor.Emit(OpCodes.Callvirt, _methodReferences[_NetMethodReference.RandomNext]);
            return;
        }

        if (expression.function.MethodMatches(BuiltinFunctions.Print)) {
            iLProcessor.Emit(OpCodes.Call, _methodReferences[_NetMethodReference.ConsoleWrite]);
        } else if (expression.function.MethodMatches(BuiltinFunctions.PrintLine)) {
            iLProcessor.Emit(OpCodes.Call, _methodReferences[_NetMethodReference.ConsoleWriteLine]);
        } else if (expression.function.MethodMatches(BuiltinFunctions.Input)) {
            iLProcessor.Emit(OpCodes.Call, _methodReferences[_NetMethodReference.ConsoleReadLine]);
        } else if (expression.function.name == "Value") {
            EmitExpression(iLProcessor, expression.arguments[0]);
            iLProcessor.Emit(OpCodes.Call, GetNullableValue(expression.arguments[0].type));
        } else if (expression.function.MethodMatches(BuiltinFunctions.HasValue)) {
            EmitExpression(iLProcessor, expression.arguments[0]);
            iLProcessor.Emit(OpCodes.Call, GetNullableHasValue(expression.arguments[0].type));
        } else {
            var methodDefinition = LookupMethod(_methods, expression.function);
            iLProcessor.Emit(OpCodes.Call, methodDefinition);
        }
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
        iLProcessor.Emit(OpCodes.Newobj, _methodReferences[_NetMethodReference.RandomCtor]);
        iLProcessor.Emit(OpCodes.Stsfld, _randomFieldDefinition);
        iLProcessor.Emit(OpCodes.Ret);
    }

    private void EmitEmptyExpression(ILProcessor iLProcessor, BoundEmptyExpression expression) {
        // iLProcessor.Emit(OpCodes.Nop);
    }

    private MethodReference GetNullableCtor(BoundType type) {
        var genericArgumentType = _assemblyDefinition.MainModule.ImportReference(_knownTypes[type.typeSymbol]);
        var methodReference =
            _assemblyDefinition.MainModule.ImportReference(_methodReferences[_NetMethodReference.NullableCtor]);

        methodReference.DeclaringType = new GenericInstanceType(_nullableReference);
        (methodReference.DeclaringType as GenericInstanceType).GenericArguments.Add(genericArgumentType);
        methodReference.Resolve();

        return methodReference;
    }

    private MethodReference GetNullableValue(BoundType type) {
        var genericArgumentType = _assemblyDefinition.MainModule.ImportReference(_knownTypes[type.typeSymbol]);
        var methodReference =
            _assemblyDefinition.MainModule.ImportReference(_methodReferences[_NetMethodReference.NullableValue]);

        methodReference.DeclaringType = new GenericInstanceType(_nullableReference);
        (methodReference.DeclaringType as GenericInstanceType).GenericArguments.Add(genericArgumentType);
        methodReference.Resolve();

        return methodReference;
    }

    private MethodReference GetNullableHasValue(BoundType type) {
        var genericArgumentType = _assemblyDefinition.MainModule.ImportReference(_knownTypes[type.typeSymbol]);
        var methodReference =
            _assemblyDefinition.MainModule.ImportReference(_methodReferences[_NetMethodReference.NullableHasValue]);

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
                return _methodReferences[_NetMethodReference.ConvertToBoolean];
            else if (to.typeSymbol == TypeSymbol.Int)
                return _methodReferences[_NetMethodReference.ConvertToInt32];
            else if (to.typeSymbol == TypeSymbol.String)
                return _methodReferences[_NetMethodReference.ConvertToString];
            else if (to.typeSymbol == TypeSymbol.Decimal)
                return _methodReferences[_NetMethodReference.ConvertToSingle];
            else
                throw new BelteInternalException($"GetConvertTo: unexpected cast from '{from}' to '{to}'");
        }

        throw new BelteInternalException("GetConvertTo: cannot convert nullable types");
    }

    private void EmitAssignmentExpression(ILProcessor iLProcessor, BoundAssignmentExpression expression) {
        var variableDefinition = _locals[(expression.left as BoundVariableExpression).variable];

        if ((expression.left as BoundVariableExpression).variable.type.isReference)
            iLProcessor.Emit(OpCodes.Ldloc_S, variableDefinition);
        else if (expression.type.isNullable)
            iLProcessor.Emit(OpCodes.Ldloca_S, variableDefinition);
        else
            iLProcessor.Emit(OpCodes.Ldloc, variableDefinition);

        var nullable = expression.type.isNullable;

        EmitExpression(
            iLProcessor, expression.right, (expression.left as BoundVariableExpression).variable.type.isReference,
            nullable: nullable, stack: false);

        _useNullRef = true && nullable;
    }

    private void EmitVariableExpression(
        ILProcessor iLProcessor, BoundVariableExpression expression, bool nullable = true) {
        if (expression.variable is ParameterSymbol parameter) {
            if (!nullable)
                iLProcessor.Emit(OpCodes.Ldarga_S, parameter.ordinal);
            else
                iLProcessor.Emit(OpCodes.Ldarg, parameter.ordinal);
        } else {
            try {
                var variableDefinition = _locals[expression.variable];
                // ? When is Ldarga_S used
                iLProcessor.Emit(OpCodes.Ldloc, variableDefinition);
            } catch (KeyNotFoundException) {
                // ! This may have side affects
                ParameterSymbol foundParameter = null;

                foreach (var parameterSymbol in _currentFunction.parameters)
                    if (parameterSymbol.name == expression.variable.name)
                        foundParameter = parameterSymbol;

                if (foundParameter != null) {
                    // ? When is Ldarga_S used
                    iLProcessor.Emit(OpCodes.Ldarg, foundParameter.ordinal);
                } else {
                    throw new BelteInternalException(
                        $"EmitVariableExpression: could not find variable '{expression.variable.name}'");
                }
            }
        }

        if (!nullable && expression.variable.type.isNullable)
            iLProcessor.Emit(OpCodes.Call, GetNullableValue(expression.variable.type));

        if (expression.variable.type.isReference)
            iLProcessor.Emit(OpCodes.Ldobj, GetType(expression.variable.type, ignoreReference: true));
    }

    private void EmitBinaryExpression(ILProcessor iLProcessor, BoundBinaryExpression expression) {
        var leftType = expression.left.type.typeSymbol;
        var rightType = expression.right.type.typeSymbol;

        if (expression.op.opKind == BoundBinaryOperatorKind.Addition) {
            if (leftType == TypeSymbol.String && rightType == TypeSymbol.String ||
                leftType == TypeSymbol.Any && rightType == TypeSymbol.Any) {
                EmitStringConcatExpression(iLProcessor, expression);
                return;
            }
        }

        if ((BoundConstant.IsNull(expression.left.constantValue) ||
            BoundConstant.IsNull(expression.right.constantValue)) &&
            (expression.op.opKind == BoundBinaryOperatorKind.EqualityEquals ||
            expression.op.opKind == BoundBinaryOperatorKind.EqualityNotEquals)) {
            if ((BoundConstant.IsNull(expression.left.constantValue)) &&
                (BoundConstant.IsNull(expression.right.constantValue))) {
                if (expression.op.opKind == BoundBinaryOperatorKind.EqualityEquals)
                    iLProcessor.Emit(OpCodes.Ldc_I4_1);
                else
                    iLProcessor.Emit(OpCodes.Ldc_I4_0);

                return;
            }

            if (BoundConstant.IsNull(expression.left.constantValue)) {
                EmitExpression(iLProcessor, expression.right);
                iLProcessor.Emit(OpCodes.Call, GetNullableHasValue(expression.right.type));
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
                iLProcessor.Emit(OpCodes.Ceq);
            } else {
                EmitExpression(iLProcessor, expression.left);
                iLProcessor.Emit(OpCodes.Call, GetNullableHasValue(expression.left.type));
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
                iLProcessor.Emit(OpCodes.Ceq);
            }

            return;
        }

        EmitExpression(iLProcessor, expression.left, nullable: false);
        EmitExpression(iLProcessor, expression.right, nullable: false);

        if (expression.op.opKind == BoundBinaryOperatorKind.EqualityEquals) {
            if (leftType == TypeSymbol.String && rightType == TypeSymbol.String ||
                leftType == TypeSymbol.Any && rightType == TypeSymbol.Any) {
                iLProcessor.Emit(OpCodes.Call, _methodReferences[_NetMethodReference.ObjectEquals]);
                return;
            }
        }

        if (expression.op.opKind == BoundBinaryOperatorKind.EqualityNotEquals) {
            if (leftType == TypeSymbol.String && rightType == TypeSymbol.String ||
                leftType == TypeSymbol.Any && rightType == TypeSymbol.Any) {
                iLProcessor.Emit(OpCodes.Call, _methodReferences[_NetMethodReference.ObjectEquals]);
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
                iLProcessor.Emit(OpCodes.Ceq);
                return;
            }
        }

        EmitBinaryOperator(iLProcessor, expression, leftType, rightType);
    }

    private void EmitBinaryOperator(
        ILProcessor iLProcessor, BoundBinaryExpression expression, TypeSymbol leftType, TypeSymbol rightType) {
        switch (expression.op.opKind) {
            case BoundBinaryOperatorKind.Addition:
                iLProcessor.Emit(OpCodes.Add);
                break;
            case BoundBinaryOperatorKind.Subtraction:
                iLProcessor.Emit(OpCodes.Sub);
                break;
            case BoundBinaryOperatorKind.Multiplication:
                iLProcessor.Emit(OpCodes.Mul);
                break;
            case BoundBinaryOperatorKind.Division:
                iLProcessor.Emit(OpCodes.Div);
                break;
            case BoundBinaryOperatorKind.Power:
                break;
            case BoundBinaryOperatorKind.LogicalAnd:
                // TODO Should wait to emit right if left is false
                iLProcessor.Emit(OpCodes.And);
                break;
            case BoundBinaryOperatorKind.LogicalOr:
                iLProcessor.Emit(OpCodes.Or);
                break;
            case BoundBinaryOperatorKind.LogicalXor:
                iLProcessor.Emit(OpCodes.Xor);
                break;
            case BoundBinaryOperatorKind.LeftShift:
                iLProcessor.Emit(OpCodes.Shl);
                break;
            case BoundBinaryOperatorKind.RightShift:
                iLProcessor.Emit(OpCodes.Shr);
                break;
            case BoundBinaryOperatorKind.ConditionalAnd:
                iLProcessor.Emit(OpCodes.And);
                break;
            case BoundBinaryOperatorKind.ConditionalOr:
                iLProcessor.Emit(OpCodes.Or);
                break;
            case BoundBinaryOperatorKind.EqualityEquals:
                iLProcessor.Emit(OpCodes.Ceq);
                break;
            case BoundBinaryOperatorKind.EqualityNotEquals:
                iLProcessor.Emit(OpCodes.Ceq);
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
                iLProcessor.Emit(OpCodes.Ceq);
                break;
            case BoundBinaryOperatorKind.LessThan:
                iLProcessor.Emit(OpCodes.Clt);
                break;
            case BoundBinaryOperatorKind.GreaterThan:
                iLProcessor.Emit(OpCodes.Cgt);
                break;
            case BoundBinaryOperatorKind.LessOrEqual:
                iLProcessor.Emit(OpCodes.Cgt);
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
                iLProcessor.Emit(OpCodes.Ceq);
                break;
            case BoundBinaryOperatorKind.GreatOrEqual:
                iLProcessor.Emit(OpCodes.Clt);
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
                iLProcessor.Emit(OpCodes.Ceq);
                break;
            default:
                throw new BelteInternalException($"EmitBinaryOperator: unexpected binary operator" +
                    $"({leftType}){SyntaxFacts.GetText(expression.op.kind)}({rightType})");
        }
    }

    private void EmitStringConcatExpression(ILProcessor iLProcessor, BoundBinaryExpression expression) {
        // Flatten the expression tree to a sequence of nodes to concatenate,
        // Then fold consecutive constants in that sequence.
        // This approach enables constant folding of non-sibling nodes,
        // Which cannot be done in theConstantFolding class as it would require changing the tree.
        // Example: folding b and c in ((a + b) + c) if they are constant.

        var nodes = FoldConstants(Flatten(expression)).ToList();

        switch (nodes.Count) {
            case 0:
                iLProcessor.Emit(OpCodes.Ldstr, string.Empty);
                break;
            case 1:
                EmitExpression(iLProcessor, nodes[0]);
                break;
            case 2:
                EmitExpression(iLProcessor, nodes[0]);
                EmitExpression(iLProcessor, nodes[1]);
                iLProcessor.Emit(OpCodes.Call, _methodReferences[_NetMethodReference.StringConcat2]);
                break;
            case 3:
                EmitExpression(iLProcessor, nodes[0]);
                EmitExpression(iLProcessor, nodes[1]);
                EmitExpression(iLProcessor, nodes[2]);
                iLProcessor.Emit(OpCodes.Call, _methodReferences[_NetMethodReference.StringConcat3]);
                break;
            case 4:
                EmitExpression(iLProcessor, nodes[0]);
                EmitExpression(iLProcessor, nodes[1]);
                EmitExpression(iLProcessor, nodes[2]);
                EmitExpression(iLProcessor, nodes[3]);
                iLProcessor.Emit(OpCodes.Call, _methodReferences[_NetMethodReference.StringConcat4]);
                break;
            default:
                iLProcessor.Emit(OpCodes.Ldc_I4, nodes.Count);
                iLProcessor.Emit(OpCodes.Newarr, _knownTypes[TypeSymbol.String]);

                for (var i=0; i<nodes.Count; i++) {
                    iLProcessor.Emit(OpCodes.Dup);
                    iLProcessor.Emit(OpCodes.Ldc_I4, i);
                    EmitExpression(iLProcessor, nodes[i]);
                    iLProcessor.Emit(OpCodes.Stelem_Ref);
                }

                iLProcessor.Emit(OpCodes.Call, _methodReferences[_NetMethodReference.StringConcatArray]);
                break;
        }

        // TODO Use similar logic for other data types and operators (e.g. 2 * x * 4 -> 8 * x)

        // (a + b) + (c + d) --> [a, b, c, d]
        static IEnumerable<BoundExpression> Flatten(BoundExpression node) {
            if (node is BoundBinaryExpression binaryExpression &&
                binaryExpression.op.opKind == BoundBinaryOperatorKind.Addition &&
                binaryExpression.left.type.typeSymbol == TypeSymbol.String &&
                binaryExpression.right.type.typeSymbol == TypeSymbol.String) {
                foreach (var result in Flatten(binaryExpression.left))
                    yield return result;

                foreach (var result in Flatten(binaryExpression.right))
                    yield return result;
            } else {
                if (node.type.typeSymbol != TypeSymbol.String)
                    throw new BelteInternalException(
                        $"Flatten: unexpected node type in string concatenation '{node.type.typeSymbol}'");

                yield return node;
            }
        }

        // [a, "foo", "bar", b, ""] --> [a, "foobar", b]
        static IEnumerable<BoundExpression> FoldConstants(IEnumerable<BoundExpression> nodes) {
            StringBuilder sb = null;

            foreach (var node in nodes) {
                if (node.constantValue != null) {
                    var stringValue = (string)node.constantValue.value;

                    if (string.IsNullOrEmpty(stringValue))
                        continue;

                    sb ??= new StringBuilder();
                    sb.Append(stringValue);
                } else {
                    if (sb?.Length > 0) {
                        yield return new BoundLiteralExpression(sb.ToString());
                        sb.Clear();
                    }

                    yield return node;
                }
            }

            if (sb?.Length > 0)
                yield return new BoundLiteralExpression(sb.ToString());
        }
    }

    private void EmitConstantExpression(
        ILProcessor iLProcessor, BoundExpression expression, bool referenceAssign = false,
        bool nullable = true, bool stack = true, bool handleAssignment = true) {
        if (expression.constantValue.value == null) {
            iLProcessor.Emit(OpCodes.Initobj, GetType(expression.type));
            return;
        }

        var expressionType = expression.type.typeSymbol;

        if (expressionType == TypeSymbol.Int) {
            var value = Convert.ToInt32(expression.constantValue.value);
            iLProcessor.Emit(OpCodes.Ldc_I4, value);
        } else if (expressionType == TypeSymbol.String) {
            var value = Convert.ToString(expression.constantValue.value);
            iLProcessor.Emit(OpCodes.Ldstr, value);
        } else if (expressionType == TypeSymbol.Bool) {
            var value = Convert.ToBoolean(expression.constantValue.value);
            var instruction = value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
            iLProcessor.Emit(instruction);
        } else if (expressionType == TypeSymbol.Decimal) {
            var value = Convert.ToSingle(expression.constantValue.value);
            iLProcessor.Emit(OpCodes.Ldc_R4, value);
        } else {
            throw new BelteInternalException(
                $"EmitConstantExpression: unexpected constant expression type '{expressionType}'");
        }

        if (referenceAssign && handleAssignment) {
            iLProcessor.Emit(OpCodes.Newobj, GetNullableCtor(expression.type));
            iLProcessor.Emit(OpCodes.Stobj, GetType(expression.type));
        } else if (nullable && stack && handleAssignment) {
            iLProcessor.Emit(OpCodes.Newobj, GetNullableCtor(expression.type));
        } else if (nullable && handleAssignment) {
            iLProcessor.Emit(OpCodes.Call, GetNullableCtor(expression.type));
        }
    }

    private void EmitUnaryExpression(ILProcessor iLProcessor, BoundUnaryExpression expression) {
        EmitExpression(iLProcessor, expression.operand, nullable: false);

        if (expression.op.opKind == BoundUnaryOperatorKind.NumericalIdentity) {
        } else if (expression.op.opKind == BoundUnaryOperatorKind.NumericalNegation) {
            iLProcessor.Emit(OpCodes.Neg);
        } else if (expression.op.opKind == BoundUnaryOperatorKind.BooleanNegation) {
            iLProcessor.Emit(OpCodes.Ldc_I4_0);
            iLProcessor.Emit(OpCodes.Ceq);
        } else if (expression.op.opKind == BoundUnaryOperatorKind.BitwiseCompliment) {
            iLProcessor.Emit(OpCodes.Not);
        } else {
            throw new BelteInternalException($"EmitUnaryExpression: unexpected unary operator" +
                $"{SyntaxFacts.GetText(expression.op.kind)}({expression.operand.type.typeSymbol})");
        }
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
