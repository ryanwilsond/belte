using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using static Buckle.Utilities.MethodUtilities;
using Buckle.Utilities;
using Shared;

namespace Buckle.CodeAnalysis.Emitting;

/// <summary>
/// Emits a bound program into a .NET assembly.
/// </summary>
internal sealed partial class ILEmitter {
    private readonly List<AssemblyDefinition> _assemblies = new List<AssemblyDefinition>();
    private readonly List<(TypeSymbol type, string metadataName)> _builtinTypes;
    private readonly Dictionary<MethodSymbol, MethodDefinition> _methods =
        new Dictionary<MethodSymbol, MethodDefinition>();
    private readonly AssemblyDefinition _assemblyDefinition;
    private readonly Dictionary<TypeSymbol, TypeReference> _knownTypes;
    private readonly Dictionary<TypeSymbol, TypeDefinition> _typeDefinitions =
        new Dictionary<TypeSymbol, TypeDefinition>();
    private readonly Dictionary<VariableSymbol, VariableDefinition> _locals =
        new Dictionary<VariableSymbol, VariableDefinition>();
    private readonly List<(int instructionIndex, BoundLabel target)> _unhandledGotos =
        new List<(int instructionIndex, BoundLabel target)>();
    private readonly Dictionary<BoundLabel, int> _labels = new Dictionary<BoundLabel, int>();
    private readonly Dictionary<NetMethodReference, MethodReference> _methodReferences;
    private readonly TypeReference _randomReference;
    private readonly TypeReference _nullableReference;
    private readonly string _namespaceName;

    private TypeDefinition _programTypeDefinition;
    private FieldDefinition _randomFieldDefinition;
    private Stack<MethodDefinition> _methodStack = new Stack<MethodDefinition>();
    private bool _insideMain;
    private int _ternaryLabelCount;
    private string _dllPath = null;

    private ILEmitter(string moduleName, string[] references) {
        diagnostics = new BelteDiagnosticQueue();
        _namespaceName = moduleName;

        var tempReferences = (references ?? new string[] { }).ToList();
        tempReferences.AddRange(new string[] {
            LocateSystemDLL("System.Console.dll"),
            LocateSystemDLL("System.Runtime.dll"),
            LocateSystemDLL("System.Runtime.Extensions.dll")
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
                NetMethodReference.ConsoleWriteLineNoArgs,
                ResolveMethod("System.Console", "WriteLine", Array.Empty<string>())
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

    private string dllPath {
        get {
            if (_dllPath is null)
                _dllPath = GetDLLPath();

            return _dllPath;
        }
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

    private static string GetSafeName(string name) {
        var provider = CodeDomProvider.CreateProvider("C#");
        return (provider.IsValidIdentifier(name) ? name : "@" + name)
            .Replace('<', '_').Replace('>', '_').Replace(':', '_');
    }

    private static string GetDLLPath() {
        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "packs",
            "Microsoft.NETCore.App.Ref"
        );

        var frameworkVersion = RuntimeInformation.FrameworkDescription.Split(' ');
        var fullVersion = frameworkVersion.Contains("Core") || frameworkVersion.Contains("Framework")
            ? frameworkVersion[2]
            : frameworkVersion[1];

        var majorMinorVersion = string.Join('.', fullVersion.Split('.').Take(2));
        var fullPath = Path.Combine(basePath, fullVersion, "ref", $"net{majorMinorVersion}");

        return fullPath;
    }

    private string LocateSystemDLL(string dllName) {
        return Path.Combine(dllPath, dllName);
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
        var seenTypes = new HashSet<string>();

        using (var indentedTextWriter = new IndentedTextWriter(stringWriter, indentString))
        using (var classCurly = new CurlyIndenter(indentedTextWriter, _programTypeDefinition.ToString())) {
            foreach (var type in _typeDefinitions) {
                if (!seenTypes.Add(type.Key.name))
                    continue;

                if (isFirst)
                    isFirst = false;
                else
                    indentedTextWriter.WriteLine();

                using (var structCurly = new CurlyIndenter(indentedTextWriter, type.Value.ToString())) {
                    foreach (var field in type.Value.Fields)
                        indentedTextWriter.WriteLine(field);
                }
            }

            foreach (var method in _methods) {
                if (isFirst)
                    isFirst = false;
                else
                    indentedTextWriter.WriteLine();

                using (var methodCurly = new CurlyIndenter(indentedTextWriter, method.Value.ToString())) {
                    foreach (var instruction in method.Value.Body.Instructions)
                        indentedTextWriter.WriteLine(instruction);
                }
            }

            stringWriter.Flush();
        }

        return stringWriter.ToString();
    }

    private void EmitInternal(BoundProgram program) {
        var objectType = _knownTypes[TypeSymbol.Any];
        _programTypeDefinition = new TypeDefinition(
            "", "<Program>$", TypeAttributes.Abstract | TypeAttributes.Sealed, objectType);
        _assemblyDefinition.MainModule.Types.Add(_programTypeDefinition);

        foreach (var @struct in program.types.Where(t => t is StructSymbol))
            EmitStructDeclaration(@struct as StructSymbol);

        foreach (var methodWithBody in program.methodBodies) {
            var isMain = program.entryPoint == methodWithBody.Key;
            EmitMethodDeclaration(methodWithBody.Key, isMain);
        }

        foreach (var methodWithBody in program.methodBodies) {
            _insideMain = program.entryPoint == methodWithBody.Key;

            EmitMethodBody(methodWithBody.Key, methodWithBody.Value);
        }

        if (program.entryPoint != null)
            _assemblyDefinition.EntryPoint = LookupMethod(_methods, program.entryPoint);
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

        throw ExceptionUtilities.Unreachable();
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

            if (methods.Count() == 1 && parameterTypeNames is null)
                return _assemblyDefinition.MainModule.ImportReference(methods.Single());

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

            ThrowRequiredMethodNotFound(typeName, methodName, parameterTypeNames);
        } else if (foundTypes.Length == 0) {
            ThrowRequiredTypeNotFound(null, typeName);
        } else {
            ThrowRequiredTypeAmbiguous(null, typeName, foundTypes);
        }

        throw ExceptionUtilities.Unreachable();
    }

    private void ThrowRequiredMethodNotFound(string typeName, object methodName, string[] parameterTypeNames) {
        string message;

        if (parameterTypeNames is null) {
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

    private FieldReference GetFieldReference(BoundMemberAccessExpression expression) {
        return new FieldReference(
            GetSafeName(expression.member.name), GetType(expression.member.type), GetType(expression.type)
        );
    }

    private TypeReference GetType(
        BoundType type, bool overrideNullability = false, bool ignoreReference = false) {
        if ((type.dimensions == 0 && !type.isNullable && !overrideNullability) ||
            type.typeSymbol == TypeSymbol.Void) {
            return _knownTypes[type.typeSymbol];
        }

        var genericArgumentType = _assemblyDefinition.MainModule.ImportReference(_knownTypes[type.typeSymbol]);
        var typeReference = new GenericInstanceType(_nullableReference.Resolve());
        typeReference.GenericArguments.Add(genericArgumentType.Resolve());
        var referenceType = new ByReferenceType(typeReference.Resolve());

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
        _programTypeDefinition.Fields.Add(_randomFieldDefinition);
        var staticConstructor = new MethodDefinition(
            ".cctor",
            MethodAttributes.Static | MethodAttributes.Private |
            MethodAttributes.RTSpecialName | MethodAttributes.SpecialName,
            _knownTypes[TypeSymbol.Void]
        );
        _programTypeDefinition.Methods.Insert(0, staticConstructor);

        var iLProcessor = staticConstructor.Body.GetILProcessor();
        iLProcessor.Emit(OpCodes.Newobj, _methodReferences[NetMethodReference.RandomCtor]);
        iLProcessor.Emit(OpCodes.Stsfld, _randomFieldDefinition);
        iLProcessor.Emit(OpCodes.Ret);
    }

    private void EmitMethodBody(MethodSymbol method, BoundBlockStatement body) {
        var methodDefinition = _methods[method];
        _locals.Clear();
        _labels.Clear();
        _unhandledGotos.Clear();
        var iLProcessor = methodDefinition.Body.GetILProcessor();

        _methodStack.Push(methodDefinition);

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

        methodDefinition.Body.OptimizeMacros();
    }

    private void EmitMethodDeclaration(MethodSymbol method, bool isMain) {
        var methodType = isMain ? GetType(BoundType.CopyWith(method.type, isNullable: false)) : GetType(method.type);
        var newMethod = new MethodDefinition(
            method.name, MethodAttributes.Static | MethodAttributes.Private, methodType);

        foreach (var parameter in method.parameters) {
            var parameterType = GetType(parameter.type);
            var parameterAttributes = ParameterAttributes.None;
            var parameterDefinition = new ParameterDefinition(parameter.name, parameterAttributes, parameterType);
            newMethod.Parameters.Add(parameterDefinition);
        }

        _programTypeDefinition.Methods.Add(newMethod);
        _methods.Add(method, newMethod);
    }

    private void EmitStructDeclaration(StructSymbol @struct) {
        var objectType = _knownTypes[TypeSymbol.Any];
        var typeDefinition = new TypeDefinition(
            _namespaceName, GetSafeName(@struct.name), TypeAttributes.NestedPublic, objectType
        );

        foreach (var field in @struct.members.OfType<FieldSymbol>()) {
            var fieldDefinition = new FieldDefinition(
                GetSafeName(field.name), FieldAttributes.Public, GetType(field.type)
            );

            typeDefinition.Fields.Add(fieldDefinition);
        }

        _knownTypes.Add(@struct, typeDefinition);
        _typeDefinitions.Add(@struct, typeDefinition);
        _assemblyDefinition.MainModule.Types.Add(typeDefinition);
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
        _locals.Add(statement.variable, variableDefinition);
        iLProcessor.Body.Variables.Add(variableDefinition);

        var preset = true;

        if (statement.variable.type.isNullable &&
            statement.variable.type.typeSymbol is not StructSymbol &&
            statement.variable.type.dimensions < 1) {
            iLProcessor.Emit(OpCodes.Ldloca_S, variableDefinition);
        } else
            preset = false;

        EmitExpression(iLProcessor, statement.initializer);

        if (statement.variable.type.typeSymbol is StructSymbol)
            iLProcessor.Emit(OpCodes.Stloc_S, variableDefinition);
        else if (statement.variable.type.isNullable &&
            !BoundConstant.IsNull(statement.initializer.constantValue) &&
            statement.variable.type.dimensions < 1) {
            iLProcessor.Emit(OpCodes.Call, GetNullableCtor(statement.initializer.type));
        } else if (!preset)
            iLProcessor.Emit(OpCodes.Stloc, variableDefinition);
    }

    private void EmitReturnStatement(ILProcessor iLProcessor, BoundReturnStatement statement) {
        /*

        <expression>

        ----> no <expression>

        ret

        ----> inside Program.Main and <expresion> is 'null'

        ldc.i4.0
        ret

        ---->

        <expression>
        ret

        */
        if (statement.expression != null) {
            if (_insideMain && BoundConstant.IsNull(statement.expression.constantValue))
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
            else
                EmitExpression(iLProcessor, statement.expression);
        }

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

        if (statement.catchBody is null) {
            EmitTryFinally(statement.body.statements, statement.finallyBody.statements);
        } else if (statement.finallyBody is null) {
            EmitTryCatch(statement.body.statements, statement.catchBody.statements);
        } else {
            EmitTryFinally(
                ImmutableArray.Create<BoundStatement>(new BoundTryStatement(statement.body, statement.catchBody, null)),
                statement.finallyBody.statements
            );
        }
    }

    private void EmitExpressionStatement(ILProcessor iLProcessor, BoundExpressionStatement statement) {
        /*

        <expression>;

        ----> <expression> is a call and the return is not picked up

        <expression>
        pop

        ---->

        <expression>

        */
        EmitExpression(iLProcessor, statement.expression);

        if (statement.expression is BoundCallExpression && statement.expression.type.typeSymbol != TypeSymbol.Void)
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
                EmitVariableExpression(iLProcessor, (BoundVariableExpression)expression);
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
                // EmitIndexExpression(indentedTextWriter, (BoundIndexExpression)expression);
                break;
            case BoundNodeKind.CastExpression:
                EmitCastExpression(iLProcessor, (BoundCastExpression)expression);
                break;
            case BoundNodeKind.TernaryExpression:
                EmitTernaryExpression(iLProcessor, (BoundTernaryExpression)expression);
                break;
            case BoundNodeKind.ReferenceExpression:
                // EmitReferenceExpression(indentedTextWriter, (BoundReferenceExpression)expression);
                break;
            case BoundNodeKind.ObjectCreationExpression:
                EmitObjectCreationExpression(iLProcessor, (BoundObjectCreationExpression)expression);
                break;
            case BoundNodeKind.MemberAccessExpression:
                // EmitMemberAccessExpression(indentedTextWriter, (BoundMemberAccessExpression)expression);
                break;
            default:
                throw new BelteInternalException($"EmitExpression: unexpected node '{expression.kind}'");
        }
    }

    private void EmitConstantExpression(ILProcessor iLProcessor, BoundExpression expression) {
        EmitBoundConstant(iLProcessor, expression.constantValue, expression.type);
    }

    private void EmitBoundConstant(ILProcessor iLProcessor, BoundConstant constant, BoundType type) {
        if (BoundConstant.IsNull(constant)) {
            if (type.typeSymbol is StructSymbol)
                iLProcessor.Emit(OpCodes.Ldnull);
            else
                iLProcessor.Emit(OpCodes.Initobj, GetType(type));

            return;
        }

        var expressionType = type.typeSymbol;

        if (constant.value is ImmutableArray<BoundConstant> ia) {
            for (var i = 0; i < ia.Length; i++) {
                var item = ia[i];
                iLProcessor.Emit(OpCodes.Dup);
                iLProcessor.Emit(OpCodes.Ldc_I4, i);
                EmitBoundConstant(iLProcessor, item, type.ChildType());

                if (type.ChildType().dimensions == 0)
                    iLProcessor.Emit(OpCodes.Stelem_Any, GetType(type.ChildType()));
                else
                    iLProcessor.Emit(OpCodes.Stelem_Ref);
            }

            return;
        }

        if (expressionType == TypeSymbol.Int) {
            var value = Convert.ToInt32(constant.value);
            iLProcessor.Emit(OpCodes.Ldc_I4, value);
        } else if (expressionType == TypeSymbol.String) {
            var value = Convert.ToString(constant.value);
            iLProcessor.Emit(OpCodes.Ldstr, value);
        } else if (expressionType == TypeSymbol.Bool) {
            var value = Convert.ToBoolean(constant.value);
            var instruction = value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
            iLProcessor.Emit(instruction);
        } else if (expressionType == TypeSymbol.Decimal) {
            var value = Convert.ToSingle(constant.value);
            iLProcessor.Emit(OpCodes.Ldc_R4, value);
        } else if (expressionType == TypeSymbol.Any) {
            var assumedType = BoundType.Assume(constant.value);
            EmitBoundConstant(iLProcessor, constant, assumedType);
            iLProcessor.Emit(OpCodes.Box, GetType(assumedType));
        } else {
            throw new BelteInternalException(
                $"EmitBoundConstant: unexpected constant expression type '{expressionType}'"
            );
        }
    }

    private void EmitInitializerListExpression(ILProcessor iLProcessor, BoundInitializerListExpression expression) {
        /*

        <items>

        ---->

        ldc.i4 <items.Length>
        newarr <type>

            For each item (single dimension):
        dup
        ldc.i4 <i>
        <items[i]>
        stelem.any <item[i].type>

            For each item (multi-dimensional):
        dup
        ldc.i4 <i>
        <items[i]>
        stelem.ref

        */
        iLProcessor.Emit(OpCodes.Ldc_I4, expression.items.Length);
        iLProcessor.Emit(OpCodes.Newarr, GetType(expression.type.ChildType()));

        for (var i = 0; i < expression.items.Length; i++) {
            var item = expression.items[i];
            iLProcessor.Emit(OpCodes.Dup);
            iLProcessor.Emit(OpCodes.Ldc_I4, i);
            EmitExpression(iLProcessor, item);

            if (item.type.dimensions == 0) {
                iLProcessor.Emit(OpCodes.Stelem_Any, GetType(item.type));
            } else {
                iLProcessor.Emit(OpCodes.Stelem_Ref);
            }
        }
    }

    private void EmitUnaryExpression(ILProcessor iLProcessor, BoundUnaryExpression expression) {
        /*

        <op> <operand>

        ---->

        <operand>

            Depending on <op>:
        | neg
        | ldc.i4.0
          ceq
        | not

        */
        EmitExpression(iLProcessor, expression.operand);

        if (expression.op.opKind == BoundUnaryOperatorKind.NumericalNegation) {
            iLProcessor.Emit(OpCodes.Neg);
        } else if (expression.op.opKind == BoundUnaryOperatorKind.BooleanNegation) {
            iLProcessor.Emit(OpCodes.Ldc_I4_0);
            iLProcessor.Emit(OpCodes.Ceq);
        } else if (expression.op.opKind == BoundUnaryOperatorKind.BitwiseCompliment) {
            iLProcessor.Emit(OpCodes.Not);
        } else {
            throw new BelteInternalException($"EmitUnaryExpression: unexpected unary operator" +
                $"{SyntaxFacts.GetText(expression.op.kind)}({expression.operand.type.typeSymbol})"
            );
        }
    }

    private void EmitBinaryExpression(ILProcessor iLProcessor, BoundBinaryExpression expression) {
        /*

        <left> <op> <right>

        ----> <left> and <right> are strings and <op> is addition

        <left>
        <right>
        call System.String System.String::Concat(System.String, System.String)

        ---->

        <left>
        <right>

            Depending on <op>:
        | add
        | sub
        | mul
        | div
        | and
        | or
        | xor
        | shl
        | shr.un
        | and
        | or
        | ceq
        | ceq
          ldc.i4.0
          ceq
        | clt
        | cgt
        | cgt
          ldc.i4.0
          ceq
        | clt
          ldc.it.0
          ceq
        | rem

        */
        var leftType = expression.left.type.typeSymbol;
        var rightType = expression.right.type.typeSymbol;

        if (expression.op.opKind == BoundBinaryOperatorKind.Addition) {
            if (leftType == TypeSymbol.String && rightType == TypeSymbol.String ||
                leftType == TypeSymbol.Any && rightType == TypeSymbol.Any) {
                EmitStringConcatExpression(iLProcessor, expression);
                return;
            }
        }

        EmitExpression(iLProcessor, expression.left);
        EmitExpression(iLProcessor, expression.right);

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
            case BoundBinaryOperatorKind.LogicalAnd:
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
            case BoundBinaryOperatorKind.UnsignedRightShift:
                iLProcessor.Emit(OpCodes.Shr_Un);
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
            case BoundBinaryOperatorKind.Modulo:
                iLProcessor.Emit(OpCodes.Rem);
                break;
            default:
                throw new BelteInternalException($"EmitBinaryOperator: unexpected binary operator" +
                    $"({expression.left.type}){SyntaxFacts.GetText(expression.op.kind)}({expression.right.type})"
                );
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
                iLProcessor.Emit(OpCodes.Call, _methodReferences[NetMethodReference.StringConcat2]);
                break;
            case 3:
                EmitExpression(iLProcessor, nodes[0]);
                EmitExpression(iLProcessor, nodes[1]);
                EmitExpression(iLProcessor, nodes[2]);
                iLProcessor.Emit(OpCodes.Call, _methodReferences[NetMethodReference.StringConcat3]);
                break;
            case 4:
                EmitExpression(iLProcessor, nodes[0]);
                EmitExpression(iLProcessor, nodes[1]);
                EmitExpression(iLProcessor, nodes[2]);
                EmitExpression(iLProcessor, nodes[3]);
                iLProcessor.Emit(OpCodes.Call, _methodReferences[NetMethodReference.StringConcat4]);
                break;
            default:
                iLProcessor.Emit(OpCodes.Ldc_I4, nodes.Count);
                iLProcessor.Emit(OpCodes.Newarr, _knownTypes[TypeSymbol.String]);

                for (var i = 0; i < nodes.Count; i++) {
                    iLProcessor.Emit(OpCodes.Dup);
                    iLProcessor.Emit(OpCodes.Ldc_I4, i);
                    EmitExpression(iLProcessor, nodes[i]);
                    iLProcessor.Emit(OpCodes.Stelem_Ref);
                }

                iLProcessor.Emit(OpCodes.Call, _methodReferences[NetMethodReference.StringConcatArray]);
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
                if (node.type.typeSymbol != TypeSymbol.String) {
                    throw new BelteInternalException(
                        $"Flatten: unexpected node type in string concatenation '{node.type.typeSymbol}'"
                    );
                }

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

    private void EmitVariableExpression(ILProcessor iLProcessor, BoundVariableExpression expression) {
        /*

        <variable>

        ---->

        ldloc <variable>

        */
        iLProcessor.Emit(OpCodes.Ldloc, _locals[expression.variable]);
    }

    private void EmitAssignmentExpression(ILProcessor iLProcessor, BoundAssignmentExpression expression) {
        /*

        <left> = <right>

        ----> <left> is an IndexExpression

        <left>
        <right>
        stelem.any

        ----> <left> is a MemberAccessExpression

        <left>
        <right>
        stfld <left.field>

        ----> <left> is a VariableExpression and a struct

        ldloca.s <left.variable>
        <right>
        stloc.s <left.variable>

        ----> <left> is a VariableExpression and nullable or dimensioned

        ldloca.s <left.variable>
        <right>
        call T System.Nullable`1< <left.type> >::.ctor(T)

        ----> <left> is a VariableExpression and not nullable

        <left>
        <right>
        stloc <left.variable>

        */
        var isNullable = false;

        if (expression.left is BoundVariableExpression ve) {
            if (ve.variable.type.isNullable &&
                ve.variable.type.typeSymbol is not StructSymbol &&
                ve.variable.type.dimensions < 1) {
                if (!expression.right.type.isNullable) {
                    isNullable = true;
                    iLProcessor.Emit(OpCodes.Ldloca_S, _locals[ve.variable]);
                }
            }
        } else {
            EmitExpression(iLProcessor, expression.left);
        }

        EmitExpression(iLProcessor, expression.right);

        if (expression.left is BoundIndexExpression) {
            iLProcessor.Emit(OpCodes.Stelem_Any);
        } else if (expression.left is BoundMemberAccessExpression ma) {
            iLProcessor.Emit(OpCodes.Stfld, GetFieldReference(ma));
        } else if (expression.left is BoundVariableExpression bve) {
            if (expression.left.type.typeSymbol is StructSymbol)
                iLProcessor.Emit(OpCodes.Stloc_S, _locals[bve.variable]);
            else if (!isNullable)
                iLProcessor.Emit(OpCodes.Stloc, _locals[bve.variable]);
            else if (isNullable)
                iLProcessor.Emit(OpCodes.Call, GetNullableCtor(expression.left.type));
        }
    }

    private void EmitEmptyExpression(ILProcessor iLProcessor, BoundEmptyExpression expression) {
        /*

        ---->

        nop

        */
        iLProcessor.Emit(OpCodes.Nop);
    }

    private void EmitCallExpression(ILProcessor iLProcessor, BoundCallExpression expression) {
        /*

        <method>(<arguments>)

        ----> <method> is RandInt

        <arguments>
        callvirt System.Int32 System.Random::Next()

        ----> <method> is Print

        <arguments>
        call System.Void System.Console::Write(System.Object)

        ----> <method> is PrintLine

        <arguments>
        call System.Void System.Console::WriteLine(System.Object)

        ----> <method> is PrintLineNoValue

        call System.Void System.Console::WriteLine()

        ----> <method> is Input

        call System.String System.Console::ReadLine()

        ----> <method> is Value

        <arguments>
        stloc.s
        ldloca.s
        call T System.Nullable`1< <type> >::.get_Value()

        ----> <method> is HasValue

        <arguments>
        stloc.s
        ldloca.s
        call System.Boolean System.Nullable`1< <type> >::.get_HasValue()

        ---->

        <arguments>
        call <method>

        */
        if (expression.method == BuiltinMethods.RandInt) {
            if (_randomFieldDefinition is null)
                EmitRandomField();

            iLProcessor.Emit(OpCodes.Ldsfld, _randomFieldDefinition);
        }

        foreach (var argument in expression.arguments)
            EmitExpression(iLProcessor, argument);

        if (expression.method == BuiltinMethods.RandInt) {
            iLProcessor.Emit(OpCodes.Callvirt, _methodReferences[NetMethodReference.RandomNext]);
            return;
        }

        if (expression.method == BuiltinMethods.Print) {
            iLProcessor.Emit(OpCodes.Call, _methodReferences[NetMethodReference.ConsoleWrite]);
        } else if (expression.method == BuiltinMethods.PrintLine) {
            iLProcessor.Emit(OpCodes.Call, _methodReferences[NetMethodReference.ConsoleWriteLine]);
        } else if (expression.method == BuiltinMethods.PrintLineNoValue) {
            iLProcessor.Emit(OpCodes.Call, _methodReferences[NetMethodReference.ConsoleWriteLineNoArgs]);
        } else if (expression.method == BuiltinMethods.Input) {
            iLProcessor.Emit(OpCodes.Call, _methodReferences[NetMethodReference.ConsoleReadLine]);
        } else if (expression.method.name == "Value") {
            var typeReference = GetType(expression.arguments[0].type);
            var variableDefinition = new VariableDefinition(typeReference);
            iLProcessor.Body.Variables.Add(variableDefinition);

            iLProcessor.Emit(OpCodes.Stloc_S, variableDefinition);
            iLProcessor.Emit(OpCodes.Ldloca_S, variableDefinition);
            iLProcessor.Emit(OpCodes.Call, GetNullableValue(expression.arguments[0].type));
        } else if (expression.method.name == "HasValue") {
            var typeReference = GetType(expression.arguments[0].type);
            var variableDefinition = new VariableDefinition(typeReference);
            iLProcessor.Body.Variables.Add(variableDefinition);

            iLProcessor.Emit(OpCodes.Stloc_S, variableDefinition);
            iLProcessor.Emit(OpCodes.Ldloca_S, variableDefinition);
            iLProcessor.Emit(OpCodes.Call, GetNullableHasValue(expression.arguments[0].type));
        } else {
            var methodDefinition = LookupMethod(_methods, expression.method);
            iLProcessor.Emit(OpCodes.Call, methodDefinition);
        }
    }

    private void EmitCastExpression(ILProcessor iLProcessor, BoundCastExpression expression) {
        if (BoundConstant.IsNull(expression.expression.constantValue)) {
            EmitExpression(iLProcessor, new BoundLiteralExpression(null, expression.type));
            return;
        }

        EmitExpression(iLProcessor, expression.expression);
        var subExpressionType = expression.expression.type;
        var expressionType = expression.type;

        var needsBoxing = subExpressionType.typeSymbol == TypeSymbol.Int ||
            subExpressionType.typeSymbol == TypeSymbol.Bool ||
            subExpressionType.typeSymbol == TypeSymbol.Decimal;

        if (needsBoxing)
            iLProcessor.Emit(OpCodes.Box, GetType(subExpressionType));

        if (expressionType.typeSymbol != TypeSymbol.Any)
            iLProcessor.Emit(OpCodes.Call, GetConvertTo(subExpressionType, expressionType, true));

        if (expression.type.isNullable && !needsBoxing)
            iLProcessor.Emit(OpCodes.Call, GetNullableCtor(expression.type));
    }

    private void EmitTernaryExpression(ILProcessor iLProcessor, BoundTernaryExpression expression) {
        /*

        <left> <center> <op> <right>

        ----> <op> is conditional

        <left>
        brtrue.s TernaryLabel0
        <right>
        br.s TernaryLabel1
    TernaryLabel0
        nop
        <center>
    TernaryLabel1
        nop

        */
        switch (expression.op.opKind) {
            case BoundTernaryOperatorKind.Conditional:
                EmitExpression(iLProcessor, expression.left);

                var centerBranchLabel = GenerateLabel();
                _unhandledGotos.Add((iLProcessor.Body.Instructions.Count, centerBranchLabel));
                iLProcessor.Emit(OpCodes.Brtrue_S, Instruction.Create(OpCodes.Nop));

                EmitExpression(iLProcessor, expression.right);

                var rightBranchLabel = GenerateLabel();
                _unhandledGotos.Add((iLProcessor.Body.Instructions.Count, rightBranchLabel));
                iLProcessor.Emit(OpCodes.Br_S, Instruction.Create(OpCodes.Nop));

                _labels.Add(centerBranchLabel, iLProcessor.Body.Instructions.Count);

                EmitExpression(iLProcessor, expression.center);

                _labels.Add(rightBranchLabel, iLProcessor.Body.Instructions.Count);
                break;
            default:
                throw new BelteInternalException(
                    $"EmitTernaryExpression: unexpected ternary operator ({expression.left.type})" +
                    $"{SyntaxFacts.GetText(expression.op.leftOpKind)}({expression.center.type})" +
                    $"{SyntaxFacts.GetText(expression.op.rightOpKind)}({expression.right.type})"
                );
        }
    }

    private BoundLabel GenerateLabel() {
        var name = $"TernaryLabel{++_ternaryLabelCount}";

        return new BoundLabel(name);
    }

    private void EmitObjectCreationExpression(ILProcessor iLProcessor, BoundObjectCreationExpression expression) {
        // iLProcessor.Emit(OpCodes.Newobj, /* TODO */null);
    }
}
