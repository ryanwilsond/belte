using System;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Libraries;
using Buckle.Utilities;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class FlowLowerer {
    private abstract partial class PatternLocalRewriter {
        private protected readonly SyntaxNode _node;
        private protected readonly FlowLowerer _flowLowerer;
        private protected readonly DagTempAllocator _tempAllocator;

        internal PatternLocalRewriter(SyntaxNode node, FlowLowerer flowLowerer, bool generateInstrumentation) {
            _node = node;
            _flowLowerer = flowLowerer;
            _generateInstrumentation = generateInstrumentation;
            _tempAllocator = new DagTempAllocator(flowLowerer, node);
        }

        private protected bool _generateInstrumentation { get; }

        internal void Free() {
            _tempAllocator.Free();
        }

        private protected BoundExpression LowerEvaluation(BoundDagEvaluation evaluation) {
            var input = _tempAllocator.GetTemp(evaluation.input);

            switch (evaluation) {
                case BoundDagTypeEvaluation t: {
                        var inputType = input.type;
                        var type = t.type;

                        var outputTemp = new BoundDagTemp(t.syntax, type, t, 0);
                        var output = _tempAllocator.GetTemp(outputTemp);
                        var conversion = new Conversions(null).ClassifyBuiltInConversion(inputType, output.type);

                        BoundExpression evaluated;

                        if (conversion.exists) {
                            if (conversion.kind == ConversionKind.ExplicitNullable &&
                                inputType.GetNullableUnderlyingType().Equals(output.type, TypeCompareKind.AllIgnoreOptions)) {
                                evaluated = Lowerer.CreateNullableGetValueCall(_node, input, inputType.GetNullableUnderlyingType());
                            } else {
                                evaluated = Cast(_node, type, input, conversion, null);
                            }
                        } else {
                            evaluated = new BoundAsOperator(
                                _node,
                                input,
                                new BoundTypeExpression(_node, new TypeWithAnnotations(type), null, type),
                                null,
                                null,
                                input.type
                            );
                        }

                        return Assignment(_node, output, evaluated, false, output.type);
                    }
                case BoundDagAssignmentEvaluation:
                default:
                    throw ExceptionUtilities.UnexpectedValue(evaluation);
            }
        }

        private protected BoundExpression LowerTest(BoundDagTest test) {
            var input = _tempAllocator.GetTemp(test.input);

            switch (test) {
                case BoundDagNonNullTest d:
                    return MakeNullCheck(d.syntax, input, input.type.IsNullableType()
                        ? BinaryOperatorKind.NullableNullNotEqual
                        : BinaryOperatorKind.NotEqual);
                case BoundDagExplicitNullTest d:
                    return MakeNullCheck(d.syntax, input, input.type.IsNullableType()
                        ? BinaryOperatorKind.NullableNullEqual
                        : BinaryOperatorKind.Equal);
                case BoundDagValueTest d:
                    return MakeValueTest(d.syntax, input, d.value);
                case BoundDagTypeTest d:
                    return new BoundIsOperator(
                        d.syntax,
                        input,
                        new BoundTypeExpression(d.syntax, new TypeWithAnnotations(d.type), null, d.type),
                        false,
                        null,
                        CorLibrary.GetSpecialType(SpecialType.Bool)
                    );
                default:
                    throw ExceptionUtilities.UnexpectedValue(test);
            }
        }

        private BoundExpression MakeNullCheck(
            SyntaxNode syntax,
            BoundExpression rewrittenExpr,
            BinaryOperatorKind operatorKind) {
            var isNot = operatorKind.Operator() == BinaryOperatorKind.NotEqual;

            if (rewrittenExpr.type.IsPointerOrFunctionPointer()) {
                TypeSymbol objectType = CorLibrary.GetSpecialType(SpecialType.Object);
                var operandType = new PointerTypeSymbol(
                    new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Void))
                );

                return new BoundIsOperator(syntax,
                    CreateCast(syntax,
                        operandType,
                        rewrittenExpr
                    ),
                    Literal(syntax, null, objectType),
                    isNot,
                    null,
                    CorLibrary.GetSpecialType(SpecialType.Bool)
                );
            }

            return (BoundExpression)_flowLowerer.Visit(new BoundIsOperator(syntax,
                rewrittenExpr,
                Literal(syntax, null, rewrittenExpr.type),
                isNot,
                null,
                CorLibrary.GetSpecialType(SpecialType.Bool)
            ));
        }

        private protected BoundExpression MakeValueTest(SyntaxNode syntax, BoundExpression input, ConstantValue value) {
            var comparisonType = input.type.EnumUnderlyingTypeOrSelf();
            var operatorType = Binder.RelationalOperatorType(comparisonType.StrippedType());
            var operatorKind = BinaryOperatorKind.Equal | operatorType;
            return MakeRelationalTest(syntax, input, operatorKind, value);
        }

        private protected BoundExpression MakeRelationalTest(SyntaxNode syntax, BoundExpression input, BinaryOperatorKind operatorKind, ConstantValue value) {
            if (input.type.specialType == SpecialType.Float64 && double.IsNaN((double)value.value) ||
                input.type.specialType == SpecialType.Float32 && float.IsNaN((float)value.value)) {
                // return _factory.MakeIsNotANumberTest(input);
                // TODO Number.IsNaN in stdlib or something
                throw ExceptionUtilities.Unreachable();
            }

            BoundExpression literal = Literal(syntax, value.value, input.type);
            var comparisonType = input.type.EnumUnderlyingTypeOrSelf();

            if (operatorKind.OperandTypes() == BinaryOperatorKind.Int &&
                comparisonType.specialType != SpecialType.Int32) {
                comparisonType = CorLibrary.GetSpecialType(SpecialType.Int32);
                input = CreateCast(syntax, comparisonType, input);
                literal = CreateCast(syntax, comparisonType, literal);
            }

            return (BoundExpression)_flowLowerer.Visit(Binary(syntax,
                input,
                operatorKind,
                literal,
                CorLibrary.GetSpecialType(SpecialType.Bool)
            ));
        }

        private protected bool TryLowerTypeTestAndCast(
            BoundDagTest test,
            BoundDagEvaluation evaluation,
            out BoundExpression sideEffect,
            out BoundExpression testExpression) {
            if (test is BoundDagTypeTest typeDecision &&
                evaluation is BoundDagTypeEvaluation typeEvaluation1 &&
                typeDecision.type.IsVerifierReference() &&
                typeEvaluation1.type.Equals(typeDecision.type, TypeCompareKind.AllIgnoreOptions) &&
                typeEvaluation1.input == typeDecision.input) {
                var input = _tempAllocator.GetTemp(test.input);
                var output = _tempAllocator.GetTemp(new BoundDagTemp(evaluation.syntax, typeEvaluation1.type, evaluation, 0));
                sideEffect = Assignment(
                    _node,
                    output,
                    new BoundAsOperator(
                        _node,
                        input,
                        new BoundTypeExpression(_node, new TypeWithAnnotations(typeEvaluation1.type), null, typeEvaluation1.type),
                        null,
                        null,
                        typeEvaluation1.type
                    ),
                    false,
                    output.type
                );

                testExpression = new BoundIsOperator(
                    _node,
                    output,
                    Literal(_node, null, output.type),
                    true,
                    null,
                    CorLibrary.GetSpecialType(SpecialType.Bool)
                );

                return true;
            }

            if (test is BoundDagNonNullTest nonNullTest &&
                evaluation is BoundDagTypeEvaluation typeEvaluation2 &&
                new Conversions(null).ClassifyBuiltInConversion(test.input.type, typeEvaluation2.type) is Conversion conv &&
                (conv.isIdentity || conv.kind == ConversionKind.ImplicitReference || conv.isBoxing) &&
                typeEvaluation2.input == nonNullTest.input) {
                var input = _tempAllocator.GetTemp(test.input);
                var baseType = typeEvaluation2.type;
                var output = _tempAllocator.GetTemp(new BoundDagTemp(evaluation.syntax, baseType, evaluation, 0));

                sideEffect = Assignment(
                    _node,
                    output,
                    CreateCast(_node, baseType, input),
                    false,
                    output.type
                );

                testExpression = new BoundIsOperator(
                    _node,
                    output,
                    Literal(_node, null, baseType),
                    true,
                    null,
                    CorLibrary.GetSpecialType(SpecialType.Bool)
                );

                return true;
            }

            sideEffect = testExpression = null;
            return false;
        }

        private protected BoundDecisionDag ShareTempsAndEvaluateInput(
            BoundExpression loweredInput,
            BoundDecisionDag decisionDag,
            Action<BoundExpression> addCode,
            out BoundExpression savedInputExpression) {
            var inputDagTemp = BoundDagTemp.ForOriginalInput(loweredInput);

            if ((loweredInput.kind == BoundKind.DataContainerExpression ||
                loweredInput.kind == BoundKind.ParameterExpression)
                && loweredInput.GetRefKind() == RefKind.None) {
                _tempAllocator.TrySetTemp(inputDagTemp, loweredInput);
            }

            var inputTemp = _tempAllocator.GetTemp(inputDagTemp);
            savedInputExpression = inputTemp;

            if (inputTemp != loweredInput)
                addCode(Assignment(_node, inputTemp, loweredInput, false, inputTemp.type));

            return decisionDag;
        }
    }
}
