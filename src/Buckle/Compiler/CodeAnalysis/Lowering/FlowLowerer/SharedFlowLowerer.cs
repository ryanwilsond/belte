using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
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

        */

        // TODO Theres a lot of code duplicate here but I think its more readable this way that it was before
        // There is still room to cut out duplicate code especially for the final node creation step
        // Another improvement would be to store info from binding in ForEachEnumeratorInfo to avoid doing the same
        // lookup work again, currently we only use that for IEnumerable

        switch (node.forEachLoopKind) {
            case ForEachLoopKind.Array:
            case ForEachLoopKind.String:
                return VisitArrayOrStringForEach(node);
            case ForEachLoopKind.Enumerator:
                return VisitEnumeratorForEach(node);
            case ForEachLoopKind.Length:
                return VisitLengthForEach(node);
            case ForEachLoopKind.Iter:
                return VisitIterForEach(node);
            case ForEachLoopKind.IEnumerable:
                return VisitIEnumerableForEach(node);
            default:
                throw ExceptionUtilities.UnexpectedValue(node.kind);
        }
    }

    private BoundNode VisitArrayOrStringForEach(BoundForEachStatement node) {
        /*

        {
            var temp = <collection>
            var length = temp.Length
            <index> = 0;

            for (; <index> < length; <index>++) {
                <value> = temp[<index>]
                <body>
            }
        }

        */
        Debug.Assert(node.forEachLoopKind is ForEachLoopKind.Array or ForEachLoopKind.String);

        var syntax = node.syntax;
        var forEachLoopKind = node.forEachLoopKind;
        var type = node.expression.StrippedType();

        var isArray = forEachLoopKind == ForEachLoopKind.Array;

        var index = node.indexLocal ?? GenerateTempLocal(CorLibrary.GetSpecialType(SpecialType.Int));
        var temp = GenerateTempLocal(type);
        var lengthOrIter = GenerateTempLocal(CorLibrary.GetSpecialType(SpecialType.Int));

        BoundExpression lengthOrIterInit = isArray
            ? new BoundArrayLength(syntax, Local(syntax, temp), CorLibrary.GetSpecialType(SpecialType.Int))
            : Call(syntax,
                StandardLibrary.GetWellKnownMember(STLWellKnownMembers.String_Length),
                Local(syntax, temp));

        BoundExpression condition = Binary(syntax,
            Local(syntax, index),
            BinaryOperatorKind.Int64LessThan,
            Local(syntax, lengthOrIter),
            CorLibrary.GetSpecialType(SpecialType.Bool)
        );

        BoundExpression indexer = isArray
            ? new BoundArrayAccessExpression(syntax,
                Local(syntax, temp),
                Local(syntax, index),
                null,
                node.valueLocal.type)
            : new BoundIndexerAccessExpression(syntax,
                Local(syntax, temp),
                Local(syntax, index),
                null,
                null,
                node.valueLocal.type);

        return Visit(Block(syntax, node.locals, [
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
        ]));
    }

    private BoundNode VisitEnumeratorForEach(BoundForEachStatement node) {
        /*

        {
            var temp = <collection>
            temp.Reset();
            <index> = 0;

            for (; temp.MoveNext(); <index>++) {
                <value> = temp.Current()
                <body>
            }
        }

        */
        Debug.Assert(node.forEachLoopKind == ForEachLoopKind.Enumerator);

        var syntax = node.syntax;
        var type = node.expression.StrippedType();

        var index = node.indexLocal ?? GenerateTempLocal(CorLibrary.GetSpecialType(SpecialType.Int));
        var temp = GenerateTempLocal(type);
        var lengthOrIter = temp;

        BoundExpression condition = InstanceCall(syntax,
            Local(syntax, lengthOrIter),
            (MethodSymbol)lengthOrIter.type.GetMembers("MoveNext").Single());

        BoundExpression indexer = InstanceCall(syntax,
            Local(syntax, lengthOrIter),
            (MethodSymbol)lengthOrIter.type.GetMembers("Current").Single());

        return Visit(Block(syntax, node.locals, [
            new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                temp,
                node.expression.Type().IsNullableType()
                    ? new BoundNullAssertOperator(syntax, node.expression, true, null, temp.type)
                    : node.expression
            )),
            new BoundExpressionStatement(syntax, InstanceCall(syntax,
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

    private BoundNode VisitLengthForEach(BoundForEachStatement node) {
        /*

        {
            var temp = <collection>
            var length = temp.op_Length()
            <index> = 0;

            for (; <index> < length; <index>++) {
                <value> = temp.op_Index(<index>)
                <body>
            }
        }

        */
        Debug.Assert(node.forEachLoopKind == ForEachLoopKind.Length);

        var syntax = node.syntax;
        var kind = node.kind;
        var type = node.expression.StrippedType();

        var lengthOps = type.GetMembers(WellKnownMemberNames.LengthOperatorName);

        var bestIndexOp = type.GetMembers(WellKnownMemberNames.IndexOperatorName)
            .WhereAsArray(m => m is MethodSymbol e && e.GetParameterType(1).specialType == SpecialType.Int)
            .SingleOrDefault() as MethodSymbol;

        var worseIndexOp = type.GetMembers(WellKnownMemberNames.IndexOperatorName)
            .WhereAsArray(m => m is MethodSymbol e && e.GetParameterType(1).StrippedType().specialType == SpecialType.Int)
            .SingleOrDefault() as MethodSymbol;

        var index = node.indexLocal ?? GenerateTempLocal(CorLibrary.GetSpecialType(SpecialType.Int));
        var temp = GenerateTempLocal(type);
        var lengthOrIter = GenerateTempLocal(CorLibrary.GetSpecialType(SpecialType.Int));

        BoundExpression lengthOrIterInit = Call(syntax, (MethodSymbol)lengthOps[0], Local(syntax, temp));

        BoundExpression condition = Binary(syntax,
            Local(syntax, index),
            BinaryOperatorKind.Int64LessThan,
            Local(syntax, lengthOrIter),
            CorLibrary.GetSpecialType(SpecialType.Bool));

        BoundExpression indexer = Call(syntax,
            bestIndexOp ?? worseIndexOp,
            Local(syntax, temp),
            bestIndexOp is not null
                ? Local(syntax, index)
                : CreateCast(syntax,
                    CorLibrary.GetNullableType(SpecialType.Int),
                    Local(syntax, index)));

        return Visit(Block(syntax, node.locals, [
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
        ]));
    }

    private BoundNode VisitIterForEach(BoundForEachStatement node) {
        /*

        {
            var temp = <collection>
            var iter = temp.op_Iter()
            <index> = 0;

            for (; iter.MoveNext(); <index>++) {
                <value> = iter.Current()
                <body>
            }
        }

        */
        Debug.Assert(node.forEachLoopKind == ForEachLoopKind.Iter);

        var syntax = node.syntax;
        var type = node.expression.StrippedType();

        var iterOps = type.GetMembers(WellKnownMemberNames.IterOperatorName);

        var index = node.indexLocal ?? GenerateTempLocal(CorLibrary.GetSpecialType(SpecialType.Int));
        var temp = GenerateTempLocal(type);
        var lengthOrIter = GenerateTempLocal(((MethodSymbol)iterOps[0]).returnType);

        BoundExpression lengthOrIterInit = Call(syntax, (MethodSymbol)iterOps[0], Local(syntax, temp));

        BoundExpression condition = InstanceCall(syntax,
            Local(syntax, lengthOrIter),
            (MethodSymbol)lengthOrIter.type.GetMembers("MoveNext").Single());

        BoundExpression indexer = InstanceCall(syntax,
            Local(syntax, lengthOrIter),
            (MethodSymbol)lengthOrIter.type.GetMembers("Current").Single());

        return Visit(Block(syntax, node.locals, [
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
        ]));
    }

    private BoundNode VisitIEnumerableForEach(BoundForEachStatement node) {
        /*

        {
            var temp = <collection>
            var iter = temp.GetEnumerator()
            <index> = 0;
            defer iter.Dispose();

            for (; iter.MoveNext(); <index>++) {
                <value> = iter.get_Current()
                <body>
            }
        }

        */
        Debug.Assert(node.forEachLoopKind == ForEachLoopKind.IEnumerable);
        Debug.Assert(node.enumeratorInfo is not null);

        var syntax = node.syntax;
        var enumeratorInfo = node.enumeratorInfo;
        var type = node.expression.StrippedType();

        var index = node.indexLocal ?? GenerateTempLocal(CorLibrary.GetSpecialType(SpecialType.Int));
        var temp = GenerateTempLocal(type);
        var iter = GenerateTempLocal(enumeratorInfo.getEnumeratorMethod.returnType);

        var iterInit = InstanceCall(syntax, Local(syntax, temp), enumeratorInfo.getEnumeratorMethod);

        var condition = InstanceCall(syntax, Local(syntax, iter), enumeratorInfo.moveNextMethod);
        var indexer = InstanceCall(syntax, Local(syntax, iter), enumeratorInfo.getCurrentMethod);
        var dispose = InstanceCall(syntax, Local(syntax, iter), enumeratorInfo.disposeMethod);

        return Visit(Block(syntax, node.locals, [
            LocalDeclaration(syntax, temp, node.expression.Type().IsNullableType()
                    ? new BoundNullAssertOperator(syntax, node.expression, true, null, temp.type)
                    : node.expression),
            LocalDeclaration(syntax, iter, iterInit),
            LocalDeclaration(syntax, index, Literal(syntax, 0L, index.type)),
            new BoundDeferStatement(syntax, Statement(syntax, dispose)),
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
