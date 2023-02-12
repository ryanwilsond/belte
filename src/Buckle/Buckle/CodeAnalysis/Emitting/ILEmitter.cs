using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Generators;
using Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using static Buckle.Utilities.FunctionUtilities;

namespace Buckle.CodeAnalysis.Emitting;

/// <summary>
/// Emits a bound program into a .NET assembly.
/// </summary>
internal sealed class ILEmitter {
    private readonly List<AssemblyDefinition> _assemblies = new List<AssemblyDefinition>();
    private readonly List<(TypeSymbol type, string metadataName)> _builtinTypes;
    private readonly Dictionary<FunctionSymbol, MethodDefinition> _methods =
        new Dictionary<FunctionSymbol, MethodDefinition>();
    private readonly AssemblyDefinition _assemblyDefinition;
    private readonly Dictionary<TypeSymbol, TypeReference> _knownTypes;
    private readonly Dictionary<VariableSymbol, VariableDefinition> _locals =
        new Dictionary<VariableSymbol, VariableDefinition>();
    private readonly List<(int instructionIndex, BoundLabel target)> _unhandledGotos =
        new List<(int instructionIndex, BoundLabel target)>();
    private readonly Dictionary<BoundLabel, int> _labels = new Dictionary<BoundLabel, int>();
    private readonly Dictionary<NetMethodReference, MethodReference> _methodReferences;
    private readonly TypeReference _randomReference;
    private readonly TypeReference _nullableReference;
    private TypeDefinition _typeDefinition;
    private FieldDefinition _randomFieldDefinition;
    private Stack<MethodDefinition> _methodStack = new Stack<MethodDefinition>();

    private ILEmitter(string moduleName, string[] references) {
        diagnostics = new BelteDiagnosticQueue();

        var tempReferences = references.ToList();
        tempReferences.AddRange(new string[] {
            "C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/3.1.0/ref/netcoreapp3.1/System.Console.dll",
            "C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/3.1.0/ref/netcoreapp3.1/System.Runtime.dll",
            "C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/3.1.0/ref/netcoreapp3.1/System.Runtime.Extensions.dll"
        });

        references = tempReferences.ToArray();

        foreach (var reference in references) {
            try {
                var assembly = AssemblyDefinition.ReadAssembly(reference);
                _assemblies.Add(assembly);
            } catch (BadImageFormatException) {
                diagnostics.Push(Error.InvalidReference(reference));
                return;
            }
        }

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
    /// Diagnostics produced by <see cref="ILEmitter" />.
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
    /// <returns>Any produced diagnostics.</returns>
    internal static BelteDiagnosticQueue Emit(
        BoundProgram program, string moduleName, string[] references, string outputPath) {
        var emitter = new ILEmitter(moduleName, references);
        return emitter.EmitToFile(program, outputPath);
    }

    /// <summary>
    /// Emits a program to a string.
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" /> to emit.</param>
    /// <param name="moduleName">Name of emitted assembly/application.</param>
    /// <param name="references">All external .NET references.</param>
    /// <param name="diagnostics">Any produced diagnostics.</param>
    /// <returns>IL code as a string.</returns>
    internal static string Emit(
        BoundProgram program, string moduleName, string[] references, out BelteDiagnosticQueue diagnostics) {
        var emitter = new ILEmitter(moduleName, references);
        return emitter.EmitToString(program, out diagnostics);
    }

    private BelteDiagnosticQueue EmitToFile(BoundProgram program, string outputPath) {
        EmitInternal(program);
        _assemblyDefinition.Write(outputPath);
        return diagnostics;
    }

    private string EmitToString(BoundProgram program, out BelteDiagnosticQueue diagnostics) {
        EmitInternal(program);
        diagnostics = this.diagnostics;

        var stringWriter = new StringWriter();
        var indentString = "    ";
        var isFirst = true;

        using (var indentedTextWriter = new IndentedTextWriter(stringWriter, indentString)) {
            foreach (var method in _methods) {
                if (isFirst)
                    isFirst = false;
                else
                    indentedTextWriter.WriteLine();

                using (var methodCurly = new CurlyIndenter(indentedTextWriter, method.Value.ToString())) {
                    foreach (var instruction in method.Value.Body.Instructions) {
                        indentedTextWriter.WriteLine(instruction);
                    }
                }
            }

            stringWriter.Flush();
        }

        return stringWriter.ToString();
    }

    private void EmitInternal(BoundProgram program) {
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

    private void EmitFunctionBody(FunctionSymbol function, BoundBlockStatement body) {
        var method = _methods[function];
        _locals.Clear();
        _labels.Clear();
        _unhandledGotos.Clear();
        var iLProcessor = method.Body.GetILProcessor();

        _methodStack.Push(method);

        foreach (var statement in body.statements)
            EmitStatement(iLProcessor, statement);

        _methodStack.Pop();

        foreach (var fixup in _unhandledGotos) {
            var targetLabel = fixup.target;
            var targetInstructionIndex = _labels[targetLabel];
            var targetInstruction = iLProcessor.Body.Instructions[targetInstructionIndex];
            var instructionFix = iLProcessor.Body.Instructions[fixup.instructionIndex];
            instructionFix.Operand = targetInstruction;
        }

        method.Body.OptimizeMacros();
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

    private void EmitStatement(ILProcessor iLProcessor, BoundStatement statement) {
        switch (statement.kind) {
            case BoundNodeKind.NopStatement:
                EmitNopStatement(iLProcessor, (BoundNopStatement)statement);
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
            case BoundNodeKind.VariableDeclarationStatement:
                EmitVariableDeclarationStatement(iLProcessor, (BoundVariableDeclarationStatement)statement);
                break;
            case BoundNodeKind.ReturnStatement:
                EmitReturnStatement(iLProcessor, (BoundReturnStatement)statement);
                break;
            case BoundNodeKind.TryStatement:
                EmitTryStatement(iLProcessor, (BoundTryStatement)statement);
                break;
            case BoundNodeKind.ExpressionStatement:
                EmitExpressionStatement(iLProcessor, (BoundExpressionStatement)statement);
                break;
            default:
                throw new BelteInternalException($"EmitStatement: unexpected node '{statement.kind}'");
        }
    }

    private void EmitNopStatement(ILProcessor iLProcessor, BoundNopStatement statement) {
        /*

        ---->

        nop

        */
        iLProcessor.Emit(OpCodes.Nop);
    }

    private void EmitLabelStatement(ILProcessor iLProcessor, BoundLabelStatement statement) {
        _labels.Add(statement.label, iLProcessor.Body.Instructions.Count);
    }

    private void EmitGotoStatement(ILProcessor iLProcessor, BoundGotoStatement statement) {
        /*

        <label>

        ---->

        br.s <label>

        */
        _unhandledGotos.Add((iLProcessor.Body.Instructions.Count, statement.label));
        iLProcessor.Emit(OpCodes.Br_S, Instruction.Create(OpCodes.Nop));
    }

    private void EmitConditionalGotoStatement(ILProcessor iLProcessor, BoundConditionalGotoStatement statement) {
        /*

        <label> <condition> <jumpIfTrue>

        ----> <jumpIfTrue> is true

        <condition>
        brtrue <label>

        ----> <jumpIfTrue> is false

        <condition>
        brfalse <label>

        */
        EmitExpression(iLProcessor, statement.condition);

        var opcode = statement.jumpIfTrue
            ? OpCodes.Brtrue
            : OpCodes.Brfalse;
        _unhandledGotos.Add((iLProcessor.Body.Instructions.Count, statement.label));
        iLProcessor.Emit(opcode, Instruction.Create(OpCodes.Nop));
    }

    private void EmitVariableDeclarationStatement(ILProcessor iLProcessor, BoundVariableDeclarationStatement statement) {
        /*

        <type> <variable> <initializer>

        ----> default case

        <initializer>
        stloc #

        ----> <type> is nullable and <initializer> is null

        ldloca.s #
        initobj valuetype <type>

        ----> <type> is nullable and <initializer> is not null

        ldloca.s #
        <initializer>
        call instance void valuetype <type>::.ctor(!0)

        ----> <type> is a struct

        <initializer>
        stloc.s #

        */
        var typeReference = GetType(statement.variable.type);
        var variableDefinition = new VariableDefinition(typeReference);
        iLProcessor.Body.Variables.Add(variableDefinition);

        var preset = true;

        if (statement.variable.type.isNullable && statement.variable.type.typeSymbol is not StructSymbol)
            iLProcessor.Emit(OpCodes.Ldloca_S, variableDefinition);
        else
            preset = false;

        EmitExpression(iLProcessor, statement.initializer);

        if (statement.variable.type.typeSymbol is StructSymbol)
            iLProcessor.Emit(OpCodes.Stloc_S, variableDefinition);
        else if (statement.variable.type.isNullable && !BoundConstant.IsNull(statement.initializer.constantValue))
            iLProcessor.Emit(OpCodes.Call, GetNullableCtor(statement.initializer.type));
        else if (!preset)
            iLProcessor.Emit(OpCodes.Stloc, variableDefinition);
    }

    private void EmitReturnStatement(ILProcessor iLProcessor, BoundReturnStatement statement) {
        /*

        <expression>

        ----> no <expression>

        ret

        ---->

        <expression>
        ret

        */
        if (statement.expression != null)
            EmitExpression(iLProcessor, statement.expression);

        iLProcessor.Emit(OpCodes.Ret);
    }

    private void EmitTryStatement(ILProcessor iLProcessor, BoundTryStatement statement) {
        /*

        <body> <catchBody> <finallyBody>

        ----> <catchBody> is null

        nop
        .try {
            nop
            <body>
            leave.s <label>
        } finally {
            nop
            <finallyBody>
            leave.s <label>
        }

        ----> <finallyBody> is null

        nop
        .try {
            nop
            <body>
            leave.s <label>
        } catch {
            nop
            <catchBody>
            leave.s <label>
        }

        ---->

        nop
        .try {
            nop
            nop
            .try {
                nop
                <body>
            } catch {
                nop
                <catchBody>
                leave.s <label>
            }
            leave.s <label>
        } finally {
            nop
            <finallyBody>
            leave.s <label>
        }

        */
        var method = _methodStack.Last();

        void EmitTryCatch(ImmutableArray<BoundStatement> tryBody, ImmutableArray<BoundStatement> catchBody) {
            var end = iLProcessor.Create(OpCodes.Nop);
            var tryStart = iLProcessor.Create(OpCodes.Nop);
            var tryEnd = iLProcessor.Create(OpCodes.Leave_S, end);

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
        }

        void EmitTryFinally(ImmutableArray<BoundStatement> tryBody, ImmutableArray<BoundStatement> finallyBody) {
            var end = iLProcessor.Create(OpCodes.Nop);
            var tryStart = iLProcessor.Create(OpCodes.Nop);
            var tryEnd = iLProcessor.Create(OpCodes.Leave_S, end);

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
        }

        if (statement.catchBody == null) {
            EmitTryFinally(statement.body.statements, statement.finallyBody.statements);
        } else if (statement.finallyBody == null) {
            EmitTryCatch(statement.body.statements, statement.catchBody.statements);
        } else {
            EmitTryFinally(
                ImmutableArray.Create<BoundStatement>(new BoundTryStatement(statement.body, statement.catchBody, null)),
                statement.finallyBody.statements
            );
            // var innerTryBody = statement.body.statements;
            // var end = iLProcessor.Create(OpCodes.Nop);
            // var innerTryStart = iLProcessor.Create(OpCodes.Nop);
            // var innerTryEnd = iLProcessor.Create(OpCodes.Leave_S, end);

            // var innerCatchBody = statement.catchBody.statements;
            // var innerHandlerStart = iLProcessor.Create(OpCodes.Nop);
            // var innerHandlerEnd = iLProcessor.Create(OpCodes.Leave_S, end);

            // var finallyBody = statement.finallyBody.statements;
            // var finallyStart = iLProcessor.Create(OpCodes.Nop);
            // var finallyEnd = iLProcessor.Create(OpCodes.Endfinally);

            // iLProcessor.Append(innerTryStart);

            // foreach (var node in innerTryBody)
            //     EmitStatement(iLProcessor, node);

            // iLProcessor.Append(innerTryEnd);
            // iLProcessor.Append(innerHandlerStart);

            // foreach (var node in innerCatchBody)
            //     EmitStatement(iLProcessor, node);

            // iLProcessor.Append(innerHandlerEnd);
            // iLProcessor.Append(finallyStart);

            // var innerHandler = new ExceptionHandler(ExceptionHandlerType.Catch) {
            //     TryStart = innerTryStart,
            //     TryEnd = innerHandlerStart,
            //     HandlerStart = innerHandlerStart,
            //     HandlerEnd = finallyStart,
            //     CatchType = _knownTypes[TypeSymbol.Any],
            // };

            // foreach (var node in finallyBody)
            //     EmitStatement(iLProcessor, node);

            // iLProcessor.Append(finallyEnd);
            // iLProcessor.Append(end);

            // var handler = new ExceptionHandler(ExceptionHandlerType.Finally) {
            //     TryStart = innerTryStart,
            //     TryEnd = finallyStart,
            //     HandlerStart = finallyStart,
            //     HandlerEnd = end,
            // };

            // method.Body.ExceptionHandlers.Add(innerHandler);
            // method.Body.ExceptionHandlers.Add(handler);
        }
    }

    private void EmitExpressionStatement(ILProcessor iLProcessor, BoundExpressionStatement statement) {
        EmitExpression(iLProcessor, statement.expression);

        if (statement.expression.type.typeSymbol != TypeSymbol.Void)
            iLProcessor.Emit(OpCodes.Pop);
    }

    private void EmitExpression(ILProcessor iLProcessor, BoundExpression expression) {
        if (expression.constantValue != null) {
            EmitConstantExpression(iLProcessor, expression);
            return;
        }

        switch (expression.kind) {
            case BoundNodeKind.LiteralExpression:
                if (expression is BoundInitializerListExpression il) {
                    // EmitInitializerListExpression(indentedTextWriter, il);
                    break;
                } else {
                    goto default;
                }
            case BoundNodeKind.UnaryExpression:
                // EmitUnaryExpression(indentedTextWriter, (BoundUnaryExpression)expression);
                break;
            case BoundNodeKind.BinaryExpression:
                // EmitBinaryExpression(indentedTextWriter, (BoundBinaryExpression)expression);
                break;
            case BoundNodeKind.VariableExpression:
                // EmitVariableExpression(indentedTextWriter, (BoundVariableExpression)expression);
                break;
            case BoundNodeKind.AssignmentExpression:
                // EmitAssignmentExpression(indentedTextWriter, (BoundAssignmentExpression)expression);
                break;
            case BoundNodeKind.EmptyExpression:
                EmitEmptyExpression(iLProcessor, (BoundEmptyExpression)expression);
                break;
            case BoundNodeKind.CallExpression:
                // EmitCallExpression(indentedTextWriter, (BoundCallExpression)expression);
                break;
            case BoundNodeKind.IndexExpression:
                // EmitIndexExpression(indentedTextWriter, (BoundIndexExpression)expression);
                break;
            case BoundNodeKind.CastExpression:
                // EmitCastExpression(indentedTextWriter, (BoundCastExpression)expression);
                break;
            case BoundNodeKind.TernaryExpression:
                // EmitTernaryExpression(indentedTextWriter, (BoundTernaryExpression)expression);
                break;
            case BoundNodeKind.ReferenceExpression:
                // EmitReferenceExpression(indentedTextWriter, (BoundReferenceExpression)expression);
                break;
            case BoundNodeKind.ConstructorExpression:
                EmitConstructorExpression(iLProcessor, (BoundConstructorExpression)expression);
                break;
            case BoundNodeKind.MemberAccessExpression:
                // EmitMemberAccessExpression(indentedTextWriter, (BoundMemberAccessExpression)expression);
                break;
            default:
                throw new BelteInternalException($"EmitExpression: unexpected node '{expression.kind}'");
        }
    }

    private void EmitConstantExpression(ILProcessor iLProcessor, BoundExpression expression) {
        if (BoundConstant.IsNull(expression.constantValue)) {
            if (expression.type.typeSymbol is StructSymbol)
                iLProcessor.Emit(OpCodes.Ldnull);
            else
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
    }

    private void EmitEmptyExpression(ILProcessor iLProcessor, BoundEmptyExpression expression) {
        iLProcessor.Emit(OpCodes.Nop);
    }

    private void EmitConstructorExpression(ILProcessor iLProcessor, BoundConstructorExpression expression) {
        // iLProcessor.Emit(OpCodes.Newobj, /* TODO */null);
    }
}
