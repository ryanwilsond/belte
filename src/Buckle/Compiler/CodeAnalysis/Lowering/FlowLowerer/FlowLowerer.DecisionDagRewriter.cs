using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class FlowLowerer {
    private abstract partial class DecisionDagRewriter : PatternLocalRewriter {
        private protected abstract ArrayBuilder<BoundStatement> BuilderForSection(SyntaxNode section);

        private ArrayBuilder<BoundStatement> _loweredDecisionDag;

        private readonly PooledDictionary<BoundDecisionDagNode, LabelSymbol> _dagNodeLabels
            = PooledDictionary<BoundDecisionDagNode, LabelSymbol>.GetInstance();

        private protected DecisionDagRewriter(
            SyntaxNode node,
            FlowLowerer flowLowerer,
            bool generateInstrumentation)
            : base(node, flowLowerer, generateInstrumentation) { }

        private void ComputeLabelSet(BoundDecisionDag decisionDag) {
            var hasPredecessor = PooledHashSet<BoundDecisionDagNode>.GetInstance();

            foreach (var node in decisionDag.topologicallySortedNodes) {
                switch (node) {
                    case BoundLeafDecisionDagNode d:
                        _dagNodeLabels[node] = d.label;
                        break;
                    case BoundEvaluationDecisionDagNode e:
                        NotePredecessor(e.next);
                        break;
                    case BoundTestDecisionDagNode p:
                        NotePredecessor(p.whenTrue);
                        NotePredecessor(p.whenFalse);
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(node.kind);
                }
            }

            hasPredecessor.Free();
            return;

            void NotePredecessor(BoundDecisionDagNode successor) {
                if (successor is not null && !hasPredecessor.Add(successor))
                    GetDagNodeLabel(successor);
            }
        }

        private protected new void Free() {
            _dagNodeLabels.Free();
            base.Free();
        }

        private protected virtual LabelSymbol GetDagNodeLabel(BoundDecisionDagNode dag) {
            if (!_dagNodeLabels.TryGetValue(dag, out var label)) {
                _dagNodeLabels.Add(dag, label = dag is BoundLeafDecisionDagNode d
                    ? d.label
                    : _flowLowerer.GenerateLabel("dagNode"));
            }

            return label;
        }

        private protected BoundDecisionDag ShareTempsIfPossibleAndEvaluateInput(
            BoundDecisionDag decisionDag,
            BoundExpression loweredSwitchGoverningExpression,
            ArrayBuilder<BoundStatement> result,
            out BoundExpression savedInputExpression) {
            decisionDag = ShareTempsAndEvaluateInput(
                loweredSwitchGoverningExpression,
                decisionDag,
                expr => result.Add(new BoundExpressionStatement(expr.syntax, expr)),
                out savedInputExpression
            );

            return decisionDag;
        }

        private protected ImmutableArray<BoundStatement> LowerDecisionDagCore(BoundDecisionDag decisionDag) {
            _loweredDecisionDag = ArrayBuilder<BoundStatement>.GetInstance();
            ComputeLabelSet(decisionDag);
            var sortedNodes = decisionDag.topologicallySortedNodes;
            var firstNode = sortedNodes[0];

            switch (firstNode) {
                case BoundLeafDecisionDagNode _:
                    _loweredDecisionDag.Add(new BoundGotoStatement(_node, GetDagNodeLabel(firstNode), null));
                    break;
            }

            // LowerWhenClauses(sortedNodes);

            var nodesToLower = sortedNodes.WhereAsArray(n => n.kind != BoundKind.LeafDecisionDagNode);
            var loweredNodes = PooledHashSet<BoundDecisionDagNode>.GetInstance();

            for (int i = 0, length = nodesToLower.Length; i < length; i++) {
                var node = nodesToLower[i];
                var alreadyLowered = loweredNodes.Contains(node);

                if (alreadyLowered && !_dagNodeLabels.TryGetValue(node, out _))
                    continue;

                if (_dagNodeLabels.TryGetValue(node, out var label))
                    _loweredDecisionDag.Add(new BoundLabelStatement(_node, label));

                if (!alreadyLowered && GenerateSwitchDispatch(node, loweredNodes))
                    continue;

                if (GenerateTypeTestAndCast(node, loweredNodes, nodesToLower, i))
                    continue;

                var nextNode = ((i + 1) < length) ? nodesToLower[i + 1] : null;

                if (nextNode is not null && loweredNodes.Contains(nextNode))
                    nextNode = null;

                LowerDecisionDagNode(node, nextNode);
            }

            loweredNodes.Free();
            var result = _loweredDecisionDag.ToImmutableAndFree();
            _loweredDecisionDag = null;
            return result;
        }

        private bool GenerateTypeTestAndCast(
            BoundDecisionDagNode node,
            HashSet<BoundDecisionDagNode> loweredNodes,
            ImmutableArray<BoundDecisionDagNode> nodesToLower,
            int indexOfNode) {
            if (node is BoundTestDecisionDagNode testNode &&
                testNode.whenTrue is BoundEvaluationDecisionDagNode evaluationNode &&
                TryLowerTypeTestAndCast(testNode.test, evaluationNode.evaluation, out var sideEffect, out var test)
                ) {
                var whenTrue = evaluationNode.next;
                var whenFalse = testNode.whenFalse;
                var canEliminateEvaluationNode = !_dagNodeLabels.ContainsKey(evaluationNode);

                if (canEliminateEvaluationNode)
                    loweredNodes.Add(evaluationNode);

                var nextNode =
                    (indexOfNode + 2 < nodesToLower.Length) &&
                    canEliminateEvaluationNode &&
                    nodesToLower[indexOfNode + 1] == evaluationNode &&
                    !loweredNodes.Contains(nodesToLower[indexOfNode + 2]) ? nodesToLower[indexOfNode + 2] : null;

                _loweredDecisionDag.Add(new BoundExpressionStatement(_node, sideEffect));
                GenerateTest(test, whenTrue, whenFalse, nextNode);
                return true;
            }

            return false;
        }

        private void GenerateTest(
            BoundExpression test,
            BoundDecisionDagNode whenTrue,
            BoundDecisionDagNode whenFalse,
            BoundDecisionDagNode nextNode) {
            if (nextNode == whenFalse) {
                _loweredDecisionDag.Add(new BoundConditionalGotoStatement(test.syntax, GetDagNodeLabel(whenTrue), test, jumpIfTrue: true));
            } else if (nextNode == whenTrue) {
                _loweredDecisionDag.Add(new BoundConditionalGotoStatement(test.syntax, GetDagNodeLabel(whenFalse), test, jumpIfTrue: false));
            } else {
                _loweredDecisionDag.Add(new BoundConditionalGotoStatement(test.syntax, GetDagNodeLabel(whenTrue), test, jumpIfTrue: true));
                _loweredDecisionDag.Add(new BoundGotoStatement(test.syntax, GetDagNodeLabel(whenFalse), null));
            }
        }

        private bool GenerateSwitchDispatch(BoundDecisionDagNode node, HashSet<BoundDecisionDagNode> loweredNodes) {
            if (!CanGenerateSwitchDispatch(node))
                return false;

            var input = ((BoundTestDecisionDagNode)node).test.input;
            var n = GatherValueDispatchNodes(node, loweredNodes, input);
            LowerValueDispatchNode(n, _tempAllocator.GetTemp(input));
            return true;

            bool CanGenerateSwitchDispatch(BoundDecisionDagNode node) {
                switch (node) {
                    case BoundTestDecisionDagNode { whenFalse: BoundTestDecisionDagNode test2 } test1:
                        return CanDispatch(test1, test2);
                    case BoundTestDecisionDagNode { whenTrue: BoundTestDecisionDagNode test2 } test1:
                        return CanDispatch(test1, test2);
                    default:
                        return false;
                }

                bool CanDispatch(BoundTestDecisionDagNode test1, BoundTestDecisionDagNode test2) {
                    if (_dagNodeLabels.ContainsKey(test2))
                        return false;

                    var t1 = test1.test;
                    var t2 = test2.test;

                    if (!(t1 is BoundDagValueTest))
                        return false;

                    if (!(t2 is BoundDagValueTest))
                        return false;

                    if (!t1.input.Equals(t2.input))
                        return false;

                    if (t1.input.type.specialType is SpecialType.Float64 or SpecialType.Float32)
                        return false;

                    return true;
                }
            }
        }

        private ValueDispatchNode GatherValueDispatchNodes(
            BoundDecisionDagNode node,
            HashSet<BoundDecisionDagNode> loweredNodes,
            BoundDagTemp input) {
            var fac = ValueSetFactory.ForInput(input);
            return GatherValueDispatchNodes(node, loweredNodes, input, fac);
        }

        private ValueDispatchNode GatherValueDispatchNodes(
            BoundDecisionDagNode node,
            HashSet<BoundDecisionDagNode> loweredNodes,
            BoundDagTemp input,
            IValueSetFactory fac) {
            if (loweredNodes.Contains(node)) {
                var foundLabel = _dagNodeLabels.TryGetValue(node, out var label);
                return new ValueDispatchNode.LeafDispatchNode(node.syntax, label);
            }

            if (!(node is BoundTestDecisionDagNode testNode && testNode.test.input.Equals(input))) {
                var label = GetDagNodeLabel(node);
                return new ValueDispatchNode.LeafDispatchNode(node.syntax, label);
            }

            switch (testNode.test) {
                case BoundDagValueTest value: {
                        loweredNodes.Add(testNode);
                        var cases = ArrayBuilder<(ConstantValue value, LabelSymbol label)>.GetInstance();
                        cases.Add((value: value.value, label: GetDagNodeLabel(testNode.whenTrue)));
                        var previous = testNode;

                        while (previous.whenFalse is BoundTestDecisionDagNode p &&
                            p.test is BoundDagValueTest vd &&
                            vd.input.Equals(input) &&
                            !_dagNodeLabels.ContainsKey(p) &&
                            !loweredNodes.Contains(p)) {
                            cases.Add((value: vd.value, label: GetDagNodeLabel(p.whenTrue)));
                            loweredNodes.Add(p);
                            previous = p;
                        }

                        var otherwise = GatherValueDispatchNodes(previous.whenFalse, loweredNodes, input, fac);
                        return PushEqualityTestsIntoTree(value.syntax, otherwise, cases.ToImmutableAndFree(), fac);
                    }
                default: {
                        var label = GetDagNodeLabel(node);
                        return new ValueDispatchNode.LeafDispatchNode(node.syntax, label);
                    }
            }
        }

        private ValueDispatchNode PushEqualityTestsIntoTree(
            SyntaxNode syntax,
            ValueDispatchNode otherwise,
            ImmutableArray<(ConstantValue value, LabelSymbol label)> cases,
            IValueSetFactory fac) {
            if (cases.IsEmpty)
                return otherwise;

            switch (otherwise) {
                case ValueDispatchNode.LeafDispatchNode leaf:
                    return new ValueDispatchNode.SwitchDispatch(syntax, cases, leaf.label);
                case ValueDispatchNode.SwitchDispatch sd:
                    return new ValueDispatchNode.SwitchDispatch(sd.syntax, sd.cases.Concat(cases), sd.otherwise);
                case ValueDispatchNode.RelationalDispatch { @operator: var op, value: var value, whenTrue: var whenTrue, whenFalse: var whenFalse } rel:
                    var (whenTrueCases, whenFalseCases) = SplitCases(cases, op, value);
                    whenTrue = PushEqualityTestsIntoTree(syntax, whenTrue, whenTrueCases, fac);
                    whenFalse = PushEqualityTestsIntoTree(syntax, whenFalse, whenFalseCases, fac);
                    var result = rel.WithTrueAndFalseChildren(whenTrue: whenTrue, whenFalse: whenFalse);
                    return result;
                default:
                    throw ExceptionUtilities.UnexpectedValue(otherwise);
            }

            (ImmutableArray<(ConstantValue value, LabelSymbol label)> whenTrueCases, ImmutableArray<(ConstantValue value, LabelSymbol label)> whenFalseCases)
                SplitCases(ImmutableArray<(ConstantValue value, LabelSymbol label)> cases, BinaryOperatorKind op, ConstantValue value) {
                var whenTrueBuilder = ArrayBuilder<(ConstantValue value, LabelSymbol label)>.GetInstance();
                var whenFalseBuilder = ArrayBuilder<(ConstantValue value, LabelSymbol label)>.GetInstance();
                op = op.Operator();
                foreach (var pair in cases) {
                    (fac.Related(op, pair.value, value) ? whenTrueBuilder : whenFalseBuilder).Add(pair);
                }

                return (whenTrueBuilder.ToImmutableAndFree(), whenFalseBuilder.ToImmutableAndFree());
            }
        }

        private void LowerValueDispatchNode(ValueDispatchNode n, BoundExpression input) {
            switch (n) {
                case ValueDispatchNode.LeafDispatchNode leaf:
                    _loweredDecisionDag.Add(new BoundGotoStatement(_node, leaf.label, null));
                    return;
                case ValueDispatchNode.SwitchDispatch eq:
                    LowerSwitchDispatchNode(eq, input);
                    return;
                case ValueDispatchNode.RelationalDispatch rel:
                    LowerRelationalDispatchNode(rel, input);
                    return;
                default:
                    throw ExceptionUtilities.UnexpectedValue(n);
            }
        }

        private void LowerRelationalDispatchNode(ValueDispatchNode.RelationalDispatch rel, BoundExpression input) {
            var test = MakeRelationalTest(rel.syntax, input, rel.@operator, rel.value);
            if (rel.whenTrue is ValueDispatchNode.LeafDispatchNode whenTrue) {
                var trueLabel = whenTrue.label;
                _loweredDecisionDag.Add(new BoundConditionalGotoStatement(_node, trueLabel, test, jumpIfTrue: true));
                LowerValueDispatchNode(rel.whenFalse, input);
            } else if (rel.whenFalse is ValueDispatchNode.LeafDispatchNode whenFalse) {
                var falseLabel = whenFalse.label;
                _loweredDecisionDag.Add(new BoundConditionalGotoStatement(_node, falseLabel, test, jumpIfTrue: false));
                LowerValueDispatchNode(rel.whenTrue, input);
            } else {
                LabelSymbol falseLabel = _flowLowerer.GenerateLabel("relationalDispatch");
                _loweredDecisionDag.Add(new BoundConditionalGotoStatement(_node, falseLabel, test, jumpIfTrue: false));
                LowerValueDispatchNode(rel.whenTrue, input);
                _loweredDecisionDag.Add(new BoundLabelStatement(_node, falseLabel));
                LowerValueDispatchNode(rel.whenFalse, input);
            }
        }

        private void LowerSwitchDispatchNode(ValueDispatchNode.SwitchDispatch node, BoundExpression input) {
            var defaultLabel = node.otherwise;

            if (input.type.IsValidSwitchType()) {
                var isStringInput = input.type.specialType == SpecialType.String;

                if (isStringInput)
                    EnsureStringHashFunction(node.cases.Length, node.syntax);

                var dispatch = new BoundSwitchDispatch(node.syntax, input, node.cases, defaultLabel);
                _loweredDecisionDag.Add(dispatch);
                // TODO nint/nuint
                // } else if (input.type.IsNativeIntegerType) {
                //     // Native types need to be dispatched using a larger underlying type so that any
                //     // possible high bits are not truncated.
                //     ImmutableArray<(ConstantValue value, LabelSymbol label)> cases;
                //     switch (input.Type.SpecialType) {
                //         case SpecialType.System_IntPtr: {
                //                 input = _factory.Convert(_factory.SpecialType(SpecialType.System_Int64), input);
                //                 cases = node.cases.SelectAsArray(p => (ConstantValue.Create((long)p.value.Int32Value), p.label));
                //                 break;
                //             }
                //         case SpecialType.System_UIntPtr: {
                //                 input = _factory.Convert(_factory.SpecialType(SpecialType.System_UInt64), input);
                //                 cases = node.cases.SelectAsArray(p => (ConstantValue.Create((ulong)p.value.UInt32Value), p.label));
                //                 break;
                //             }
                //         default:
                //             throw ExceptionUtilities.UnexpectedValue(input.Type);
                //     }

                //     var dispatch = new BoundSwitchDispatch(node.syntax, input, cases, defaultLabel, lengthBasedStringSwitchDataOpt: null);
                //     _loweredDecisionDag.Add(dispatch);
            } else {
                var lessThanOrEqualOperator = input.type.specialType switch {
                    SpecialType.Float32 => BinaryOperatorKind.Float32LessThanOrEqual,
                    SpecialType.Float64 => BinaryOperatorKind.Float64LessThanOrEqual,
                    SpecialType.Decimal => BinaryOperatorKind.Float64LessThanOrEqual,
                    _ => throw ExceptionUtilities.UnexpectedValue(input.type.specialType)
                };

                var cases = node.cases.Sort(new CasesComparer(input.type));
                LowerFloatDispatch(0, cases.Length);

                void LowerFloatDispatch(int firstIndex, int count) {
                    if (count <= 3) {
                        for (int i = firstIndex, limit = firstIndex + count; i < limit; i++) {
                            _loweredDecisionDag.Add(new BoundConditionalGotoStatement(
                                _node,
                                cases[i].label,
                                MakeValueTest(node.syntax, input, cases[i].value),
                                jumpIfTrue: true
                            ));
                        }

                        _loweredDecisionDag.Add(new BoundGotoStatement(_node, defaultLabel, null));
                    } else {
                        var half = count / 2;
                        var gt = _flowLowerer.GenerateLabel("greaterThanMidpoint");
                        _loweredDecisionDag.Add(new BoundConditionalGotoStatement(
                            _node,
                            gt,
                            MakeRelationalTest(node.syntax, input, lessThanOrEqualOperator, cases[firstIndex + half - 1].value),
                            jumpIfTrue: false
                        ));

                        LowerFloatDispatch(firstIndex, half);
                        _loweredDecisionDag.Add(new BoundLabelStatement(_node, gt));
                        LowerFloatDispatch(firstIndex + half, count - half);
                    }
                }
            }

            return;
        }

        private void EnsureStringHashFunction(int labelsCount, SyntaxNode syntaxNode) {
            // TODO String hash jump table (currently always string comparisons)
            // if (!CodeAnalysis.CodeGen.SwitchStringJumpTableEmitter.ShouldGenerateHashTableSwitch(labelsCount)) {
            //     return;
            // }

            // var privateImplClass = module.GetPrivateImplClass(syntaxNode, _localRewriter._diagnostics.DiagnosticBag);
            // if (privateImplClass.PrivateImplementationDetails.GetMethod(stringPatternInput switch {
            //     StringPatternInput.String => CodeAnalysis.CodeGen.PrivateImplementationDetails.SynthesizedStringHashFunctionName,
            //     StringPatternInput.SpanChar => CodeAnalysis.CodeGen.PrivateImplementationDetails.SynthesizedReadOnlySpanHashFunctionName,
            //     StringPatternInput.ReadOnlySpanChar => CodeAnalysis.CodeGen.PrivateImplementationDetails.SynthesizedSpanHashFunctionName,
            //     _ => throw ExceptionUtilities.UnexpectedValue(stringPatternInput),
            // }) != null) {
            //     return;
            // }

            // // cannot emit hash method if have no access to Chars.
            // var charsMember = stringPatternInput switch {
            //     StringPatternInput.String => _localRewriter._compilation.GetSpecialTypeMember(SpecialMember.System_String__Chars),
            //     StringPatternInput.SpanChar => _localRewriter._compilation.GetWellKnownTypeMember(WellKnownMember.System_Span_T__get_Item),
            //     StringPatternInput.ReadOnlySpanChar => _localRewriter._compilation.GetWellKnownTypeMember(WellKnownMember.System_ReadOnlySpan_T__get_Item),
            //     _ => throw ExceptionUtilities.UnexpectedValue(stringPatternInput),
            // };
            // if ((object)charsMember == null || charsMember.HasUseSiteError) {
            //     return;
            // }

            // TypeSymbol returnType = _factory.SpecialType(SpecialType.System_UInt32);
            // TypeSymbol paramType = stringPatternInput switch {
            //     StringPatternInput.String => _factory.SpecialType(SpecialType.System_String),
            //     StringPatternInput.SpanChar => _factory.WellKnownType(WellKnownType.System_Span_T)
            //         .Construct(_factory.SpecialType(SpecialType.System_Char)),
            //     StringPatternInput.ReadOnlySpanChar => _factory.WellKnownType(WellKnownType.System_ReadOnlySpan_T)
            //         .Construct(_factory.SpecialType(SpecialType.System_Char)),
            //     _ => throw ExceptionUtilities.UnexpectedValue(stringPatternInput),
            // };

            // SynthesizedGlobalMethodSymbol method = stringPatternInput switch {
            //     StringPatternInput.String => new SynthesizedStringSwitchHashMethod(privateImplClass, returnType, paramType),
            //     StringPatternInput.SpanChar => new SynthesizedSpanSwitchHashMethod(privateImplClass, returnType, paramType, isReadOnlySpan: false),
            //     StringPatternInput.ReadOnlySpanChar => new SynthesizedSpanSwitchHashMethod(privateImplClass, returnType, paramType, isReadOnlySpan: true),
            //     _ => throw ExceptionUtilities.UnexpectedValue(stringPatternInput),
            // };
            // privateImplClass.PrivateImplementationDetails.TryAddSynthesizedMethod(method.GetCciAdapter());
        }

        private void LowerDecisionDagNode(BoundDecisionDagNode node, BoundDecisionDagNode nextNode) {
            switch (node) {
                case BoundEvaluationDecisionDagNode evaluationNode: {
                        var sideEffect = LowerEvaluation(evaluationNode.evaluation);
                        _loweredDecisionDag.Add(new BoundExpressionStatement(node.syntax, sideEffect));

                        if (_generateInstrumentation)
                            _loweredDecisionDag.Add(BoundSequencePoint.CreateHidden());

                        if (nextNode != evaluationNode.next) {
                            _loweredDecisionDag.Add(
                                new BoundGotoStatement(node.syntax, GetDagNodeLabel(evaluationNode.next), null)
                            );
                        }
                    }

                    break;

                case BoundTestDecisionDagNode testNode: {
                        var test = base.LowerTest(testNode.test);
                        GenerateTest(test, testNode.whenTrue, testNode.whenFalse, nextNode);
                    }

                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(node.kind);
            }
        }
    }
}
