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

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class Emitter {
    public DiagnosticQueue diagnostics = new DiagnosticQueue();

    private readonly List<AssemblyDefinition> assemblies = new List<AssemblyDefinition>();
    private readonly List<(TypeSymbol type, string metadataName)> builtinTypes;
    private readonly Dictionary<FunctionSymbol, MethodDefinition> methods_ =
        new Dictionary<FunctionSymbol, MethodDefinition>();
    private readonly AssemblyDefinition assemblyDefinition_;
    private readonly Dictionary<TypeSymbol, TypeReference> knownTypes_;
    private readonly MethodReference consoleWriteReference_;
    private readonly MethodReference consoleWriteLineReference_;
    private readonly MethodReference consoleReadLineReference_;
    private readonly MethodReference stringConcat2Reference_;
    private readonly MethodReference stringConcat3Reference_;
    private readonly MethodReference stringConcat4Reference_;
    private readonly MethodReference stringConcatArrayReference_;
    private readonly Dictionary<VariableSymbol, VariableDefinition> locals_ =
        new Dictionary<VariableSymbol, VariableDefinition>();
    private readonly List<(int instructionIndex, BoundLabel target)> fixups_ =
        new List<(int instructionIndex, BoundLabel target)>();
    private readonly Dictionary<BoundLabel, int> labels_ = new Dictionary<BoundLabel, int>();
    private readonly MethodReference convertToBooleanReference_;
    private readonly MethodReference convertToInt32Reference_;
    private readonly MethodReference convertToStringReference_;
    private readonly MethodReference convertToSingleReference_;
    private readonly MethodReference objectEqualsReference_;
    private readonly MethodReference randomNextReference_;
    private readonly TypeReference randomReference_;
    private readonly MethodReference randomCtorReference_;
    private readonly TypeReference nullableReference_;
    private readonly MethodReference nullableCtorReference_;
    private readonly MethodReference nullableValueReference_;
    private readonly MethodReference nullableHasValueReference_;
    private bool useNullRef = false;
    private FunctionSymbol currentFunction_;

    private TypeDefinition typeDefinition_;
    private FieldDefinition randomFieldDefinition_;

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

        consoleWriteReference_ = ResolveMethod("System.Console", "Write", new [] { "System.Object" });
        consoleWriteLineReference_ = ResolveMethod("System.Console", "WriteLine", new [] { "System.Object" });
        consoleReadLineReference_ = ResolveMethod("System.Console", "ReadLine", Array.Empty<string>());
        stringConcat2Reference_ = ResolveMethod(
            "System.String", "Concat", new [] { "System.String", "System.String" });
        stringConcat3Reference_ = ResolveMethod(
            "System.String", "Concat", new [] { "System.String", "System.String", "System.String" });
        stringConcat4Reference_ = ResolveMethod("System.String", "Concat",
            new [] { "System.String", "System.String", "System.String", "System.String" });
        stringConcatArrayReference_ = ResolveMethod("System.String", "Concat", new [] { "System.String[]" });
        convertToBooleanReference_ = ResolveMethod("System.Convert", "ToBoolean", new [] { "System.Object" });
        convertToInt32Reference_ = ResolveMethod("System.Convert", "ToInt32", new [] { "System.Object" });
        convertToSingleReference_ = ResolveMethod("System.Convert", "ToSingle", new [] { "System.Object" });
        convertToStringReference_ = ResolveMethod("System.Convert", "ToString", new [] { "System.Object" });
        objectEqualsReference_ = ResolveMethod(
            "System.Object", "Equals", new [] { "System.Object", "System.Object" });
        randomReference_ = ResolveType(null, "System.Random");
        randomCtorReference_ = ResolveMethod("System.Random", ".ctor", Array.Empty<string>());
        randomNextReference_ = ResolveMethod("System.Random", "Next", new [] { "System.Int32" });
        nullableReference_ = ResolveType(null, "System.Nullable`1");
        nullableCtorReference_ = ResolveMethod("System.Nullable`1", ".ctor", null);
        nullableValueReference_ = ResolveMethod("System.Nullable`1", "get_Value", null);
        nullableHasValueReference_ = ResolveMethod("System.Nullable`1", "get_HasValue", null);
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

    public static DiagnosticQueue Emit(
        BoundProgram program, string moduleName, string[] references, string outputPath) {
        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return program.diagnostics;

        var emitter = new Emitter(moduleName, references);
        return emitter.Emit(program, outputPath);
    }

    public DiagnosticQueue Emit(BoundProgram program, string outputPath) {
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
            currentFunction_ = functionWithBody.Key;
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

        foreach (var statement in body.statements)
            EmitStatement(iLProcessor, statement, method);

        foreach (var fixup in fixups_) {
            var targetLabel = fixup.target;
            var targetInstructionIndex = labels_[targetLabel];
            var targetInstruction = iLProcessor.Body.Instructions[targetInstructionIndex];
            var instructionFix = iLProcessor.Body.Instructions[fixup.instructionIndex];
            instructionFix.Operand = targetInstruction;
        }

        method.Body.OptimizeMacros();
    }

    private void EmitStatement(ILProcessor iLProcessor, BoundStatement statement, MethodDefinition method) {
        switch (statement.type) {
            case BoundNodeType.NopStatement:
                EmitNopStatement(iLProcessor, (BoundNopStatement)statement);
                break;
            case BoundNodeType.ExpressionStatement:
                EmitExpressionStatement(iLProcessor, (BoundExpressionStatement)statement);
                break;
            case BoundNodeType.VariableDeclarationStatement:
                EmitVariableDeclarationStatement(iLProcessor, (BoundVariableDeclarationStatement)statement);
                break;
            case BoundNodeType.GotoStatement:
                EmitGotoStatement(iLProcessor, (BoundGotoStatement)statement);
                break;
            case BoundNodeType.LabelStatement:
                EmitLabelStatement(iLProcessor, (BoundLabelStatement)statement);
                break;
            case BoundNodeType.ConditionalGotoStatement:
                EmitConditionalGotoStatement(iLProcessor, (BoundConditionalGotoStatement)statement);
                break;
            case BoundNodeType.ReturnStatement:
                EmitReturnStatement(iLProcessor, (BoundReturnStatement)statement);
                break;
            case BoundNodeType.TryStatement:
                EmitTryStatement(iLProcessor, (BoundTryStatement)statement, method);
                break;
            default:
                throw new Exception($"EmitStatement: unexpected node '{statement.type}'");
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
                EmitStatement(iLProcessor, node, method);

            iLProcessor.Append(tryEnd);
            iLProcessor.Append(handlerStart);

            foreach (var node in catchBody)
                EmitStatement(iLProcessor, node, method);

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
                EmitStatement(iLProcessor, node, method);

            iLProcessor.Append(innerTryEnd);
            iLProcessor.Append(innerHandlerStart);

            foreach (var node in innerCatchBody)
                EmitStatement(iLProcessor, node, method);

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
                EmitStatement(iLProcessor, node, method);

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

    private void EmitReturnStatement(ILProcessor iLProcessor, BoundReturnStatement statement) {
        if (statement.expression != null)
            EmitExpression(iLProcessor, statement.expression, nullable: statement.expression.typeClause.isNullable);

        iLProcessor.Emit(OpCodes.Ret);
    }

    private void EmitConditionalGotoStatement(ILProcessor iLProcessor, BoundConditionalGotoStatement statement) {
        EmitExpression(iLProcessor, statement.condition);

        // TODO: this is getting messed up
        var opcode = statement.jumpIfTrue
            ? OpCodes.Brtrue
            : OpCodes.Brfalse;
        fixups_.Add((iLProcessor.Body.Instructions.Count, statement.label));
        iLProcessor.Emit(opcode, Instruction.Create(OpCodes.Nop));
    }

    private void EmitLabelStatement(ILProcessor iLProcessor, BoundLabelStatement statement) {
        labels_.Add(statement.label, iLProcessor.Body.Instructions.Count);
    }

    private void EmitGotoStatement(ILProcessor iLProcessor, BoundGotoStatement statement) {
        fixups_.Add((iLProcessor.Body.Instructions.Count, statement.label));
        iLProcessor.Emit(OpCodes.Br, Instruction.Create(OpCodes.Nop));
    }

    private void EmitVariableDeclarationStatement(
        ILProcessor iLProcessor, BoundVariableDeclarationStatement statement) {
        var typeReference = GetType(statement.variable.typeClause);
        var variableDefinition = new VariableDefinition(typeReference);
        locals_.Add(statement.variable, variableDefinition);
        iLProcessor.Body.Variables.Add(variableDefinition);

        if (statement.variable.typeClause.isReference) {
            if (statement.variable is ParameterSymbol parameter) {
                iLProcessor.Emit(OpCodes.Ldarga_S, parameter.ordinal);
            } else {
                var referenceVariable = locals_[((BoundReferenceExpression)statement.initializer).variable];
                iLProcessor.Emit(OpCodes.Ldloca_S, referenceVariable);
            }

            iLProcessor.Emit(OpCodes.Stloc, variableDefinition);
            return;
        }

        if (statement.variable.typeClause.dimensions == 0 && statement.variable.typeClause.isNullable &&
            statement.initializer.type != BoundNodeType.CallExpression)
            iLProcessor.Emit(OpCodes.Ldloca_S, variableDefinition);

        EmitExpression(
            iLProcessor, statement.initializer, nullable: statement.variable.typeClause.isNullable, stack: false);

        if (statement.variable.typeClause.dimensions > 0 || !statement.variable.typeClause.isNullable ||
            statement.initializer.type == BoundNodeType.CallExpression)
            iLProcessor.Emit(OpCodes.Stloc, variableDefinition);
    }

    private void EmitExpressionStatement(ILProcessor iLProcessor, BoundExpressionStatement statement) {
        EmitExpression(iLProcessor, statement.expression);

        if (statement.expression.typeClause?.lType != TypeSymbol.Void && !useNullRef)
            iLProcessor.Emit(OpCodes.Pop);

        useNullRef = false;
    }

    private void EmitExpression(
        ILProcessor iLProcessor, BoundExpression expression, bool referenceAssign = false,
        bool nullable = true, bool stack = true, bool handleAssignment = true) {
        if (expression.constantValue != null) {
            EmitConstantExpression(iLProcessor, expression, referenceAssign, nullable, stack, handleAssignment);
            return;
        }

        switch (expression.type) {
            case BoundNodeType.LiteralExpression:
                if (expression is BoundInitializerListExpression il) {
                    EmitInitializerListExpression(iLProcessor, il);
                    break;
                } else {
                    goto default;
                }
            case BoundNodeType.UnaryExpression:
                EmitUnaryExpression(iLProcessor, (BoundUnaryExpression)expression);
                break;
            case BoundNodeType.BinaryExpression:
                EmitBinaryExpression(iLProcessor, (BoundBinaryExpression)expression);
                break;
            case BoundNodeType.VariableExpression:
                EmitVariableExpression(iLProcessor, (BoundVariableExpression)expression, nullable);
                break;
            case BoundNodeType.AssignmentExpression:
                EmitAssignmentExpression(iLProcessor, (BoundAssignmentExpression)expression);
                break;
            case BoundNodeType.EmptyExpression:
                EmitEmptyExpression(iLProcessor, (BoundEmptyExpression)expression);
                break;
            case BoundNodeType.CallExpression:
                EmitCallExpression(iLProcessor, (BoundCallExpression)expression);
                break;
            case BoundNodeType.IndexExpression:
                EmitIndexExpression(iLProcessor, (BoundIndexExpression)expression);
                break;
            case BoundNodeType.CastExpression:
                EmitCastExpression(iLProcessor, (BoundCastExpression)expression);
                break;
            default:
                throw new Exception($"EmitExpression: unexpected node '{expression.type}'");
        }
    }

    private void EmitIndexExpression(ILProcessor iLProcessor, BoundIndexExpression expression) {
        EmitExpression(iLProcessor, expression.expression);
        iLProcessor.Emit(OpCodes.Ldc_I4, (int)expression.index.constantValue.value);

        var typeClause = expression.expression.typeClause;

        if (typeClause.ChildType().dimensions == 0) {
            iLProcessor.Emit(OpCodes.Ldelem_Any, GetType(typeClause.BaseType()));
        } else {
            iLProcessor.Emit(OpCodes.Ldelem_Ref);
        }
    }

    private void EmitInitializerListExpression(ILProcessor iLProcessor, BoundInitializerListExpression expression) {
        iLProcessor.Emit(OpCodes.Ldc_I4, expression.items.Length);
        iLProcessor.Emit(OpCodes.Newarr, GetType(expression.typeClause.ChildType()));

        for (int i=0; i<expression.items.Length; i++) {
            var item = expression.items[i];
            iLProcessor.Emit(OpCodes.Dup);
            iLProcessor.Emit(OpCodes.Ldc_I4, i);
            EmitExpression(iLProcessor, item);

            if (item.typeClause.dimensions == 0) {
                iLProcessor.Emit(OpCodes.Newobj, GetNullableCtor(item.typeClause));
                iLProcessor.Emit(OpCodes.Stelem_Any, GetType(item.typeClause, true));
            } else {
                iLProcessor.Emit(OpCodes.Stelem_Ref);
            }
        }
    }

    private void EmitCastExpression(ILProcessor iLProcessor, BoundCastExpression expression) {
        if (expression.expression is BoundLiteralExpression le && le.constantValue.value == null) {
            EmitExpression(iLProcessor, new BoundLiteralExpression(le.value, expression.typeClause));
            return;
        }

        EmitExpression(iLProcessor, expression.expression, handleAssignment: false);
        var subExpressionType = expression.expression.typeClause;
        var expressionType = expression.typeClause;

        var needsBoxing = subExpressionType.lType == TypeSymbol.Int ||
            subExpressionType.lType == TypeSymbol.Bool ||
            subExpressionType.lType == TypeSymbol.Decimal;

        if (needsBoxing)
            iLProcessor.Emit(OpCodes.Box, GetType(subExpressionType, ignoreReference: true));

        if (expressionType.lType != TypeSymbol.Any)
            iLProcessor.Emit(OpCodes.Call, GetConvertTo(subExpressionType, expressionType, true));

        if (expression.typeClause.isNullable)
            iLProcessor.Emit(OpCodes.Call, GetNullableCtor(expression.typeClause));
    }

    private void EmitCallExpression(ILProcessor iLProcessor, BoundCallExpression expression) {
        if (expression.function == BuiltinFunctions.Randint) {
            if (randomFieldDefinition_ == null)
                EmitRandomField();

            iLProcessor.Emit(OpCodes.Ldsfld, randomFieldDefinition_);
        }

        foreach (var argument in expression.arguments)
            EmitExpression(iLProcessor, argument);

        if (expression.function == BuiltinFunctions.Randint) {
            iLProcessor.Emit(OpCodes.Callvirt, randomNextReference_);
            return;
        }

        if (MethodsMatch(expression.function, BuiltinFunctions.Print)) {
            iLProcessor.Emit(OpCodes.Call, consoleWriteReference_);
        } else if (MethodsMatch(expression.function, BuiltinFunctions.PrintLine)) {
            iLProcessor.Emit(OpCodes.Call, consoleWriteLineReference_);
        } else if (MethodsMatch(expression.function, BuiltinFunctions.Input)) {
            iLProcessor.Emit(OpCodes.Call, consoleReadLineReference_);
        } else if (expression.function.name == "Value") {
            EmitExpression(iLProcessor, expression.arguments[0]);
            iLProcessor.Emit(OpCodes.Call, GetNullableValue(expression.arguments[0].typeClause));
        } else {
            var methodDefinition = LookupMethod(expression.function);
            iLProcessor.Emit(OpCodes.Call, methodDefinition);
        }
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
        iLProcessor.Emit(OpCodes.Newobj, randomCtorReference_);
        iLProcessor.Emit(OpCodes.Stsfld, randomFieldDefinition_);
        iLProcessor.Emit(OpCodes.Ret);
    }

    private void EmitEmptyExpression(ILProcessor iLProcessor, BoundEmptyExpression expression) {
        // TODO: breaks control flow
        // iLProcessor.Emit(OpCodes.Nop);
    }

    private MethodReference GetNullableCtor(BoundTypeClause type) {
        var genericArgumentType = assemblyDefinition_.MainModule.ImportReference(knownTypes_[type.lType]);
        var methodReference = assemblyDefinition_.MainModule.ImportReference(nullableCtorReference_);
        methodReference.DeclaringType = new GenericInstanceType(nullableReference_);
        (methodReference.DeclaringType as GenericInstanceType).GenericArguments.Add(genericArgumentType);
        methodReference.Resolve();

        return methodReference;
    }

    private MethodReference GetNullableValue(BoundTypeClause type) {
        var genericArgumentType = assemblyDefinition_.MainModule.ImportReference(knownTypes_[type.lType]);
        var methodReference = assemblyDefinition_.MainModule.ImportReference(nullableValueReference_);
        methodReference.DeclaringType = new GenericInstanceType(nullableReference_);
        (methodReference.DeclaringType as GenericInstanceType).GenericArguments.Add(genericArgumentType);
        methodReference.Resolve();

        return methodReference;
    }

    private MethodReference GetNullableHasValue(BoundTypeClause type) {
        var genericArgumentType = assemblyDefinition_.MainModule.ImportReference(knownTypes_[type.lType]);
        var methodReference = assemblyDefinition_.MainModule.ImportReference(nullableHasValueReference_);
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
                return convertToBooleanReference_;
            else if (to.lType == TypeSymbol.Int)
                return convertToInt32Reference_;
            else if (to.lType == TypeSymbol.String)
                return convertToStringReference_;
            else if (to.lType == TypeSymbol.Decimal)
                return convertToSingleReference_;
            else
                throw new Exception($"GetConvertTo: unexpected cast from '{from}' to '{to}'");
        }

        throw new Exception("GetConvertTo: cannot convert nullable types");
    }

    private void EmitAssignmentExpression(ILProcessor iLProcessor, BoundAssignmentExpression expression) {
        var variableDefinition = locals_[expression.variable];

        if (expression.variable.typeClause.isReference)
            iLProcessor.Emit(OpCodes.Ldloc_S, variableDefinition);
        else if (expression.typeClause.isNullable)
            iLProcessor.Emit(OpCodes.Ldloca_S, variableDefinition);
        else
            iLProcessor.Emit(OpCodes.Ldloc, variableDefinition);

        var nullable = expression.typeClause.isNullable;

        EmitExpression(
            iLProcessor, expression.expression, expression.variable.typeClause.isReference,
            nullable: nullable, stack: false);

        useNullRef = true && nullable;
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
                var variableDefinition = locals_[expression.variable];
                // ? when is Ldarga_S used
                iLProcessor.Emit(OpCodes.Ldloc, variableDefinition);
            } catch {
                // ! This may have side affects
                ParameterSymbol foundParameter = null;

                foreach (var parameterSymbol in currentFunction_.parameters)
                    if (parameterSymbol.name == expression.variable.name)
                        foundParameter = parameterSymbol;

                if (foundParameter != null) {
                    // ? when is Ldarga_S used
                    iLProcessor.Emit(OpCodes.Ldarg, foundParameter.ordinal);
                } else {
                    throw new Exception(
                        $"EmitVariableExpression: could not find variable '{expression.variable.name}'");
                }
            }
        }

        if (!nullable && expression.variable.typeClause.isNullable)
            iLProcessor.Emit(OpCodes.Call, GetNullableValue(expression.variable.typeClause));

        if (expression.variable.typeClause.isReference)
            iLProcessor.Emit(OpCodes.Ldobj, GetType(expression.variable.typeClause, ignoreReference: true));
    }

    private void EmitBinaryExpression(ILProcessor iLProcessor, BoundBinaryExpression expression) {
        var leftType = expression.left.typeClause.lType;
        var rightType = expression.right.typeClause.lType;

        if (expression.op.opType == BoundBinaryOperatorType.Addition) {
            if (leftType == TypeSymbol.String && rightType == TypeSymbol.String ||
                leftType == TypeSymbol.Any && rightType == TypeSymbol.Any) {
                EmitStringConcatExpression(iLProcessor, expression);
                return;
            }
        }

        if (((expression.left.constantValue != null && expression.left.constantValue?.value == null)
            || (expression.right.constantValue != null && expression.right.constantValue?.value == null)) &&
            (expression.op.opType == BoundBinaryOperatorType.EqualityEquals ||
            expression.op.opType == BoundBinaryOperatorType.EqualityNotEquals)) {
            if ((expression.left.constantValue != null && expression.left.constantValue?.value == null) &&
                (expression.right.constantValue != null && expression.right.constantValue?.value == null)) {
                if (expression.op.opType == BoundBinaryOperatorType.EqualityEquals)
                    iLProcessor.Emit(OpCodes.Ldc_I4_1);
                else
                    iLProcessor.Emit(OpCodes.Ldc_I4_0);

                return;
            }

            if ((expression.left.constantValue != null && expression.left.constantValue?.value == null)) {
                EmitExpression(iLProcessor, expression.right);
                iLProcessor.Emit(OpCodes.Call, GetNullableHasValue(expression.right.typeClause));
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
                iLProcessor.Emit(OpCodes.Ceq);
            } else {
                EmitExpression(iLProcessor, expression.left);
                iLProcessor.Emit(OpCodes.Call, GetNullableHasValue(expression.left.typeClause));
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
                iLProcessor.Emit(OpCodes.Ceq);
            }

            return;
        }

        EmitExpression(iLProcessor, expression.left, nullable: false);
        EmitExpression(iLProcessor, expression.right, nullable: false);

        if (expression.op.opType == BoundBinaryOperatorType.EqualityEquals) {
            if (leftType == TypeSymbol.String && rightType == TypeSymbol.String ||
                leftType == TypeSymbol.Any && rightType == TypeSymbol.Any) {
                iLProcessor.Emit(OpCodes.Call, objectEqualsReference_);
                return;
            }
        }

        if (expression.op.opType == BoundBinaryOperatorType.EqualityNotEquals) {
            if (leftType == TypeSymbol.String && rightType == TypeSymbol.String ||
                leftType == TypeSymbol.Any && rightType == TypeSymbol.Any) {
                iLProcessor.Emit(OpCodes.Call, objectEqualsReference_);
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
                iLProcessor.Emit(OpCodes.Ceq);
                return;
            }
        }

        EmitBinaryOperator(iLProcessor, expression, leftType, rightType);
    }

    private void EmitBinaryOperator(
        ILProcessor iLProcessor, BoundBinaryExpression expression, TypeSymbol leftType, TypeSymbol rightType) {
        switch (expression.op.opType) {
            case BoundBinaryOperatorType.Addition:
                iLProcessor.Emit(OpCodes.Add);
                break;
            case BoundBinaryOperatorType.Subtraction:
                iLProcessor.Emit(OpCodes.Sub);
                break;
            case BoundBinaryOperatorType.Multiplication:
                iLProcessor.Emit(OpCodes.Mul);
                break;
            case BoundBinaryOperatorType.Division:
                iLProcessor.Emit(OpCodes.Div);
                break;
            case BoundBinaryOperatorType.Power:
                break;
            case BoundBinaryOperatorType.LogicalAnd:
                // TODO: should wait to emit right if left is false
                iLProcessor.Emit(OpCodes.And);
                break;
            case BoundBinaryOperatorType.LogicalOr:
                iLProcessor.Emit(OpCodes.Or);
                break;
            case BoundBinaryOperatorType.LogicalXor:
                iLProcessor.Emit(OpCodes.Xor);
                break;
            case BoundBinaryOperatorType.LeftShift:
                iLProcessor.Emit(OpCodes.Shl);
                break;
            case BoundBinaryOperatorType.RightShift:
                iLProcessor.Emit(OpCodes.Shr);
                break;
            case BoundBinaryOperatorType.ConditionalAnd:
                iLProcessor.Emit(OpCodes.And);
                break;
            case BoundBinaryOperatorType.ConditionalOr:
                iLProcessor.Emit(OpCodes.Or);
                break;
            case BoundBinaryOperatorType.EqualityEquals:
                iLProcessor.Emit(OpCodes.Ceq);
                break;
            case BoundBinaryOperatorType.EqualityNotEquals:
                iLProcessor.Emit(OpCodes.Ceq);
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
                iLProcessor.Emit(OpCodes.Ceq);
                break;
            case BoundBinaryOperatorType.LessThan:
                iLProcessor.Emit(OpCodes.Clt);
                break;
            case BoundBinaryOperatorType.GreaterThan:
                iLProcessor.Emit(OpCodes.Cgt);
                break;
            case BoundBinaryOperatorType.LessOrEqual:
                iLProcessor.Emit(OpCodes.Cgt);
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
                iLProcessor.Emit(OpCodes.Ceq);
                break;
            case BoundBinaryOperatorType.GreatOrEqual:
                iLProcessor.Emit(OpCodes.Clt);
                iLProcessor.Emit(OpCodes.Ldc_I4_0);
                iLProcessor.Emit(OpCodes.Ceq);
                break;
            default:
                throw new Exception($"EmitBinaryOperator: unexpected binary operator" +
                    $"({leftType}){SyntaxFacts.GetText(expression.op.type)}({rightType})");
        }
    }

    private void EmitStringConcatExpression(ILProcessor iLProcessor, BoundBinaryExpression expression) {
        // Flatten the expression tree to a sequence of nodes to concatenate,
        // then fold consecutive constants in that sequence.
        // This approach enables constant folding of non-sibling nodes,
        // which cannot be done in theConstantFolding class as it would require changing the tree.
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
                iLProcessor.Emit(OpCodes.Call, stringConcat2Reference_);
                break;
            case 3:
                EmitExpression(iLProcessor, nodes[0]);
                EmitExpression(iLProcessor, nodes[1]);
                EmitExpression(iLProcessor, nodes[2]);
                iLProcessor.Emit(OpCodes.Call, stringConcat3Reference_);
                break;
            case 4:
                EmitExpression(iLProcessor, nodes[0]);
                EmitExpression(iLProcessor, nodes[1]);
                EmitExpression(iLProcessor, nodes[2]);
                EmitExpression(iLProcessor, nodes[3]);
                iLProcessor.Emit(OpCodes.Call, stringConcat4Reference_);
                break;
            default:
                iLProcessor.Emit(OpCodes.Ldc_I4, nodes.Count);
                iLProcessor.Emit(OpCodes.Newarr, knownTypes_[TypeSymbol.String]);

                for (var i=0; i<nodes.Count; i++) {
                    iLProcessor.Emit(OpCodes.Dup);
                    iLProcessor.Emit(OpCodes.Ldc_I4, i);
                    EmitExpression(iLProcessor, nodes[i]);
                    iLProcessor.Emit(OpCodes.Stelem_Ref);
                }

                iLProcessor.Emit(OpCodes.Call, stringConcatArrayReference_);
                break;
        }

        // TODO: use similar logic for other data types and operators (e.g. 2 * x * 4 -> 8 * x)
        // (a + b) + (c + d) --> [a, b, c, d]
        static IEnumerable<BoundExpression> Flatten(BoundExpression node) {
            if (node is BoundBinaryExpression binaryExpression &&
                binaryExpression.op.opType == BoundBinaryOperatorType.Addition &&
                binaryExpression.left.typeClause.lType == TypeSymbol.String &&
                binaryExpression.right.typeClause.lType == TypeSymbol.String) {
                foreach (var result in Flatten(binaryExpression.left))
                    yield return result;

                foreach (var result in Flatten(binaryExpression.right))
                    yield return result;
            } else {
                if (node.typeClause.lType != TypeSymbol.String)
                    throw new Exception(
                        $"Flatten: unexpected node type in string concatenation '{node.typeClause.lType}'");

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
            iLProcessor.Emit(OpCodes.Initobj, GetType(expression.typeClause));
            return;
        }

        var expressionType = expression.typeClause.lType;

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
            throw new Exception($"EmitConstantExpression: unexpected constant expression type '{expressionType}'");
        }

        // TODO: this entire file is spaghetti code, need to rewrite with a better understanding of when to use
        // ldarg vs ldarga vs ldarga.s, newobj vs initobj vs call, etc.
        if (referenceAssign && handleAssignment) {
            iLProcessor.Emit(OpCodes.Newobj, GetNullableCtor(expression.typeClause));
            iLProcessor.Emit(OpCodes.Stobj, GetType(expression.typeClause));
        } else if (nullable && stack && handleAssignment) {
            iLProcessor.Emit(OpCodes.Newobj, GetNullableCtor(expression.typeClause));
        } else if (nullable && handleAssignment) {
            iLProcessor.Emit(OpCodes.Call, GetNullableCtor(expression.typeClause));
        }
    }

    private void EmitUnaryExpression(ILProcessor iLProcessor, BoundUnaryExpression expression) {
        EmitExpression(iLProcessor, expression.operand, nullable: false);

        if (expression.op.opType == BoundUnaryOperatorType.NumericalIdentity) {
        } else if (expression.op.opType == BoundUnaryOperatorType.NumericalNegation) {
            iLProcessor.Emit(OpCodes.Neg);
        } else if (expression.op.opType == BoundUnaryOperatorType.BooleanNegation) {
            iLProcessor.Emit(OpCodes.Ldc_I4_0);
            iLProcessor.Emit(OpCodes.Ceq);
        } else if (expression.op.opType == BoundUnaryOperatorType.BitwiseCompliment) {
            iLProcessor.Emit(OpCodes.Not);
        } else {
            throw new Exception($"EmitUnaryExpression: unexpected unary operator" +
                $"{SyntaxFacts.GetText(expression.op.type)}({expression.operand.typeClause.lType})");
        }
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
        // only used sometimes
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
