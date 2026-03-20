using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class UnaryOperatorOverloadResolutionResult {
    internal static readonly ObjectPool<UnaryOperatorOverloadResolutionResult> Pool = CreatePool();

    internal readonly ArrayBuilder<UnaryOperatorAnalysisResult> results;

    internal UnaryOperatorOverloadResolutionResult() {
        results = new ArrayBuilder<UnaryOperatorAnalysisResult>(10);
    }

    internal bool AnyValid() {
        foreach (var result in results) {
            if (result.isValid)
                return true;
        }

        return false;
    }

    internal bool SingleValid() {
        var oneValid = false;

        foreach (var result in results) {
            if (result.isValid) {
                if (oneValid)
                    return false;

                oneValid = true;
            }
        }

        return oneValid;
    }

    internal UnaryOperatorAnalysisResult best {
        get {
            UnaryOperatorAnalysisResult best = default;
            foreach (var result in results) {
                if (result.isValid) {
                    if (best.isValid)
                        return default;

                    best = result;
                }
            }

            return best;
        }
    }

    internal static UnaryOperatorOverloadResolutionResult GetInstance() {
        return Pool.Allocate();
    }

    internal void Free() {
        results.Clear();
        Pool.Free(this);
    }

    private static ObjectPool<UnaryOperatorOverloadResolutionResult> CreatePool() {
        ObjectPool<UnaryOperatorOverloadResolutionResult> pool = null;

        pool = new ObjectPool<UnaryOperatorOverloadResolutionResult>(
            () => new UnaryOperatorOverloadResolutionResult(), 10
        );

        return pool;
    }
}
