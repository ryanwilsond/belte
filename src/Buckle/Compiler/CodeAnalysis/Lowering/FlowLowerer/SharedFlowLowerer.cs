using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

internal partial class SharedFlowLowerer : BoundTreeRewriterWithStackGuard {
    private readonly List<string> _localNames = [];
    private int _tempCount = 0;
    private int _labelCount;

    private protected readonly BelteDiagnosticQueue _diagnostics;

    private protected SharedFlowLowerer(
        MethodSymbol method,
        BoundBlockStatement body,
        BelteDiagnosticQueue diagnostics) {
        _container = method;
        _diagnostics = diagnostics;
        _localNames.AddRange(body.locals.Select(l => l.name));
    }

    private protected MethodSymbol _container { get; set; }

    internal static BoundBlockStatement Lower(
        MethodSymbol method,
        BoundBlockStatement statement,
        BelteDiagnosticQueue diagnostics) {
        var lowerer = new SharedFlowLowerer(method, statement, diagnostics);
        return (BoundBlockStatement)lowerer.Visit(statement);
    }

    internal override BoundNode VisitForEachStatement(BoundForEachStatement node) {
        /*

        for (<value>, <index> in <collection>)
            <body>

        ----> <collection> is array or string

        {
            var temp = <collection>
            var length = temp.Length
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
        var isEnumerator = type.originalDefinition.Equals(CorLibrary.GetWellKnownType(WellKnownType.Enumerator));
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
        var lengthOrIter = isEnumerator ? temp : (isArray || isString || lengthOps.Any())
            ? GenerateTempLocal(CorLibrary.GetSpecialType(SpecialType.Int))
            : GenerateTempLocal(((MethodSymbol)iterOps[0]).returnType);

        BoundExpression lengthOrIterInit = isEnumerator ? null : isArray
            ? new BoundArrayLength(syntax, Local(syntax, temp), CorLibrary.GetSpecialType(SpecialType.Int))
            : isString
                ? Call(syntax,
                    StandardLibrary.GetWellKnownMember(STLWellKnownMembers.String_Length),
                    Local(syntax, temp))
                : lengthOps.Any()
                    ? Call(syntax, (MethodSymbol)lengthOps[0], Local(syntax, temp))
                    : Call(syntax, (MethodSymbol)iterOps[0], Local(syntax, temp));

        BoundExpression condition = (isString || isArray || lengthOps.Any())
            ? Binary(syntax,
                Local(syntax, index),
                BinaryOperatorKind.Int64LessThan,
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

        return Visit(Block(syntax, node.locals, [
            new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                temp,
                node.expression.Type().IsNullableType()
                    ? new BoundNullAssertOperator(syntax, node.expression, true, null, temp.type)
                    : node.expression
            )),
            !isEnumerator ? new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                lengthOrIter,
                lengthOrIterInit
            )) : new BoundExpressionStatement(syntax, InstanceCall(syntax,
                Local(syntax, temp),
                (MethodSymbol)type.GetMembers("Reset").Single()
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
        ]));
    }

    internal override BoundNode VisitLocalFunctionStatement(BoundLocalFunctionStatement node) {
        var oldContainer = _container;
        _container = node.symbol;
        var newNode = base.VisitLocalFunctionStatement(node);
        _container = oldContainer;
        return newNode;
    }

    private protected SynthesizedLabelSymbol GenerateLabel(string suffix = null) {
        return new SynthesizedLabelSymbol($"Label{++_labelCount}{suffix}");
    }

    private protected SynthesizedDataContainerSymbol GenerateTempLocal(TypeSymbol type) {
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
