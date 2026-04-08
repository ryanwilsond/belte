using System.Collections.Generic;
using System.Linq;
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

    internal override BoundNode VisitForEachStatement(BoundForEachStatement node) {
        /*

        for (<value>, <index> in <collection>)
            <body>

        ---->

        {
            var temp = <collection>
            var length = LowLevel.Length<>(temp)
            <index> = 0;

            for (; <index> < length; index++) {
                <value> = temp[<index>]
                <body>
            }
        }

        */
        var syntax = node.syntax;
        var isString = node.expression.StrippedType().specialType == SpecialType.String;
        var temp = GenerateTempLocal(node.expression.type);
        var length = GenerateTempLocal(CorLibrary.GetSpecialType(SpecialType.Int));
        var lengthInit = isString
            ? Call(syntax, (MethodSymbol)StandardLibrary.String.GetMembers("Length").Single(), Local(syntax, temp))
            : Call(syntax,
                ((MethodSymbol)StandardLibrary.LowLevel.GetMembers("Length").Single())
                    .Construct([new TypeOrConstant(node.expression.type)]),
                Local(syntax, temp));
        BoundExpression indexer = isString
            ? new BoundIndexerAccessExpression(syntax,
                Local(syntax, temp),
                Local(syntax, node.indexLocal),
                null,
                null,
                node.valueLocal.type)
            : new BoundArrayAccessExpression(syntax,
                Local(syntax, temp),
                Local(syntax, node.indexLocal),
                null,
                node.valueLocal.type);

        return Visit(
            Block(syntax,
                node.locals,
                new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                    temp,
                    node.expression
                )),
                new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                    length,
                    lengthInit
                )),
                new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                    node.indexLocal,
                    Literal(syntax, 0, node.indexLocal.type)
                )),
                new BoundForStatement(syntax,
                    [],
                    new BoundNopStatement(syntax),
                    [],
                    Binary(syntax,
                        Local(syntax, node.indexLocal),
                        BinaryOperatorKind.IntLessThan,
                        Local(syntax, length),
                        CorLibrary.GetSpecialType(SpecialType.Bool)
                    ),
                    new BoundExpressionStatement(syntax, Increment(syntax, Local(syntax, node.indexLocal))),
                    Block(syntax,
                        new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                            node.valueLocal,
                            indexer
                        )),
                        node.body
                    ),
                    node.breakLabel,
                    node.continueLabel
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
