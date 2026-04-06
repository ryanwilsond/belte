using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class FlowLowerer : BoundTreeRewriter {
    private readonly List<string> _localNames = [];
    private int _tempCount = 0;
    private MethodSymbol _container;

    private readonly BelteDiagnosticQueue _diagnostics;
    private int _labelCount;

    private FlowLowerer(MethodSymbol method, BelteDiagnosticQueue diagnostics) {
        _container = method;
        _diagnostics = diagnostics;
    }

    internal static BoundStatement Lower(
        MethodSymbol method,
        BoundStatement statement,
        BelteDiagnosticQueue diagnostics) {
        var lowerer = new FlowLowerer(method, diagnostics);
        return (BoundStatement)lowerer.Visit(statement);
    }

    internal override BoundNode VisitTryStatement(BoundTryStatement node) {
        if (node.finallyBody is not null) {
            foreach (var statement in ((BoundBlockStatement)node.finallyBody).statements) {
                if (statement.kind == BoundKind.ReturnStatement) {
                    _diagnostics.Push(Error.CannotReturnFromFinally(statement.syntax.location));
                    break;
                }
            }
        }

        return base.VisitTryStatement(node);
    }

    internal override BoundNode VisitIfStatement(BoundIfStatement statement) {
        /*

        if <condition>
            <then>

        ---->

        gotoFalse <condition> end
        <then>
        end:

        ==============================

        if <condition>
            <then>
        else
            <elseStatement>

        ---->

        gotoFalse <condition> else
        <then>
        goto end
        else:
        <elseStatement>
        end:

        */
        var syntax = statement.syntax;

        if (statement.alternative is null) {
            var endLabel = GenerateLabel();

            return VisitBlockStatement(
                Block(syntax,
                    GotoIfNot(syntax,
                        @goto: endLabel,
                        @ifNot: statement.condition
                    ),
                    statement.consequence,
                    Label(syntax, endLabel)
                )
            );
        } else {
            var elseLabel = GenerateLabel();
            var endLabel = GenerateLabel();

            return VisitBlockStatement(
                Block(syntax,
                    GotoIfNot(syntax,
                        @goto: elseLabel,
                        @ifNot: statement.condition
                    ),
                    statement.consequence,
                    Goto(syntax, endLabel),
                    Label(syntax, elseLabel),
                    statement.alternative,
                    Label(syntax, endLabel)
                )
            );
        }
    }

    internal override BoundNode VisitWhileStatement(BoundWhileStatement statement) {
        /*

        while <condition>
            <body>

        ---->

        continue:
        gotoFalse <condition> end
        <body>
        goto continue
        break:

        */
        var syntax = statement.syntax;
        var continueLabel = statement.continueLabel;
        var breakLabel = statement.breakLabel;

        return VisitBlockStatement(
            Block(syntax,
                statement.locals,
                Label(syntax, continueLabel),
                GotoIfNot(syntax,
                    @goto: breakLabel,
                    @ifNot: statement.condition
                ),
                statement.body,
                Goto(syntax, continueLabel),
                Label(syntax, breakLabel)
            )
        );
    }

    internal override BoundNode VisitDoWhileStatement(BoundDoWhileStatement statement) {
        /*

        do
            <body>
        while <condition>

        ---->

        continue:
        <body>
        gotoTrue <condition> continue
        break:

        */
        var syntax = statement.syntax;
        var continueLabel = statement.continueLabel;
        var breakLabel = statement.breakLabel;

        return VisitBlockStatement(
            Block(syntax,
                statement.locals,
                Label(syntax, continueLabel),
                statement.body,
                GotoIf(syntax,
                    @goto: continueLabel,
                    @if: statement.condition
                ),
                Label(syntax, breakLabel)
            )
        );
    }

    internal override BoundNode VisitForStatement(BoundForStatement statement) {
        /*

        for (<initializer> <condition>; <step>)
            <body>

        ---->

        {
            <initializer>
            while (<condition>) {
                <body>
            continue:
                <step>
            }
        }

        */
        var syntax = statement.syntax;
        var continueLabel = statement.continueLabel;
        var breakLabel = statement.breakLabel;
        var condition = statement.condition ?? Literal(syntax, true, CorLibrary.GetSpecialType(SpecialType.Bool));

        BoundStatement whileBlock;

        if (statement.step is null) {
            whileBlock = statement.body;
        } else {
            whileBlock = Block(
                syntax,
                statement.body,
                Label(syntax, continueLabel),
                statement.step
            );
        }

        return Visit(
            Block(syntax,
                statement.locals,
                statement.initializer,
                While(syntax,
                    statement.innerLocals,
                    condition,
                    whileBlock,
                    breakLabel,
                    GenerateLabel()
                )
            )
        );
    }

    internal override BoundNode VisitBreakStatement(BoundBreakStatement statement) {
        /*

        break;

        ---->

        goto <label>

        */
        return Goto(statement.syntax, statement.label);
    }

    internal override BoundNode VisitContinueStatement(BoundContinueStatement statement) {
        /*

        continue;

        ---->

        goto <label>

        */
        return Goto(statement.syntax, statement.label);
    }

    internal override BoundNode VisitSwitchStatement(BoundSwitchStatement node) {
        return SwitchStatementLocalRewriter.Rewrite(this, node);
    }

    private SynthesizedLabelSymbol GenerateLabel(string suffix = null) {
        return new SynthesizedLabelSymbol($"Label{++_labelCount}{suffix}");
    }

    private SynthesizedDataContainerSymbol GenerateTempLocal(TypeSymbol type) {
        string name;

        do {
            name = $"temp{_tempCount++}";
        } while (_localNames.Contains(name));

        return new SynthesizedDataContainerSymbol(
            _container,
            new TypeWithAnnotations(type),
            SynthesizedLocalKind.ExpanderTemp,
            name
        );
    }
}
