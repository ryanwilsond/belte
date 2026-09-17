using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ForEachEnumeratorInfo {
    internal readonly MethodSymbol getEnumeratorMethod;
    internal readonly MethodSymbol moveNextMethod;
    internal readonly MethodSymbol getCurrentMethod;
    internal readonly MethodSymbol disposeMethod;
    internal readonly MethodSymbol lengthOp;
    internal readonly MethodSymbol indexOp;
    internal readonly bool indexOpNeedsCast;
    internal readonly MethodSymbol iterOp;
    internal readonly BoundExpression start;
    internal readonly BoundExpression end;
    internal readonly bool inclusiveEnd;

    private ForEachEnumeratorInfo(
        MethodSymbol getEnumeratorMethod,
        MethodSymbol moveNextMethod,
        MethodSymbol getCurrentMethod,
        MethodSymbol disposeMethod,
        MethodSymbol lengthOp,
        MethodSymbol indexOp,
        bool indexOpNeedsCast,
        MethodSymbol iterOp,
        BoundExpression start,
        BoundExpression end,
        bool inclusiveEnd) {
        this.getEnumeratorMethod = getEnumeratorMethod;
        this.moveNextMethod = moveNextMethod;
        this.getCurrentMethod = getCurrentMethod;
        this.disposeMethod = disposeMethod;
        this.lengthOp = lengthOp;
        this.indexOp = indexOp;
        this.indexOpNeedsCast = indexOpNeedsCast;
        this.iterOp = iterOp;
        this.start = start;
        this.end = end;
        this.inclusiveEnd = inclusiveEnd;
    }

    internal static ForEachEnumeratorInfo CreateRangeInfo(
        BoundExpression start,
        BoundExpression end,
        bool inclusiveEnd) {
        return new ForEachEnumeratorInfo(
            null, null, null, null, null, null, false, null,
            start: start,
            end: end,
            inclusiveEnd: inclusiveEnd
        );
    }

    internal static ForEachEnumeratorInfo CreateIEnumerableInfo(
        MethodSymbol getEnumeratorMethod,
        MethodSymbol moveNextMethod,
        MethodSymbol getCurrentMethod,
        MethodSymbol disposeMethod) {
        return new ForEachEnumeratorInfo(
            getEnumeratorMethod: getEnumeratorMethod,
            moveNextMethod: moveNextMethod,
            getCurrentMethod: getCurrentMethod,
            disposeMethod: disposeMethod,
            null, null, false, null, null, null, false
        );
    }

    internal static ForEachEnumeratorInfo CreateLengthOpInfo(
        MethodSymbol lengthOp,
        MethodSymbol indexOp,
        bool indexOpNeedsCast) {
        return new ForEachEnumeratorInfo(
            null, null, null, null,
            lengthOp: lengthOp,
            indexOp: indexOp,
            indexOpNeedsCast: indexOpNeedsCast,
            null, null, null, false
        );
    }

    internal static ForEachEnumeratorInfo CreateEnumeratorInfo(
        MethodSymbol moveNextMethod,
        MethodSymbol currentMethod,
        MethodSymbol resetMethod) {
        return new ForEachEnumeratorInfo(
            null,
            moveNextMethod: moveNextMethod,
            getCurrentMethod: currentMethod,
            disposeMethod: resetMethod,
            null, null, false, null, null, null, false
        );
    }

    internal static ForEachEnumeratorInfo CreateIterOpInfo(MethodSymbol iterOp) {
        return new ForEachEnumeratorInfo(
            null, null, null, null, null, null, false,
            iterOp: iterOp,
            null, null, false
        );
    }
}
