using System;
using Buckle.CodeAnalysis.Binding;
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
                default:
                    throw ExceptionUtilities.UnexpectedValue(test);
            }
        }

        private BoundExpression MakeNullCheck(
            SyntaxNode syntax,
            BoundExpression rewrittenExpr,
            BinaryOperatorKind operatorKind) {
            if (rewrittenExpr.type.IsPointerOrFunctionPointer()) {
                TypeSymbol objectType = CorLibrary.GetSpecialType(SpecialType.Object);
                var operandType = new PointerTypeSymbol(
                    new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Void))
                );

                return Binary(syntax,
                    CreateCast(syntax,
                        operandType,
                        rewrittenExpr
                    ),
                    operatorKind,
                    CreateCast(syntax,
                        operandType,
                        Literal(syntax, null, objectType)
                    ),
                    CorLibrary.GetSpecialType(SpecialType.Bool)
                );
            }

            return (BoundExpression)_flowLowerer.Visit(Binary(syntax,
                rewrittenExpr,
                operatorKind,
                Literal(syntax, null, rewrittenExpr.type),
                CorLibrary.GetSpecialType(SpecialType.Bool)
            ));
        }

        private protected BoundExpression MakeValueTest(SyntaxNode syntax, BoundExpression input, ConstantValue value) {
            var comparisonType = input.type.EnumUnderlyingTypeOrSelf();
            var operatorType = Binder.RelationalOperatorType(comparisonType);
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

            BoundExpression literal = Literal(syntax, value, input.type);
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
