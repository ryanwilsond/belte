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

    internal override BoundNode VisitNullBindingStatement(BoundNullBindingStatement node) {
        /*

        if (<source> -> <target>!)
            <body>
        <elseClause>

        ---->

        {
            var temp = <source>

            if (temp isnt null) {
                <target> = temp!
                <body>
            } <elseClause>
        }

        */
        var syntax = node.syntax;
        var temp = GenerateTempLocal(node.expression.Type());
        var condition = new BoundIsOperator(
            syntax,
            Local(syntax, temp),
            Literal(syntax, null, temp.type),
            true,
            null,
            CorLibrary.GetSpecialType(SpecialType.Bool)
        );

        return Visit(
            Block(syntax,
                node.locals,
                new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                    temp,
                    node.expression
                )),
                new BoundIfStatement(syntax,
                    condition,
                    Block(syntax,
                        new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                            node.valueLocal,
                            new BoundNullAssertOperator(syntax, Local(syntax, temp), true, null, node.valueLocal.type)
                        )),
                        node.consequence
                    ),
                    node.alternative
                )
            )
        );
    }

    internal override BoundNode VisitForEachStatement(BoundForEachStatement node) {
        /*

        for (<value>, <index> in <collection>)
            <body>

        ----> <collection> is array or string

        {
            var temp = <collection>
            var length = LowLevel.Length<>(temp)
            <index> = 0;

            for (; <index> < length; index++) {
                <value> = temp[<index>]
                <body>
            }
        }

        ----> <collection> defines length and [] operators

        {
            var temp = <collection>
            var length = temp.op_Length()
            <index> = 0;

            for (; <index> < length; index++) {
                <value> = temp.op_Index(<index>)
                <body>
            }
        }

        ----> <collection> defines iter operator

        {
            var temp = <collection>
            var iter = temp.op_Iter()
            <index> = 0;

            for (; iter.MoveNext(); index++) {
                <value> = iter.Current()
                <body>
            }
        }
        */
        var syntax = node.syntax;
        var type = node.expression.StrippedType();
        var isString = type.specialType == SpecialType.String;
        var isArray = type.IsArray();
        var iterOps = type.GetMembers(WellKnownMemberNames.IterOperatorName);
        var lengthOps = type.GetMembers(WellKnownMemberNames.LengthOperatorName);
        var bestIndexOp = type.GetMembers(WellKnownMemberNames.IndexOperatorName)
            .WhereAsArray(m => m is MethodSymbol e && e.GetParameterType(1).specialType == SpecialType.Int)
            .SingleOrDefault() as MethodSymbol;
        var worseIndexOp = type.GetMembers(WellKnownMemberNames.IndexOperatorName)
            .WhereAsArray(m => m is MethodSymbol e && e.GetParameterType(1).StrippedType().specialType == SpecialType.Int)
            .SingleOrDefault() as MethodSymbol;

        var index = node.indexLocal ?? GenerateTempLocal(CorLibrary.GetSpecialType(SpecialType.Int));
        var temp = GenerateTempLocal(type);
        var lengthOrIter = (isArray || isString || lengthOps.Any())
            ? GenerateTempLocal(CorLibrary.GetSpecialType(SpecialType.Int))
            : GenerateTempLocal(((MethodSymbol)iterOps[0]).returnType);

        var lengthOrIterInit = isArray
            ? Call(syntax,
                ((MethodSymbol)StandardLibrary.LowLevel.GetMembers("Length").Single())
                    .Construct([new TypeOrConstant(node.expression.type)]),
                Local(syntax, temp))
            : isString
                ? Call(syntax,
                    (MethodSymbol)StandardLibrary.String.GetMembers("Length").Single(),
                    Local(syntax, temp))
                : lengthOps.Any()
                    ? Call(syntax, (MethodSymbol)lengthOps[0], Local(syntax, temp))
                    : Call(syntax, (MethodSymbol)iterOps[0], Local(syntax, temp));

        BoundExpression condition = (isString || isArray || lengthOps.Any())
            ? Binary(syntax,
                Local(syntax, index),
                BinaryOperatorKind.IntLessThan,
                Local(syntax, lengthOrIter),
                CorLibrary.GetSpecialType(SpecialType.Bool))
            : InstanceCall(syntax,
                Local(syntax, lengthOrIter),
                (MethodSymbol)lengthOrIter.type.GetMembers("MoveNext").Single());

        BoundExpression indexer = isArray
            ? new BoundArrayAccessExpression(syntax,
                Local(syntax, temp),
                Local(syntax, index),
                null,
                node.valueLocal.type)
            : isString
                ? new BoundIndexerAccessExpression(syntax,
                    Local(syntax, temp),
                    Local(syntax, index),
                    null,
                    null,
                    node.valueLocal.type)
                : lengthOps.Any()
                    ? Call(syntax,
                        bestIndexOp ?? worseIndexOp,
                        Local(syntax, temp),
                        bestIndexOp is not null
                            ? Local(syntax, index)
                            : CreateCast(syntax,
                                CorLibrary.GetNullableType(SpecialType.Int),
                                Local(syntax, index)))
                    : InstanceCall(syntax,
                        Local(syntax, lengthOrIter),
                        (MethodSymbol)lengthOrIter.type.GetMembers("Current").Single());

        return Visit(
            Block(syntax,
                node.locals,
                new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                    temp,
                    node.expression.Type().IsNullableType()
                        ? new BoundNullAssertOperator(syntax, node.expression, true, null, temp.type)
                        : node.expression
                )),
                new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                    lengthOrIter,
                    lengthOrIterInit
                )),
                new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                    index,
                    Literal(syntax, 0L, index.type)
                )),
                new BoundForStatement(syntax,
                    [],
                    new BoundNopStatement(syntax),
                    [],
                    condition,
                    new BoundExpressionStatement(syntax, Increment(syntax, Local(syntax, index))),
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
