using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Lowering;

internal partial class SharedFlowLowerer : BoundTreeRewriterWithStackGuard {
    // TODO Tune
    private const int MaxUnroll = 64;

    private readonly List<string> _localNames = [];
    private int _tempCount = 0;
    private int _labelCount;

    private protected readonly Compilation _compilation;

    private protected SharedFlowLowerer(
        Compilation compilation,
        MethodSymbol method,
        BoundBlockStatement body) {
        _compilation = compilation;
        _container = method;
        _localNames.AddRange(body.locals.Select(l => l.name));
    }

    private protected MethodSymbol _container { get; set; }

    internal static BoundBlockStatement Lower(
        Compilation compilation,
        MethodSymbol method,
        BoundBlockStatement statement) {
        var lowerer = new SharedFlowLowerer(compilation, method, statement);
        return (BoundBlockStatement)lowerer.Visit(statement);
    }

    internal override BoundNode VisitForEachStatement(BoundForEachStatement node) {
        /*

        for (<value>, <index> in <collection>)
            <body>

        */

        // TODO Theres a lot of code duplicate here but I think its more readable this way that it was before
        // There is still room to cut out duplicate code especially for the final node creation step

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
            case ForEachLoopKind.Range:
                return VisitRangeForEach(node);
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
        Debug.Assert(node.enumeratorInfo is null);

        var syntax = node.syntax;
        var forEachLoopKind = node.forEachLoopKind;
        var type = node.expression.StrippedType();

        var isArray = forEachLoopKind == ForEachLoopKind.Array;

        var index = node.indexLocal ?? GenerateTempLocal(_compilation.GetSpecialType(SpecialType.Int));
        var temp = GenerateTempLocal(type);
        var lengthOrIter = GenerateTempLocal(_compilation.GetSpecialType(SpecialType.Int));

        BoundExpression lengthOrIterInit = isArray
            ? new BoundArrayLength(syntax, Local(syntax, temp), _compilation.GetSpecialType(SpecialType.Int))
            : Call(syntax,
                _compilation.standardLibrary.GetWellKnownMember(STLWellKnownMembers.String_Length),
                Local(syntax, temp));

        BoundExpression condition = Binary(syntax,
            Local(syntax, index),
            BinaryOperatorKind.Int64LessThan,
            Local(syntax, lengthOrIter),
            _compilation.GetSpecialType(SpecialType.Bool)
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
                Literal(_compilation, syntax, 0L, index.type)
            )),
            new BoundForStatement(syntax,
                [],
                new BoundNopStatement(syntax),
                [],
                condition,
                new BoundExpressionStatement(syntax, Increment(_compilation, syntax, Local(syntax, index))),
                node.unroll,
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
        Debug.Assert(node.enumeratorInfo is not null);
        Debug.Assert(node.enumeratorInfo.moveNextMethod is not null);
        Debug.Assert(node.enumeratorInfo.getCurrentMethod is not null);
        Debug.Assert(node.enumeratorInfo.disposeMethod is not null);

        var syntax = node.syntax;
        var type = node.expression.StrippedType();

        var index = node.indexLocal ?? GenerateTempLocal(_compilation.GetSpecialType(SpecialType.Int));
        var temp = GenerateTempLocal(type);
        var lengthOrIter = temp;

        BoundExpression condition = InstanceCall(syntax,
            Local(syntax, lengthOrIter),
            node.enumeratorInfo.moveNextMethod
        );

        BoundExpression indexer = InstanceCall(syntax,
            Local(syntax, lengthOrIter),
            node.enumeratorInfo.getCurrentMethod
        );

        return Visit(Block(syntax, node.locals, [
            new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                temp,
                node.expression.Type().IsNullableType()
                    ? new BoundNullAssertOperator(syntax, node.expression, true, null, temp.type)
                    : node.expression
            )),
            new BoundExpressionStatement(syntax, InstanceCall(syntax,
                Local(syntax, temp),
                node.enumeratorInfo.disposeMethod
            )),
            new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                index,
                Literal(_compilation, syntax, 0L, index.type)
            )),
            new BoundForStatement(syntax,
                [],
                new BoundNopStatement(syntax),
                [],
                condition,
                new BoundExpressionStatement(syntax, Increment(_compilation, syntax, Local(syntax, index))),
                node.unroll,
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
        Debug.Assert(node.enumeratorInfo is not null);
        Debug.Assert(node.enumeratorInfo.lengthOp is not null);
        Debug.Assert(node.enumeratorInfo.indexOp is not null);

        var syntax = node.syntax;
        var type = node.expression.StrippedType();

        var index = node.indexLocal ?? GenerateTempLocal(_compilation.GetSpecialType(SpecialType.Int));
        var temp = GenerateTempLocal(type);
        var lengthOrIter = GenerateTempLocal(_compilation.GetSpecialType(SpecialType.Int));

        BoundExpression lengthOrIterInit = Call(syntax, node.enumeratorInfo.lengthOp, Local(syntax, temp));

        BoundExpression condition = Binary(syntax,
            Local(syntax, index),
            BinaryOperatorKind.Int64LessThan,
            Local(syntax, lengthOrIter),
            _compilation.GetSpecialType(SpecialType.Bool));

        BoundExpression indexer = Call(syntax,
            node.enumeratorInfo.indexOp,
            Local(syntax, temp),
            !node.enumeratorInfo.indexOpNeedsCast
                ? Local(syntax, index)
                : CreateCast(syntax,
                    _compilation.corLibrary.GetNullableType(SpecialType.Int),
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
                Literal(_compilation, syntax, 0L, index.type)
            )),
            new BoundForStatement(syntax,
                [],
                new BoundNopStatement(syntax),
                [],
                condition,
                new BoundExpressionStatement(syntax, Increment(_compilation, syntax, Local(syntax, index))),
                node.unroll,
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
        Debug.Assert(node.enumeratorInfo is not null);
        Debug.Assert(node.enumeratorInfo.iterOp is not null);

        var syntax = node.syntax;
        var type = node.expression.StrippedType();

        var index = node.indexLocal ?? GenerateTempLocal(_compilation.GetSpecialType(SpecialType.Int));
        var temp = GenerateTempLocal(type);
        var lengthOrIter = GenerateTempLocal(node.enumeratorInfo.iterOp.returnType);

        BoundExpression lengthOrIterInit = Call(syntax, node.enumeratorInfo.iterOp, Local(syntax, temp));

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
                Literal(_compilation, syntax, 0L, index.type)
            )),
            new BoundForStatement(syntax,
                [],
                new BoundNopStatement(syntax),
                [],
                condition,
                new BoundExpressionStatement(syntax, Increment(_compilation, syntax, Local(syntax, index))),
                node.unroll,
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
        Debug.Assert(node.enumeratorInfo.getEnumeratorMethod is not null);
        Debug.Assert(node.enumeratorInfo.moveNextMethod is not null);
        Debug.Assert(node.enumeratorInfo.getCurrentMethod is not null);

        var syntax = node.syntax;
        var enumeratorInfo = node.enumeratorInfo;
        var type = node.expression.StrippedType();

        var index = node.indexLocal ?? GenerateTempLocal(_compilation.GetSpecialType(SpecialType.Int));
        var temp = GenerateTempLocal(type);
        var iter = GenerateTempLocal(enumeratorInfo.getEnumeratorMethod.returnType);

        var iterInit = InstanceCall(syntax, Local(syntax, temp), enumeratorInfo.getEnumeratorMethod);

        var condition = InstanceCall(syntax, Local(syntax, iter), enumeratorInfo.moveNextMethod);
        var indexer = InstanceCall(syntax, Local(syntax, iter), enumeratorInfo.getCurrentMethod);

        BoundStatement deferDispose = enumeratorInfo.disposeMethod is null
            ? new BoundNopStatement(syntax)
            : new BoundDeferStatement(
                syntax,
                Statement(syntax, InstanceCall(syntax, Local(syntax, iter), enumeratorInfo.disposeMethod))
              );

        return Visit(Block(syntax, node.locals, [
            LocalDeclaration(syntax, temp, node.expression.Type().IsNullableType()
                    ? new BoundNullAssertOperator(syntax, node.expression, true, null, temp.type)
                    : node.expression),
            LocalDeclaration(syntax, iter, iterInit),
            LocalDeclaration(syntax, index, Literal(_compilation, syntax, 0L, index.type)),
            deferDispose,
            new BoundForStatement(syntax,
                [],
                new BoundNopStatement(syntax),
                [],
                condition,
                new BoundExpressionStatement(syntax, Increment(_compilation, syntax, Local(syntax, index))),
                node.unroll,
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

    private BoundNode VisitRangeForEach(BoundForEachStatement node) {
        /*

        {
            for (int i = start; i < end; i++) {
                <value> = i
                <body>
            }
        }

        */
        Debug.Assert(node.forEachLoopKind == ForEachLoopKind.Range);
        Debug.Assert(node.enumeratorInfo is not null);
        Debug.Assert(node.enumeratorInfo.start is not null);
        Debug.Assert(node.enumeratorInfo.end is not null);

        var shouldUnroll = node.unroll &&
            (long)node.enumeratorInfo.end.constantValue.value -
            (long)node.enumeratorInfo.start.constantValue.value <= MaxUnroll;

        if (shouldUnroll)
            return UnrollForEach(node);

        var syntax = node.syntax;

        var i = GenerateTempLocal(node.enumeratorInfo.start.type);

        var condition = Binary(
            syntax,
            Local(syntax, i),
            node.enumeratorInfo.inclusiveEnd
                ? BinaryOperatorKind.Int64LessThanOrEqual
                : BinaryOperatorKind.Int64LessThan,
            node.enumeratorInfo.end,
            _compilation.GetSpecialType(SpecialType.Bool)
        );

        return Visit(Block(syntax, node.locals, [
            new BoundForStatement(syntax,
                [i],
                LocalDeclaration(syntax, i, node.enumeratorInfo.start),
                [],
                condition,
                new BoundExpressionStatement(syntax, Increment(_compilation, syntax, Local(syntax, i))),
                node.unroll,
                Block(syntax,
                    new BoundLocalDeclarationStatement(syntax, new BoundDataContainerDeclaration(syntax,
                        node.valueLocal,
                        Local(syntax, i)
                    )),
                    node.body
                ),
                node.breakLabel,
                node.continueLabel
            )
        ]));
    }

    private BoundNode UnrollForEach(BoundForEachStatement node) {
        /*

        {
            {
                <value> = <start>
                <body>
            }
            {
                <value> = <start> + 1
                <body>
            }
            ...
        }

        */
        // TODO Could do some optimization here
        Debug.Assert(node.forEachLoopKind == ForEachLoopKind.Range);

        var syntax = node.syntax;
        var info = node.enumeratorInfo;

        var start = (long)info.start.constantValue.value;
        var end = (long)info.end.constantValue.value;

        if (info.inclusiveEnd)
            end++;

        var statements = ArrayBuilder<BoundStatement>.GetInstance((int)(start - end));
        var valueType = node.valueLocal.type;

        for (var ind = start; ind < end; ind++) {
            statements.Add(Block(syntax, node.innerLocals, [
                Statement(syntax, Assignment(syntax,
                    Local(syntax, node.valueLocal),
                    Literal(_compilation, syntax, ind, valueType),
                    false,
                    valueType
                )),
                node.body
            ]));
        }

        return Visit(Block(syntax, node.locals, statements.ToArrayAndFree()));
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
