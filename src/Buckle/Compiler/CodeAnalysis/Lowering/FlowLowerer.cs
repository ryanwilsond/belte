using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class FlowLowerer : BoundTreeRewriter {
    private int _labelCount;

    internal static BoundStatement Lower(BoundStatement statement) {
        var lowerer = new FlowLowerer();
        return (BoundStatement)lowerer.Visit(statement);
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
                <step>;
            }
        }

        */
        var syntax = statement.syntax;
        var continueLabel = statement.continueLabel;
        var breakLabel = statement.breakLabel;
        // var condition = statement.condition.kind == BoundKind.EmptyExpression
        //     ? Literal(syntax, true)
        //     : statement.condition;
        var condition = statement.condition;

        return Visit(
            Block(syntax,
                statement.locals,
                statement.initializer,
                While(syntax,
                    statement.innerLocals,
                    condition,
                    Block(syntax,
                        statement.body,
                        Label(syntax, continueLabel),
                        Statement(syntax, statement.step)
                    ),
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

    private SynthesizedLabelSymbol GenerateLabel() {
        return new SynthesizedLabelSymbol($"Label{++_labelCount}");
    }
}
