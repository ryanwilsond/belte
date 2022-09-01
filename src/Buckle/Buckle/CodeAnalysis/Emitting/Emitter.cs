using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Diagnostics;

namespace Buckle.CodeAnalysis.Emitting;

internal class Emitter {

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
        ConvertToSingle,
        ObjectEquals,
        RandomNext,
        RandomCtor,
        NullableCtor,
        NullableValue,
        NullableHasValue,
    }

    internal BelteDiagnosticQueue diagnostics = new BelteDiagnosticQueue();

    private readonly List<AssemblyDefinition> assemblies = new List<AssemblyDefinition>();
    private readonly List<(TypeSymbol type, string metadataName)> builtinTypes;
    private readonly Dictionary<FunctionSymbol, MethodDefinition> methods_ =
        new Dictionary<FunctionSymbol, MethodDefinition>();
    private readonly AssemblyDefinition assemblyDefinition_;
    private readonly Dictionary<TypeSymbol, TypeReference> knownTypes_;
    private readonly Dictionary<VariableSymbol, VariableDefinition> locals_ =
        new Dictionary<VariableSymbol, VariableDefinition>();
    private readonly List<(int instructionIndex, BoundLabel target)> fixups_ =
        new List<(int instructionIndex, BoundLabel target)>();
    private readonly Dictionary<BoundLabel, int> labels_ = new Dictionary<BoundLabel, int>();
    private readonly Dictionary<NetMethodReference, MethodReference> methodReferences_;
    private readonly TypeReference randomReference_;
    private readonly TypeReference nullableReference_;

    private TypeDefinition typeDefinition_;
    private FieldDefinition randomFieldDefinition_;

    private Stack<MethodDefinition> methodStack_ = new Stack<MethodDefinition>();

    private Emitter(string moduleName, string[] references) {
        if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return;

        foreach (var reference in references) {
            try {
                var assembly = AssemblyDefinition.ReadAssembly(reference);
                assemblies.Add(assembly);
            } catch (BadImageFormatException) {
                diagnostics.Push(Error.InvalidReference(reference));
            }
        }

        if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return;

        builtinTypes = new List<(TypeSymbol type, string metadataName)>() {
            (TypeSymbol.Any, "System.Object"),
            (TypeSymbol.Bool, "System.Boolean"),
            (TypeSymbol.Int, "System.Int32"),
            (TypeSymbol.Decimal, "System.Single"),
            (TypeSymbol.String, "System.String"),
            (TypeSymbol.Void, "System.Void"),
        };

        var assemblyName = new AssemblyNameDefinition(moduleName, new Version(1, 0));
        assemblyDefinition_ = AssemblyDefinition.CreateAssembly(assemblyName, moduleName, ModuleKind.Console);
        knownTypes_ = new Dictionary<TypeSymbol, TypeReference>();

        foreach (var (typeSymbol, metadataName) in builtinTypes) {
            var typeReference = ResolveType(typeSymbol.name, metadataName);
            knownTypes_.Add(typeSymbol, typeReference);
        }

        methodReferences_ = new Dictionary<NetMethodReference, MethodReference>() {
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
                NetMethodReference.ConvertToSingle,
                ResolveMethod("System.Convert", "ToSingle", new [] { "System.Object" })
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

        randomReference_ = ResolveType(null, "System.Random");
        nullableReference_ = ResolveType(null, "System.Nullable`1");
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

    MethodReference ResolveMethod(
        string typeName, string methodName, string[] parameterTypeNames) {

        var foundTypes = assemblies.SelectMany(a => a.Modules)
            .SelectMany(m => m.Types)
            .Where(t => t.FullName == typeName)
            .ToArray();

        if (foundTypes.Length == 1) {
            var foundType = foundTypes[0];
            var methods = foundType.Methods.Where(m => m.Name == methodName);

            if (methods.ToArray().Length == 1 && parameterTypeNames == null)
                return assemblyDefinition_.MainModule.ImportReference(methods.Single());

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

    internal static BelteDiagnosticQueue Emit(
        BoundProgram program, string moduleName, string[] references, string outputPath) {
        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return program.diagnostics;

        var emitter = new Emitter(moduleName, references);
        return emitter.Emit(program, outputPath);
    }

    internal BelteDiagnosticQueue Emit(BoundProgram program, string outputPath) {
        diagnostics.Move(program.diagnostics);

        if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return diagnostics;

        var objectType = knownTypes_[TypeSymbol.Any];
        typeDefinition_ = new TypeDefinition(
            "", "<Program>$", TypeAttributes.Abstract | TypeAttributes.Sealed, objectType);
        assemblyDefinition_.MainModule.Types.Add(typeDefinition_);

        foreach (var functionWithBody in program.functionBodies)
            EmitFunctionDeclaration(functionWithBody.Key);

        foreach (var functionWithBody in program.functionBodies) {
            // currentFunction_ = functionWithBody.Key;
            EmitFunctionBody(functionWithBody.Key, functionWithBody.Value);
        }

        if (program.mainFunction != null)
            assemblyDefinition_.EntryPoint = LookupMethod(program.mainFunction);

        assemblyDefinition_.Write(outputPath);

        return diagnostics;
    }

    private void EmitFunctionBody(FunctionSymbol function, BoundBlockStatement body) {
        var method = methods_[function];
        locals_.Clear();
        labels_.Clear();
        fixups_.Clear();
        var iLProcessor = method.Body.GetILProcessor();

        methodStack_.Push(method);

        foreach (var statement in body.statements)
            EmitStatement(iLProcessor, statement);

        methodStack_.Pop();

        foreach (var fixup in fixups_) {
            var targetLabel = fixup.target;
            var targetInstructionIndex = labels_[targetLabel];
            var targetInstruction = iLProcessor.Body.Instructions[targetInstructionIndex];
            var instructionFix = iLProcessor.Body.Instructions[fixup.instructionIndex];
            instructionFix.Operand = targetInstruction;
        }

        method.Body.OptimizeMacros();
    }

    private void EmitStatement(ILProcessor iLProcessor, BoundStatement statement) {
        switch (statement.type) {
            case BoundNodeType.NopStatement:
                EmitNopStatement(iLProcessor, (BoundNopStatement)statement);
                break;
            case BoundNodeType.ExpressionStatement:
                // EmitExpressionStatement(iLProcessor, (BoundExpressionStatement)statement);
                break;
            case BoundNodeType.VariableDeclarationStatement:
                // EmitVariableDeclarationStatement(iLProcessor, (BoundVariableDeclarationStatement)statement);
                break;
            case BoundNodeType.GotoStatement:
                EmitGotoStatement(iLProcessor, (BoundGotoStatement)statement);
                break;
            case BoundNodeType.LabelStatement:
                EmitLabelStatement(iLProcessor, (BoundLabelStatement)statement);
                break;
            case BoundNodeType.ConditionalGotoStatement:
                // EmitConditionalGotoStatement(iLProcessor, (BoundConditionalGotoStatement)statement);
                break;
            case BoundNodeType.ReturnStatement:
                // EmitReturnStatement(iLProcessor, (BoundReturnStatement)statement);
                break;
            case BoundNodeType.TryStatement:
                EmitTryStatement(iLProcessor, (BoundTryStatement)statement);
                break;
            default:
                throw new Exception($"EmitStatement: unexpected node '{statement.type}'");
        }
    }

    private void EmitTryStatement(ILProcessor iLProcessor, BoundTryStatement statement) {
        var method = methodStack_.Last();

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
                TryStart=tryStart,
                TryEnd=handlerStart,
                HandlerStart=handlerStart,
                HandlerEnd=end,
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
                TryStart=tryStart,
                TryEnd=handlerStart,
                HandlerStart=handlerStart,
                HandlerEnd=end,
                CatchType=knownTypes_[TypeSymbol.Any],
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
                TryStart=innerTryStart,
                TryEnd=innerHandlerStart,
                HandlerStart=innerHandlerStart,
                HandlerEnd=finallyStart,
                CatchType=knownTypes_[TypeSymbol.Any],
            };

            foreach (var node in finallyBody)
                EmitStatement(iLProcessor, node);

            iLProcessor.Append(finallyEnd);
            iLProcessor.Append(end);

            var handler = new ExceptionHandler(ExceptionHandlerType.Finally) {
                TryStart=innerTryStart,
                TryEnd=finallyStart,
                HandlerStart=finallyStart,
                HandlerEnd=end,
            };

            method.Body.ExceptionHandlers.Add(innerHandler);
            method.Body.ExceptionHandlers.Add(handler);
        }
    }

    private void EmitNopStatement(ILProcessor iLProcessor, BoundNopStatement statement) {
        iLProcessor.Emit(OpCodes.Nop);
    }

    private void EmitLabelStatement(ILProcessor iLProcessor, BoundLabelStatement statement) {
        labels_.Add(statement.label, iLProcessor.Body.Instructions.Count);
    }

    private void EmitGotoStatement(ILProcessor iLProcessor, BoundGotoStatement statement) {
        fixups_.Add((iLProcessor.Body.Instructions.Count, statement.label));
        iLProcessor.Emit(OpCodes.Br, Instruction.Create(OpCodes.Nop));
    }

    private bool MethodsMatch(FunctionSymbol left, FunctionSymbol right) {
        if (left.name == right.name && left.parameters.Length == right.parameters.Length) {
            var parametersMatch = true;

            for (int i=0; i<left.parameters.Length; i++) {
                var checkParameter = left.parameters[i];
                var parameter = right.parameters[i];

                if (checkParameter.name != parameter.name || checkParameter.typeClause != parameter.typeClause)
                    parametersMatch = false;
            }

            if (parametersMatch)
                return true;
        }

        return false;
    }

    private MethodDefinition LookupMethod(FunctionSymbol function) {
        foreach (var pair in methods_)
            if (MethodsMatch(pair.Key, function))
                return pair.Value;

        throw new Exception($"LookupMethod: could not find method '{function.name}'");
    }

    private void EmitRandomField() {
        randomFieldDefinition_ = new FieldDefinition(
                                "$randInt", FieldAttributes.Static | FieldAttributes.Private, randomReference_);
        typeDefinition_.Fields.Add(randomFieldDefinition_);
        var staticConstructor = new MethodDefinition(
            ".cctor",
            MethodAttributes.Static | MethodAttributes.Private |
            MethodAttributes.RTSpecialName | MethodAttributes.SpecialName,
            knownTypes_[TypeSymbol.Void]
        );
        typeDefinition_.Methods.Insert(0, staticConstructor);

        var iLProcessor = staticConstructor.Body.GetILProcessor();
        iLProcessor.Emit(OpCodes.Newobj, methodReferences_[NetMethodReference.RandomCtor]);
        iLProcessor.Emit(OpCodes.Stsfld, randomFieldDefinition_);
        iLProcessor.Emit(OpCodes.Ret);
    }

    private void EmitEmptyExpression(ILProcessor iLProcessor, BoundEmptyExpression expression) {
        // TODO breaks control flow
        // iLProcessor.Emit(OpCodes.Nop);
    }

    private MethodReference GetNullableCtor(BoundTypeClause type) {
        var genericArgumentType = assemblyDefinition_.MainModule.ImportReference(knownTypes_[type.lType]);
        var methodReference =
            assemblyDefinition_.MainModule.ImportReference(methodReferences_[NetMethodReference.NullableCtor]);

        methodReference.DeclaringType = new GenericInstanceType(nullableReference_);
        (methodReference.DeclaringType as GenericInstanceType).GenericArguments.Add(genericArgumentType);
        methodReference.Resolve();

        return methodReference;
    }

    private MethodReference GetNullableValue(BoundTypeClause type) {
        var genericArgumentType = assemblyDefinition_.MainModule.ImportReference(knownTypes_[type.lType]);
        var methodReference =
            assemblyDefinition_.MainModule.ImportReference(methodReferences_[NetMethodReference.NullableValue]);

        methodReference.DeclaringType = new GenericInstanceType(nullableReference_);
        (methodReference.DeclaringType as GenericInstanceType).GenericArguments.Add(genericArgumentType);
        methodReference.Resolve();

        return methodReference;
    }

    private MethodReference GetNullableHasValue(BoundTypeClause type) {
        var genericArgumentType = assemblyDefinition_.MainModule.ImportReference(knownTypes_[type.lType]);
        var methodReference =
            assemblyDefinition_.MainModule.ImportReference(methodReferences_[NetMethodReference.NullableHasValue]);

        methodReference.DeclaringType = new GenericInstanceType(nullableReference_);
        (methodReference.DeclaringType as GenericInstanceType).GenericArguments.Add(genericArgumentType);
        methodReference.Resolve();

        return methodReference;
    }

    private MethodReference GetConvertTo(BoundTypeClause from, BoundTypeClause to, bool isImplicit) {
        if (!from.isNullable || isImplicit) {
            if (to.lType == TypeSymbol.Any)
                return null;
            else if (to.lType == TypeSymbol.Bool)
                return methodReferences_[NetMethodReference.ConvertToBoolean];
            else if (to.lType == TypeSymbol.Int)
                return methodReferences_[NetMethodReference.ConvertToInt32];
            else if (to.lType == TypeSymbol.String)
                return methodReferences_[NetMethodReference.ConvertToString];
            else if (to.lType == TypeSymbol.Decimal)
                return methodReferences_[NetMethodReference.ConvertToSingle];
            else
                throw new Exception($"GetConvertTo: unexpected cast from '{from}' to '{to}'");
        }

        throw new Exception("GetConvertTo: cannot convert nullable types");
    }

    private void EmitFunctionDeclaration(FunctionSymbol function) {
        var functionType = GetType(function.typeClause);
        var method = new MethodDefinition(
            function.name, MethodAttributes.Static | MethodAttributes.Private, functionType);

        foreach (var parameter in function.parameters) {
            var parameterType = GetType(parameter.typeClause);
            var parameterAttributes = ParameterAttributes.None;
            var parameterDefinition = new ParameterDefinition(parameter.name, parameterAttributes, parameterType);
            method.Parameters.Add(parameterDefinition);
        }

        typeDefinition_.Methods.Add(method);
        methods_.Add(function, method);
    }

    private TypeReference GetType(
        BoundTypeClause type, bool overrideNullability = false, bool ignoreReference = false) {
        if ((type.dimensions == 0 && !type.isNullable && !overrideNullability) ||
            type.lType == TypeSymbol.Void)
            return knownTypes_[type.lType];

        var genericArgumentType = assemblyDefinition_.MainModule.ImportReference(knownTypes_[type.lType]);
        var typeReference = new GenericInstanceType(nullableReference_);
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
