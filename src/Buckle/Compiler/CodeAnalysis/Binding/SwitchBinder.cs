using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal class SwitchBinder : LocalScopeBinder {
    private static readonly object DefaultKey = new object();
    private static readonly object NullKey = new object();

    private protected readonly SwitchStatementSyntax _switchSyntax;
    private readonly SynthesizedLabelSymbol _breakLabel;
    private BoundExpression _switchGoverningExpression;
    private BelteDiagnosticQueue _switchGoverningDiagnostics;
    private Dictionary<object, SourceLabelSymbol> _lazySwitchLabelsMap;

    private SwitchBinder(Binder next, SwitchStatementSyntax switchSyntax)
        : base(next) {
        _switchSyntax = switchSyntax;
        _breakLabel = new SynthesizedLabelSymbol("break");
    }

    internal override bool isLocalFunctionsScopeBinder => true;

    internal override SynthesizedLabelSymbol breakLabel => _breakLabel;

    internal override bool isLabelsScopeBinder => true;

    private protected BoundExpression switchGoverningExpression {
        get {
            EnsureSwitchGoverningExpressionAndDiagnosticsBound();
            return _switchGoverningExpression;
        }
    }

    private protected BelteDiagnosticQueue switchGoverningDiagnostics {
        get {
            EnsureSwitchGoverningExpressionAndDiagnosticsBound();
            return _switchGoverningDiagnostics;
        }
    }

    private protected TypeSymbol switchGoverningType => switchGoverningExpression.type;

    private Dictionary<SyntaxNode, LabelSymbol> _labelsByNode;

    private protected Dictionary<SyntaxNode, LabelSymbol> labelsByNode {
        get {
            if (_labelsByNode is null) {
                var result = new Dictionary<SyntaxNode, LabelSymbol>();

                foreach (var label in labels) {
                    var node = ((SourceLabelSymbol)label).identifierNodeOrToken?.AsNode();

                    if (node is not null)
                        result.TryAdd(node, label);
                }

                _labelsByNode = result;
            }

            return _labelsByNode;
        }
    }

    private Dictionary<object, SourceLabelSymbol> labelsByValue {
        get {
            if (_lazySwitchLabelsMap == null && labels.Length > 0)
                _lazySwitchLabelsMap = BuildLabelsByValue(labels);

            return _lazySwitchLabelsMap;
        }
    }

    internal override SyntaxNode scopeDesignator => _switchSyntax;

    internal override ImmutableArray<DataContainerSymbol> GetDeclaredLocalsForScope(SyntaxNode scopeDesignator) {
        if (_switchSyntax == scopeDesignator)
            return locals;

        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<LocalFunctionSymbol> GetDeclaredLocalFunctionsForScope(
        BelteSyntaxNode scopeDesignator) {
        if (_switchSyntax == scopeDesignator)
            return localFunctions;

        throw ExceptionUtilities.Unreachable();
    }

    internal static SwitchBinder Create(Binder next, SwitchStatementSyntax switchSyntax) {
        return new SwitchBinder(next, switchSyntax);
    }

    private protected static object KeyForConstant(ConstantValue constantValue) {
        return constantValue is null ? NullKey : constantValue.value;
    }

    private static Dictionary<object, SourceLabelSymbol> BuildLabelsByValue(ImmutableArray<LabelSymbol> labels) {
        // TODO We don't use a custom comparer, but maybe we should?
        var map = new Dictionary<object, SourceLabelSymbol>(labels.Length);

        foreach (SourceLabelSymbol label in labels) {
            var labelKind = label.identifierNodeOrToken.kind;

            if (labelKind == SyntaxKind.IdentifierToken)
                continue;

            object key;
            var constantValue = label.switchCaseLabelConstant;

            if (constantValue is not null)
                key = KeyForConstant(constantValue);
            else if (labelKind == SyntaxKind.DefaultSwitchLabel)
                key = DefaultKey;
            else
                key = label.identifierNodeOrToken.AsNode();

            map.TryAdd(key, label);
        }

        return map;
    }

    private protected override ImmutableArray<DataContainerSymbol> BuildLocals() {
        var builder = ArrayBuilder<DataContainerSymbol>.GetInstance();

        foreach (var section in _switchSyntax.sections)
            builder.AddRange(BuildLocals(section.statements, GetBinder(section)));

        return builder.ToImmutableAndFree();
    }

    private protected override ImmutableArray<LocalFunctionSymbol> BuildLocalFunctions() {
        var builder = ArrayBuilder<LocalFunctionSymbol>.GetInstance();

        foreach (var section in _switchSyntax.sections)
            builder.AddRange(BuildLocalFunctions(section.statements));

        return builder.ToImmutableAndFree();
    }

    private protected override ImmutableArray<LabelSymbol> BuildLabels() {
        var labels = ArrayBuilder<LabelSymbol>.GetInstance();

        foreach (var section in _switchSyntax.sections) {
            BuildSwitchLabels(section.Labels, GetBinder(section), labels, BelteDiagnosticQueue.Discarded);
            BuildLabels(section.statements, ref labels);
        }

        return labels.ToImmutableAndFree();
    }

    private void BuildSwitchLabels(
        SyntaxList<SwitchLabelSyntax> labelsSyntax,
        Binder sectionBinder,
        ArrayBuilder<LabelSymbol> labels,
        BelteDiagnosticQueue tempDiagnosticBag) {
        foreach (var labelSyntax in labelsSyntax) {
            ConstantValue boundLabelConstant = null;

            switch (labelSyntax.kind) {
                case SyntaxKind.CaseSwitchLabel:
                    var caseLabel = (CaseSwitchLabelSyntax)labelSyntax;
                    var boundLabelExpression = sectionBinder.BindTypeOrRValueAllowingImplicitEnum(
                        caseLabel.value,
                        tempDiagnosticBag
                    );

                    if (boundLabelExpression is not BoundTypeExpression)
                        ConvertCaseExpression(labelSyntax, boundLabelExpression, out boundLabelConstant, tempDiagnosticBag);

                    labels.Add(new SourceLabelSymbol((MethodSymbol)containingMember, labelSyntax, boundLabelConstant));

                    break;
                case SyntaxKind.MultiCaseSwitchLabel:
                    var multiCaseLabel = (MultiCaseSwitchLabelSyntax)labelSyntax;

                    foreach (var value in multiCaseLabel.values) {
                        var boundValue = sectionBinder.BindTypeOrRValueAllowingImplicitEnum(value, tempDiagnosticBag);

                        if (boundValue is not BoundTypeExpression)
                            ConvertCaseExpression(labelSyntax, boundValue, out boundLabelConstant, tempDiagnosticBag);

                        labels.Add(new SourceLabelSymbol((MethodSymbol)containingMember, null, boundLabelConstant));
                    }

                    labels.Add(new SourceLabelSymbol((MethodSymbol)containingMember, multiCaseLabel));
                    break;
                default:
                    labels.Add(new SourceLabelSymbol((MethodSymbol)containingMember, labelSyntax, null));
                    break;
            }
        }
    }

    private protected BoundExpression ConvertCaseExpression(
        BelteSyntaxNode node,
        BoundExpression caseExpression,
        out ConstantValue constantValue,
        BelteDiagnosticQueue diagnostics,
        bool isGotoCaseExpr = false) {
        var hasErrors = false;

        if (isGotoCaseExpr) {
            var conversion = conversions.ClassifyConversionFromExpression(caseExpression, switchGoverningType);

            if (!conversion.isImplicit) {
                GenerateImplicitConversionError(diagnostics, node, conversion, caseExpression, switchGoverningType);
                hasErrors = true;
            }

            caseExpression = CreateConversion(caseExpression, conversion, switchGoverningType, diagnostics);
        }

        return ConvertPatternExpression(
            switchGoverningType,
            node,
            caseExpression,
            out constantValue,
            hasErrors,
            diagnostics,
            out _
        );
    }

    private void EnsureSwitchGoverningExpressionAndDiagnosticsBound() {
        if (_switchGoverningExpression is null) {
            var switchGoverningDiagnostics = BelteDiagnosticQueue.GetInstance();
            var boundSwitchExpression = BindSwitchGoverningExpression(switchGoverningDiagnostics);

            Interlocked.CompareExchange(ref _switchGoverningExpression, boundSwitchExpression, null);
            InterlockedOperations.Initialize(ref _switchGoverningDiagnostics, switchGoverningDiagnostics);
        }
    }
    private BoundExpression BindSwitchGoverningExpression(BelteDiagnosticQueue diagnostics) {
        var node = _switchSyntax.expression;
        var binder = GetBinder(node);

        var switchGoverningExpression = binder.BindRValueWithoutTargetType(node, diagnostics);
        var switchGoverningType = switchGoverningExpression.type;

        if (switchGoverningType is not null && !switchGoverningType.IsErrorType()) {
            if (switchGoverningType.IsValidSwitchType()) {
                return switchGoverningExpression;
            } else {
                var conversion = binder.conversions.ClassifyImplicitUserDefinedConversionForSwitchType(
                    switchGoverningType,
                    out var resultantGoverningType
                );

                if (conversion.exists) {
                    return binder.CreateConversion(
                        node,
                        switchGoverningExpression,
                        conversion,
                        isCast: false,
                        resultantGoverningType,
                        diagnostics
                    );
                } else if (!switchGoverningType.IsVoidType()) {
                    // TODO Patterns
                    // if (!PatternsEnabled)
                    diagnostics.Push(Error.SwitchTypeValueExpected(node.location));

                    return switchGoverningExpression;
                } else {
                    switchGoverningType = CreateErrorType(switchGoverningType.name);
                }
            }
        }

        if (!switchGoverningExpression.hasErrors)
            diagnostics.Push(Error.SwitchExpressionValueExpected(node.location, switchGoverningExpression));

        return new BoundErrorExpression(
            node,
            LookupResultKind.Empty,
            [],
            [switchGoverningExpression],
            switchGoverningType ?? CreateErrorType()
        );
    }

    private protected SourceLabelSymbol FindMatchingSwitchCaseLabel(
        ConstantValue constantValue,
        BelteSyntaxNode labelSyntax) {
        object key;

        if (constantValue is not null)
            key = KeyForConstant(constantValue);
        else
            key = labelSyntax;

        return FindMatchingSwitchLabel(key);
    }

    private SourceLabelSymbol GetDefaultLabel() {
        return FindMatchingSwitchLabel(DefaultKey);
    }

    private SourceLabelSymbol FindMatchingSwitchLabel(object key) {
        var labelsMap = labelsByValue;

        if (labelsMap is not null) {
            if (labelsMap.TryGetValue(key, out var label))
                return label;
        }

        return null;
    }

    internal BoundStatement BindGotoCaseOrDefault(
        GotoStatementSyntax node,
        Binder gotoBinder,
        BelteDiagnosticQueue diagnostics) {
        BoundExpression gotoCaseExpressionOpt = null;

        if (!node.containsDiagnostics) {
            ConstantValue gotoCaseExpressionConstant = null;
            var hasErrors = false;
            SourceLabelSymbol matchedLabelSymbol;

            if (node.value is not null) {
                gotoCaseExpressionOpt = gotoBinder.BindValue(node.value, diagnostics, BindValueKind.RValue);

                gotoCaseExpressionOpt = ConvertCaseExpression(
                    node,
                    gotoCaseExpressionOpt,
                    out gotoCaseExpressionConstant,
                    diagnostics,
                    isGotoCaseExpr: true
                );

                hasErrors = hasErrors || gotoCaseExpressionOpt.hasErrors;

                if (!hasErrors && gotoCaseExpressionConstant is null) {
                    diagnostics.Push(Error.ConstantExpected(node.location));
                    hasErrors = true;
                }

                matchedLabelSymbol = FindMatchingSwitchCaseLabel(gotoCaseExpressionConstant, node);
            } else {
                matchedLabelSymbol = GetDefaultLabel();
            }

            if (matchedLabelSymbol is null) {
                if (!hasErrors) {
                    var labelName = SyntaxFacts.GetText(node.caseOrDefaultKeyword.kind);

                    if (node.kind == SyntaxKind.GotoStatement)
                        labelName += " " + gotoCaseExpressionConstant.value?.ToString();

                    labelName += ":";

                    diagnostics.Push(Error.LabelNotFound(node.location, labelName));
                    hasErrors = true;
                }
            } else {
                return new BoundGotoStatement(node, matchedLabelSymbol, gotoCaseExpressionOpt, hasErrors);
            }
        }

        return new BoundErrorStatement(
            syntax: node,
            childBoundNodes: gotoCaseExpressionOpt is not null ? [gotoCaseExpressionOpt] : [],
            hasErrors: true
        );
    }

    internal override BoundStatement BindSwitchStatementCore(
        SwitchStatementSyntax node,
        Binder originalBinder,
        BelteDiagnosticQueue diagnostics) {
        var boundSwitchGoverningExpression = switchGoverningExpression;
        diagnostics.PushRange(switchGoverningDiagnostics);

        var switchSections = BindSwitchSections(originalBinder, diagnostics, out var defaultLabel);
        var locals = GetDeclaredLocalsForScope(node);
        var functions = GetDeclaredLocalFunctionsForScope(node);

        var decisionDag = DecisionDagBuilder.CreateDecisionDagForSwitchStatement(
            syntax: node,
            switchGoverningExpression: boundSwitchGoverningExpression,
            switchSections: switchSections,
            defaultLabel: defaultLabel?.label ?? breakLabel,
            diagnostics
        );

        CheckSwitchErrors(ref switchSections, decisionDag, diagnostics);

        decisionDag = decisionDag.SimplifyDecisionDagIfConstantInput(boundSwitchGoverningExpression);

        return new BoundSwitchStatement(
            syntax: node,
            expression: boundSwitchGoverningExpression,
            innerLocals: locals,
            innerLocalFunctions: functions,
            switchSections: switchSections,
            defaultLabel: defaultLabel,
            breakLabel: breakLabel,
            reachabilityDecisionDag: decisionDag
        );
    }

    private ImmutableArray<BoundSwitchSection> BindSwitchSections(
        Binder originalBinder,
        BelteDiagnosticQueue diagnostics,
        out BoundSwitchLabel defaultLabel) {
        var boundSwitchSectionsBuilder = ArrayBuilder<BoundSwitchSection>.GetInstance(_switchSyntax.sections.Count);
        defaultLabel = null;

        foreach (var sectionSyntax in _switchSyntax.sections) {
            var section = BindSwitchSection(sectionSyntax, originalBinder, ref defaultLabel, diagnostics);
            boundSwitchSectionsBuilder.Add(section);
        }

        return boundSwitchSectionsBuilder.ToImmutableAndFree();
    }

    private BoundSwitchSection BindSwitchSection(
        SwitchSectionSyntax node,
        Binder originalBinder,
        ref BoundSwitchLabel defaultLabel,
        BelteDiagnosticQueue diagnostics) {
        var boundLabelsBuilder = ArrayBuilder<BoundSwitchLabel>.GetInstance(node.Labels.Count);
        var sectionBinder = originalBinder.GetBinder(node);
        var labelsByNode = this.labelsByNode;

        foreach (var labelSyntax in node.Labels) {
            var label = labelsByNode[labelSyntax];
            var boundLabels = BindSwitchSectionLabels(sectionBinder, labelSyntax, label, ref defaultLabel, diagnostics);
            boundLabelsBuilder.AddRange(boundLabels);
        }

        var boundStatementsBuilder = ArrayBuilder<BoundStatement>.GetInstance(node.statements.Count);

        foreach (var statement in node.statements) {
            var boundStatement = sectionBinder.BindStatement(statement, diagnostics);

            // TODO Using error

            boundStatementsBuilder.Add(boundStatement);
        }

        boundStatementsBuilder.Add(new BoundBreakStatement(node, breakLabel));

        return new BoundSwitchSection(
            node,
            sectionBinder.GetDeclaredLocalsForScope(node),
            boundLabelsBuilder.ToImmutableAndFree(),
            boundStatementsBuilder.ToImmutableAndFree()
        );
    }

    private BoundSwitchLabel[] BindSwitchSectionLabels(
        Binder sectionBinder,
        SwitchLabelSyntax node,
        LabelSymbol label,
        ref BoundSwitchLabel defaultLabel,
        BelteDiagnosticQueue diagnostics) {
        switch (node.kind) {
            case SyntaxKind.CaseSwitchLabel: {
                    var caseLabelSyntax = (CaseSwitchLabelSyntax)node;
                    // TODO Do we have parse warnings?
                    var hasErrors = node.containsDiagnostics;

                    var pattern = sectionBinder.BindConstantPatternWithFallbackToTypePattern(
                        caseLabelSyntax.value,
                        caseLabelSyntax.value,
                        switchGoverningType,
                        hasErrors,
                        diagnostics
                    );

                    ReportIfConstantNamedUnderscore(pattern, caseLabelSyntax.value);

                    return [new BoundSwitchLabel(node, label, pattern, pattern.hasErrors)];
                }
            case SyntaxKind.DefaultSwitchLabel: {
                    var pattern = new BoundDiscardPattern(
                        node,
                        inputType: switchGoverningType,
                        narrowedType: switchGoverningType
                    );

                    var hasErrors = pattern.hasErrors;

                    if (defaultLabel is not null) {
                        diagnostics.Push(Error.DuplicateCaseLabel(node.location, label.name));
                        hasErrors = true;
                        return [new BoundSwitchLabel(node, label, pattern, hasErrors)];
                    } else {
                        return [defaultLabel = new BoundSwitchLabel(node, label, pattern, hasErrors)];
                    }
                }

            case SyntaxKind.MultiCaseSwitchLabel: {
                    var multiCase = (MultiCaseSwitchLabelSyntax)node;
                    var hasErrors = node.containsDiagnostics;
                    var builder = ArrayBuilder<BoundSwitchLabel>.GetInstance();

                    foreach (var value in multiCase.values) {
                        var pattern = sectionBinder.BindConstantPatternWithFallbackToTypePattern(
                            value,
                            value,
                            switchGoverningType,
                            hasErrors,
                            diagnostics
                        );

                        ReportIfConstantNamedUnderscore(pattern, value);

                        builder.Add(new BoundSwitchLabel(node, label, pattern, pattern.hasErrors));
                    }

                    return builder.ToArrayAndFree();
                }
            default:
                throw ExceptionUtilities.UnexpectedValue(node);
        }

        void ReportIfConstantNamedUnderscore(BoundPattern pattern, ExpressionSyntax expression) {
            // if (pattern is BoundConstantPattern { hasErrors: false } && IsUnderscore(expression)) {
            // TODO warning?
            // diagnostics.Add(ErrorCode.WRN_CaseConstantNamedUnderscore, expression.Location);
            // }
        }
    }

    private void CheckSwitchErrors(
        ref ImmutableArray<BoundSwitchSection> switchSections,
        BoundDecisionDag decisionDag,
        BelteDiagnosticQueue diagnostics) {
        var reachableLabels = decisionDag.reachableLabels;

        if (!switchSections.Any(static (s, reachableLabels)
                => s.switchLabels.Any(IsSubsumed, reachableLabels), reachableLabels)) {
            return;
        }

        var sectionBuilder = ArrayBuilder<BoundSwitchSection>.GetInstance(switchSections.Length);
        var anyPreviousErrors = false;

        foreach (var oldSection in switchSections) {
            var labelBuilder = ArrayBuilder<BoundSwitchLabel>.GetInstance(oldSection.switchLabels.Length);

            foreach (var label in oldSection.switchLabels) {
                var newLabel = label;

                if (!label.hasErrors && IsSubsumed(label, reachableLabels) &&
                    label.syntax.kind != SyntaxKind.DefaultSwitchLabel) {
                    var syntax = label.syntax;

                    switch (syntax.kind) {
                        case SyntaxKind.CaseSwitchLabel:
                        case SyntaxKind.MultiCaseSwitchLabel:
                            var location = (syntax.kind == SyntaxKind.CaseSwitchLabel)
                                ? ((CaseSwitchLabelSyntax)syntax).value.location
                                : ((MultiCaseSwitchLabelSyntax)syntax).location;

                            if (label.pattern is BoundConstantPattern cp && cp.constantValue is not null &&
                                FindMatchingSwitchCaseLabel(cp.constantValue, (BelteSyntaxNode)syntax) != label.label) {
                                diagnostics.Push(Error.DuplicateCaseLabel(
                                    syntax.location,
                                    DisplayText.FormatLiteral(cp.constantValue.value)
                                ));
                            } else if (!label.pattern.hasErrors && !anyPreviousErrors) {
                                diagnostics.Push(Error.SwitchCaseSubsumed(location));
                            }

                            break;
                        default:
                            throw ExceptionUtilities.UnexpectedValue(syntax.kind);
                    }

                    newLabel = new BoundSwitchLabel(label.syntax, label.label, label.pattern/*, label.WhenClause*/, hasErrors: true);
                }

                anyPreviousErrors |= label.hasErrors;
                labelBuilder.Add(newLabel);
            }

            sectionBuilder.Add(
                oldSection.Update(oldSection.locals, labelBuilder.ToImmutableAndFree(), oldSection.statements)
            );
        }

        switchSections = sectionBuilder.ToImmutableAndFree();

        static bool IsSubsumed(BoundSwitchLabel switchLabel, ImmutableHashSet<LabelSymbol> reachableLabels) {
            return !reachableLabels.Contains(switchLabel.label);
        }
    }
}
