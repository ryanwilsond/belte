using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Buckle.CodeAnalysis.Emitting {
    internal sealed class Emitter {
        public DiagnosticQueue diagnostics = new DiagnosticQueue();

        private readonly Dictionary<FunctionSymbol, MethodDefinition> methods_ =
            new Dictionary<FunctionSymbol, MethodDefinition>();
        private readonly AssemblyDefinition assemblyDefinition_;
        private readonly Dictionary<TypeSymbol, TypeReference> knownTypes_;
        private readonly MethodReference consoleWriteLineReference_;
        private readonly MethodReference consoleReadLineReference_;
        private readonly MethodReference stringConcatReference_;
        private readonly Dictionary<VariableSymbol, VariableDefinition> locals_ =
            new Dictionary<VariableSymbol, VariableDefinition>();
        private readonly List<(int instructionIndex, BoundLabel target)> fixups_ =
            new List<(int instructionIndex, BoundLabel target)>();
        private readonly Dictionary<BoundLabel, int> labels_ = new Dictionary<BoundLabel, int>();

        private TypeDefinition typeDefinition_;
        private MethodReference convertToBooleanReference_;
        private MethodReference convertToInt32Reference_;
        private MethodReference convertToStringReference_;
        private MethodReference objectEqualsReference_;

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

            consoleWriteLineReference_ = ResolveMethod("System.Console", "WriteLine", new [] { "System.String" });
            consoleReadLineReference_ = ResolveMethod("System.Console", "ReadLine", Array.Empty<string>());
            stringConcatReference_ = ResolveMethod(
                "System.String", "Concat", new [] { "System.String", "System.String" });
            convertToBooleanReference_ = ResolveMethod("System.Convert", "ToBoolean", new [] { "System.Object" });
            convertToInt32Reference_ = ResolveMethod("System.Convert", "ToInt32", new [] { "System.Object" });
            convertToStringReference_ = ResolveMethod("System.Convert", "ToString", new [] { "System.Object" });
            objectEqualsReference_ = ResolveMethod(
                "System.Object", "Equals", new [] { "System.Object", "System.Object" });
        }

        public DiagnosticQueue Emit(BoundProgram program, string outputPath) {
            if (diagnostics.Any())
                return diagnostics;

            var objectType = knownTypes_[TypeSymbol.Any];
            typeDefinition_ = new TypeDefinition(
                "", "Program", TypeAttributes.Abstract | TypeAttributes.Sealed, objectType);
            assemblyDefinition_.MainModule.Types.Add(typeDefinition_);

            foreach (var functionWithBody in program.functions)
                EmitFunctionDeclaration(functionWithBody.Key);

            foreach (var functionWithBody in program.functions)
                EmitFunctionBody(functionWithBody.Key, functionWithBody.Value);

            if (program.mainFunction != null)
                assemblyDefinition_.EntryPoint = methods_[program.mainFunction];

            assemblyDefinition_.Write(outputPath);

            return diagnostics;
        }

        private void EmitFunctionBody(FunctionSymbol function, BoundBlockStatement body) {
            var method = methods_[function];
            locals_.Clear();
            fixups_.Clear();
            labels_.Clear();
            var ilProcessor = method.Body.GetILProcessor();

            foreach (var statement in body.statements)
                EmitStatement(ilProcessor, statement);

            foreach (var fixup in fixups_) {
                var targetLabel = fixup.target;
                var targetInstructionIndex = labels_[targetLabel];
                var targetInstruction = ilProcessor.Body.Instructions[targetInstructionIndex];
                var instructionFix = ilProcessor.Body.Instructions[fixup.instructionIndex];
                instructionFix.Operand = targetInstruction;
            }

            method.Body.OptimizeMacros();
        }

        private void EmitStatement(ILProcessor ilProcessor, BoundStatement statement) {
            switch (statement.type) {
                case BoundNodeType.ExpressionStatement:
                    EmitExpressionStatement(ilProcessor, (BoundExpressionStatement)statement);
                    break;
                case BoundNodeType.VariableDeclarationStatement:
                    EmitVariableDeclarationStatement(ilProcessor, (BoundVariableDeclarationStatement)statement);
                    break;
                case BoundNodeType.GotoStatement:
                    EmitGotoStatement(ilProcessor, (BoundGotoStatement)statement);
                    break;
                case BoundNodeType.LabelStatement:
                    EmitLabelStatement(ilProcessor, (BoundLabelStatement)statement);
                    break;
                case BoundNodeType.ConditionalGotoStatement:
                    EmitConditionalGotoStatement(ilProcessor, (BoundConditionalGotoStatement)statement);
                    break;
                case BoundNodeType.ReturnStatement:
                    EmitReturnStatement(ilProcessor, (BoundReturnStatement)statement);
                    break;
                default:
                    diagnostics.Push(DiagnosticType.Fatal, $"unexpected node '{statement.type}'");
                    break;
            }
        }

        private void EmitReturnStatement(ILProcessor ilProcessor, BoundReturnStatement statement) {
            if (statement.expression != null)
                EmitExpression(ilProcessor, statement.expression);

            ilProcessor.Emit(OpCodes.Ret);
        }

        private void EmitConditionalGotoStatement(ILProcessor ilProcessor, BoundConditionalGotoStatement statement) {
            EmitExpression(ilProcessor, statement.condition);

            var opcode = statement.jumpIfTrue ? OpCodes.Brtrue : OpCodes.Brfalse;
            fixups_.Add((ilProcessor.Body.Instructions.Count, statement.label));
            ilProcessor.Emit(OpCodes.Br, Instruction.Create(OpCodes.Nop));
        }

        private void EmitLabelStatement(ILProcessor ilProcessor, BoundLabelStatement statement) {
            // TODO: fix JIT error
            labels_.Add(statement.label, ilProcessor.Body.Instructions.Count);
        }

        private void EmitGotoStatement(ILProcessor ilProcessor, BoundGotoStatement statement) {
            fixups_.Add((ilProcessor.Body.Instructions.Count, statement.label));
            ilProcessor.Emit(OpCodes.Br, Instruction.Create(OpCodes.Nop));
        }

        private void EmitVariableDeclarationStatement(
            ILProcessor ilProcessor, BoundVariableDeclarationStatement statement) {
            var typeReference = knownTypes_[statement.variable.lType];
            var variableDefinition = new VariableDefinition(typeReference);
            locals_.Add(statement.variable, variableDefinition);
            ilProcessor.Body.Variables.Add(variableDefinition);

            EmitExpression(ilProcessor, statement.initializer);
            ilProcessor.Emit(OpCodes.Stloc, variableDefinition);
        }

        private void EmitExpressionStatement(ILProcessor ilProcessor, BoundExpressionStatement statement) {
            EmitExpression(ilProcessor, statement.expression);

            if (statement.expression.lType != TypeSymbol.Void)
                ilProcessor.Emit(OpCodes.Pop);
        }

        private void EmitExpression(ILProcessor ilProcessor, BoundExpression expression) {
            switch (expression.type) {
                case BoundNodeType.UnaryExpression:
                    EmitUnaryExpression(ilProcessor, (BoundUnaryExpression)expression);
                    break;
                case BoundNodeType.LiteralExpression:
                    EmitLiteralExpression(ilProcessor, (BoundLiteralExpression)expression);
                    break;
                case BoundNodeType.BinaryExpression:
                    EmitBinaryExpression(ilProcessor, (BoundBinaryExpression)expression);
                    break;
                case BoundNodeType.VariableExpression:
                    EmitVariableExpression(ilProcessor, (BoundVariableExpression)expression);
                    break;
                case BoundNodeType.AssignmentExpression:
                    EmitAssignmentExpression(ilProcessor, (BoundAssignmentExpression)expression);
                    break;
                case BoundNodeType.EmptyExpression:
                    EmitEmptyExpression(ilProcessor, (BoundEmptyExpression)expression);
                    break;
                case BoundNodeType.CallExpression:
                    EmitCallExpression(ilProcessor, (BoundCallExpression)expression);
                    break;
                case BoundNodeType.CastExpression:
                    EmitCastExpression(ilProcessor, (BoundCastExpression)expression);
                    break;
                default:
                    diagnostics.Push(DiagnosticType.Fatal, $"unexpected node '{expression.type}'");
                    break;
            }
        }

        private void EmitCastExpression(ILProcessor ilProcessor, BoundCastExpression expression) {
            EmitExpression(ilProcessor, expression.expression);
            var needsBoxing = expression.expression.lType == TypeSymbol.Int ||
                expression.expression.lType == TypeSymbol.Bool;

            if (needsBoxing)
                ilProcessor.Emit(OpCodes.Box, knownTypes_[expression.expression.lType]);

            if (expression.lType == TypeSymbol.Any) {
            } else if (expression.lType == TypeSymbol.Bool) {
                ilProcessor.Emit(OpCodes.Call, convertToBooleanReference_);
            } else if (expression.lType == TypeSymbol.Int) {
                ilProcessor.Emit(OpCodes.Call, convertToInt32Reference_);
            } else if (expression.lType == TypeSymbol.String) {
                ilProcessor.Emit(OpCodes.Call, convertToStringReference_);
            } else {
                diagnostics.Push(DiagnosticType.Fatal,
                    $"unexpected cast from '{expression.expression.lType}' to '{expression.lType}'");
            }
        }

        private void EmitCallExpression(ILProcessor ilProcessor, BoundCallExpression expression) {
            foreach (var argument in expression.arguments)
                EmitExpression(ilProcessor, argument);

            if (expression.function == BuiltinFunctions.Print) {
                ilProcessor.Emit(OpCodes.Call, consoleWriteLineReference_);
            } else if (expression.function == BuiltinFunctions.Input) {
                ilProcessor.Emit(OpCodes.Call, consoleReadLineReference_);
            } else if (expression.function == BuiltinFunctions.Randint) {
                // TODO: implement
            } else {
                var methodDefinition = methods_[expression.function];
                ilProcessor.Emit(OpCodes.Call, methodDefinition);
            }
        }

        private void EmitEmptyExpression(ILProcessor ilProcessor, BoundEmptyExpression expression) {
        }

        private void EmitAssignmentExpression(ILProcessor ilProcessor, BoundAssignmentExpression expression) {
            var variableDefinition = locals_[expression.variable];
            EmitExpression(ilProcessor, expression.expression);
            ilProcessor.Emit(OpCodes.Dup);
            ilProcessor.Emit(OpCodes.Stloc, variableDefinition);
        }

        private void EmitVariableExpression(ILProcessor ilProcessor, BoundVariableExpression expression) {
            if (expression.variable is ParameterSymbol parameter) {
                ilProcessor.Emit(OpCodes.Ldarg, parameter.ordinal);
            } else {
                var variableDefinition = locals_[expression.variable];
                ilProcessor.Emit(OpCodes.Ldloc, variableDefinition);
            }
        }

        private void EmitBinaryExpression(ILProcessor ilProcessor, BoundBinaryExpression expression) {
            EmitExpression(ilProcessor, expression.left);
            EmitExpression(ilProcessor, expression.right);

            if (expression.op.opType == BoundBinaryOperatorType.EqualityEquals) {
                if (expression.left.lType == TypeSymbol.String && expression.right.lType == TypeSymbol.String ||
                    expression.left.lType == TypeSymbol.Any && expression.right.lType == TypeSymbol.Any) {
                    ilProcessor.Emit(OpCodes.Call, objectEqualsReference_);
                    return;
                }
            }

            if (expression.op.opType == BoundBinaryOperatorType.EqualityNotEquals) {
                if (expression.left.lType == TypeSymbol.String && expression.right.lType == TypeSymbol.String ||
                    expression.left.lType == TypeSymbol.Any && expression.right.lType == TypeSymbol.Any) {
                    ilProcessor.Emit(OpCodes.Call, objectEqualsReference_);
                    ilProcessor.Emit(OpCodes.Ldc_I4_0);
                    ilProcessor.Emit(OpCodes.Ceq);
                    return;
                }
            }

            if (expression.op.opType == BoundBinaryOperatorType.Addition) {
                if (expression.left.lType == TypeSymbol.String && expression.right.lType == TypeSymbol.String ||
                    expression.left.lType == TypeSymbol.Any && expression.right.lType == TypeSymbol.Any) {
                    ilProcessor.Emit(OpCodes.Call, stringConcatReference_);
                    return;
                }
            }

            switch (expression.op.opType) {
                case BoundBinaryOperatorType.Addition:
                    ilProcessor.Emit(OpCodes.Add);
                    break;
                case BoundBinaryOperatorType.Subtraction:
                    ilProcessor.Emit(OpCodes.Sub);
                    break;
                case BoundBinaryOperatorType.Multiplication:
                    ilProcessor.Emit(OpCodes.Mul);
                    break;
                case BoundBinaryOperatorType.Division:
                    ilProcessor.Emit(OpCodes.Div);
                    break;
                case BoundBinaryOperatorType.Power:
                    break;
                case BoundBinaryOperatorType.LogicalAnd:
                    // should wait to emit right if left is false
                    ilProcessor.Emit(OpCodes.And);
                    break;
                case BoundBinaryOperatorType.LogicalOr:
                    ilProcessor.Emit(OpCodes.Or);
                    break;
                case BoundBinaryOperatorType.LogicalXor:
                    ilProcessor.Emit(OpCodes.Xor);
                    break;
                case BoundBinaryOperatorType.LeftShift:
                    ilProcessor.Emit(OpCodes.Shl);
                    break;
                case BoundBinaryOperatorType.RightShift:
                    ilProcessor.Emit(OpCodes.Shr);
                    break;
                case BoundBinaryOperatorType.ConditionalAnd:
                    ilProcessor.Emit(OpCodes.And);
                    break;
                case BoundBinaryOperatorType.ConditionalOr:
                    ilProcessor.Emit(OpCodes.Or);
                    break;
                case BoundBinaryOperatorType.EqualityEquals:
                    ilProcessor.Emit(OpCodes.Ceq);
                    break;
                case BoundBinaryOperatorType.EqualityNotEquals:
                    ilProcessor.Emit(OpCodes.Ceq);
                    ilProcessor.Emit(OpCodes.Ldc_I4_0);
                    ilProcessor.Emit(OpCodes.Ceq);
                    break;
                case BoundBinaryOperatorType.LessThan:
                    ilProcessor.Emit(OpCodes.Clt);
                    break;
                case BoundBinaryOperatorType.GreaterThan:
                    ilProcessor.Emit(OpCodes.Cgt);
                    break;
                case BoundBinaryOperatorType.LessOrEqual:
                    ilProcessor.Emit(OpCodes.Cgt);
                    ilProcessor.Emit(OpCodes.Ldc_I4_0);
                    ilProcessor.Emit(OpCodes.Ceq);
                    break;
                case BoundBinaryOperatorType.GreatOrEqual:
                    ilProcessor.Emit(OpCodes.Clt);
                    ilProcessor.Emit(OpCodes.Ldc_I4_0);
                    ilProcessor.Emit(OpCodes.Ceq);
                    break;
                default:
                    diagnostics.Push(DiagnosticType.Fatal, $"unexpected binary operator" +
                    $"({expression.left.lType}){SyntaxFacts.GetText(expression.op.type)}({expression.right.lType})");
                    break;
            }
        }

        private void EmitLiteralExpression(ILProcessor ilProcessor, BoundLiteralExpression expression) {
            if (expression.lType == TypeSymbol.Int) {
                // for efficiency can add hardcoded constants e.g. Ldc_I4_0
                ilProcessor.Emit(OpCodes.Ldc_I4, (int)expression.value);
            } else if (expression.lType == TypeSymbol.String) {
                ilProcessor.Emit(OpCodes.Ldstr, (string)expression.value);
            } else if (expression.lType == TypeSymbol.Bool) {
                var value = (bool)expression.value;
                var instruction = value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
                ilProcessor.Emit(instruction);
            } else {
                diagnostics.Push(DiagnosticType.Fatal, $"unexpected literal '{expression.lType}'");
            }
        }

        private void EmitUnaryExpression(ILProcessor ilProcessor, BoundUnaryExpression expression) {
            EmitExpression(ilProcessor, expression.operand);

            if (expression.op.opType == BoundUnaryOperatorType.NumericalIdentity) {
            } else if (expression.op.opType == BoundUnaryOperatorType.NumericalNegation) {
                ilProcessor.Emit(OpCodes.Neg);
            } else if (expression.op.opType == BoundUnaryOperatorType.BooleanNegation) {
                ilProcessor.Emit(OpCodes.Ldc_I4_0);
                ilProcessor.Emit(OpCodes.Ceq);
            } else if (expression.op.opType == BoundUnaryOperatorType.BitwiseCompliment) {
                ilProcessor.Emit(OpCodes.Not);
            } else {
                diagnostics.Push(DiagnosticType.Fatal, $"unexpected unary operator" +
                    $"{SyntaxFacts.GetText(expression.op.type)}({expression.operand.lType})");
            }
        }

        private void EmitFunctionDeclaration(FunctionSymbol function) {
            var functionType = knownTypes_[function.lType];
            var method = new MethodDefinition(
                function.name, MethodAttributes.Static | MethodAttributes.Private, functionType);

            foreach (var parameter in function.parameters) {
                var parameterType = knownTypes_[parameter.lType];
                var parameterAttributes = ParameterAttributes.None;
                var parameterDefinition = new ParameterDefinition(parameter.name, parameterAttributes, parameterType);
                method.Parameters.Add(parameterDefinition);
            }

            typeDefinition_.Methods.Add(method);
            methods_.Add(function, method);
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
