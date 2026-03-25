using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    private readonly struct AttributeExpressionVisitor {
        private readonly Binder _binder;

        internal AttributeExpressionVisitor(Binder binder) {
            _binder = binder;
        }

        internal ImmutableArray<TypedConstant> VisitArguments(
            ImmutableArray<BoundExpression> arguments,
            BelteDiagnosticQueue diagnostics,
            ref bool attrHasErrors,
            bool parentHasErrors = false) {
            var validatedArguments = ImmutableArray<TypedConstant>.Empty;

            var numArguments = arguments.Length;

            if (numArguments > 0) {
                var builder = ArrayBuilder<TypedConstant>.GetInstance(numArguments);
                foreach (var argument in arguments) {
                    var curArgumentHasErrors = parentHasErrors || argument.hasErrors;
                    builder.Add(VisitExpression(argument, diagnostics, ref attrHasErrors, curArgumentHasErrors));
                }

                validatedArguments = builder.ToImmutableAndFree();
            }

            return validatedArguments;
        }

        internal ImmutableArray<KeyValuePair<string, TypedConstant>> VisitNamedArguments(
            ImmutableArray<BoundAssignmentOperator> arguments,
            BelteDiagnosticQueue diagnostics,
            ref bool attrHasErrors) {
            ArrayBuilder<KeyValuePair<string, TypedConstant>>? builder = null;
            foreach (var argument in arguments) {
                var kv = VisitNamedArgument(argument, diagnostics, ref attrHasErrors);

                if (kv.HasValue) {
                    if (builder == null) {
                        builder = ArrayBuilder<KeyValuePair<string, TypedConstant>>.GetInstance();
                    }

                    builder.Add(kv.Value);
                }
            }

            if (builder is null)
                return [];

            return builder.ToImmutableAndFree();
        }

        private KeyValuePair<string, TypedConstant>? VisitNamedArgument(
            BoundAssignmentOperator assignment,
            BelteDiagnosticQueue diagnostics,
            ref bool attrHasErrors) {
            KeyValuePair<string, TypedConstant>? visitedArgument = null;

            switch (assignment.left.kind) {
                case BoundKind.FieldAccessExpression:
                    var fa = (BoundFieldAccessExpression)assignment.left;
                    visitedArgument = new KeyValuePair<string, TypedConstant>(
                        fa.field.name,
                        VisitExpression(assignment.right, diagnostics, ref attrHasErrors, assignment.hasErrors)
                    );

                    break;
            }

            return visitedArgument;
        }

        private TypedConstant VisitExpression(
            BoundExpression node,
            BelteDiagnosticQueue diagnostics,
            ref bool attrHasErrors,
            bool curArgumentHasErrors) {
            var typedConstantKind = node.type.GetAttributeParameterTypedConstantKind(_binder.compilation);
            return VisitExpression(
                node,
                typedConstantKind,
                diagnostics,
                ref attrHasErrors,
                curArgumentHasErrors || typedConstantKind == TypedConstantKind.Error
            );
        }

        private TypedConstant VisitExpression(
            BoundExpression node,
            TypedConstantKind typedConstantKind,
            BelteDiagnosticQueue diagnostics,
            ref bool attrHasErrors,
            bool curArgumentHasErrors) {
            var constantValue = node.constantValue;

            if (constantValue is not null) {
                // if (constantValue.isBad) {
                //     typedConstantKind = TypedConstantKind.Error;
                // }

                // ConstantValueUtils.CheckLangVersionForConstantValue(node, diagnostics);

                return CreateTypedConstant(
                    node,
                    typedConstantKind,
                    diagnostics,
                    ref attrHasErrors,
                    curArgumentHasErrors,
                    simpleValue: constantValue.value
                );
            }

            switch (node.kind) {
                case BoundKind.CastExpression:
                    return VisitConversion((BoundCastExpression)node, diagnostics, ref attrHasErrors, curArgumentHasErrors);
                case BoundKind.TypeOfExpression:
                    return VisitTypeOfExpression((BoundTypeOfExpression)node, diagnostics, ref attrHasErrors, curArgumentHasErrors);
                case BoundKind.ArrayCreationExpression:
                    return VisitArrayCreationExpression((BoundArrayCreationExpression)node, diagnostics, ref attrHasErrors, curArgumentHasErrors);
                default:
                    return CreateTypedConstant(node, TypedConstantKind.Error, diagnostics, ref attrHasErrors, curArgumentHasErrors);
            }
        }

        private TypedConstant VisitConversion(
            BoundCastExpression node,
            BelteDiagnosticQueue diagnostics,
            ref bool attrHasErrors,
            bool curArgumentHasErrors) {
            var type = node.type;
            var operand = node.operand;
            var operandType = operand.type;

            if (type is not null && operandType is not null) {
                if (type.specialType == SpecialType.Object ||
                    operandType.IsArray() && type.IsArray() &&
                    ((ArrayTypeSymbol)type).elementType.specialType == SpecialType.Object) {
                    var typedConstantKind = operandType.GetAttributeParameterTypedConstantKind(_binder.compilation);
                    return VisitExpression(operand, typedConstantKind, diagnostics, ref attrHasErrors, curArgumentHasErrors);
                }
            }

            return CreateTypedConstant(node, TypedConstantKind.Error, diagnostics, ref attrHasErrors, curArgumentHasErrors);
        }

        private static TypedConstant VisitTypeOfExpression(
            BoundTypeOfExpression node,
            BelteDiagnosticQueue diagnostics,
            ref bool attrHasErrors,
            bool curArgumentHasErrors) {
            var typeOfArgument = (TypeSymbol?)node.sourceType.type;

            if (typeOfArgument is not null) {
                var isValidArgument = true;

                switch (typeOfArgument.kind) {
                    case SymbolKind.TemplateParameter:
                        isValidArgument = false;
                        break;
                    default:
                        isValidArgument = typeOfArgument.IsUnboundTemplateType() ||
                            !typeOfArgument.ContainsTemplateParameter();
                        break;
                }

                if (!isValidArgument && !curArgumentHasErrors) {
                    // Binder.Error(diagnostics, ErrorCode.ERR_AttrArgWithTypeVars, node.Syntax, typeOfArgument.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
                    curArgumentHasErrors = true;
                    attrHasErrors = true;
                }
            }

            return CreateTypedConstant(
                node,
                TypedConstantKind.Type,
                diagnostics,
                ref attrHasErrors,
                curArgumentHasErrors,
                simpleValue: node.sourceType.type
            );
        }

        private TypedConstant VisitArrayCreationExpression(
            BoundArrayCreationExpression node,
            BelteDiagnosticQueue diagnostics,
            ref bool attrHasErrors,
            bool curArgumentHasErrors) {
            var type = (ArrayTypeSymbol)node.type;
            var typedConstantKind = type.GetAttributeParameterTypedConstantKind(_binder.compilation);

            ImmutableArray<TypedConstant> initializer;
            if (node.initializer is null) {
                initializer = [];
            } else {
                initializer = VisitArguments(
                    node.initializer.items,
                    diagnostics,
                    ref attrHasErrors,
                    curArgumentHasErrors
                );
            }

            return CreateTypedConstant(
                node,
                typedConstantKind,
                diagnostics,
                ref attrHasErrors,
                curArgumentHasErrors,
                arrayValue: initializer
            );
        }

        private static TypedConstant CreateTypedConstant(
            BoundExpression node,
            TypedConstantKind typedConstantKind,
            BelteDiagnosticQueue diagnostics,
            ref bool attrHasErrors,
            bool curArgumentHasErrors,
            object? simpleValue = null, ImmutableArray<TypedConstant> arrayValue = default) {
            var type = node.type;

            if (typedConstantKind != TypedConstantKind.Error && type.ContainsTemplateParameter())
                typedConstantKind = TypedConstantKind.Error;

            if (typedConstantKind == TypedConstantKind.Error) {
                if (!curArgumentHasErrors) {
                    // Binder.Error(diagnostics, ErrorCode.ERR_BadAttributeArgument, node.Syntax);
                    attrHasErrors = true;
                }

                return new TypedConstant(type, TypedConstantKind.Error, null);
            } else if (typedConstantKind == TypedConstantKind.Array) {
                return new TypedConstant(type, arrayValue);
            } else {
                return new TypedConstant(type, typedConstantKind, simpleValue);
            }
        }
    }
}
