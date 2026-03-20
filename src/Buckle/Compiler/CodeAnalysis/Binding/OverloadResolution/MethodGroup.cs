using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class MethodGroup {
    internal static readonly ObjectPool<MethodGroup> Pool = CreatePool();

    private MethodGroup() {
        methods = [];
        templateArguments = [];
    }

    internal string name => methods.Count > 0 ? methods[0].name : null;

    internal BoundExpression receiver { get; private set; }

    internal ArrayBuilder<MethodSymbol> methods { get; }

    internal ArrayBuilder<TypeOrConstant> templateArguments { get; }

    internal BelteDiagnostic error { get; private set; }

    internal LookupResultKind resultKind { get; private set; }

    internal BoundExpression instance {
        get {
            if (receiver is null)
                return null;

            if (receiver.kind == BoundKind.TypeExpression)
                return null;

            return receiver;
        }
    }

    internal void PopulateWithSingleMethod(
        BoundExpression receiverOpt,
        MethodSymbol method,
        LookupResultKind resultKind = LookupResultKind.Viable,
        BelteDiagnostic error = null) {
        PopulateHelper(receiverOpt, resultKind, error);
        methods.Add(method);
    }

    internal void PopulateWithNonExtensionMethods(
        BoundExpression receiverOpt,
        ImmutableArray<MethodSymbol> methods,
        ImmutableArray<TypeOrConstant> templateArguments,
        LookupResultKind resultKind = LookupResultKind.Viable,
        BelteDiagnostic error = null) {
        PopulateHelper(receiverOpt, resultKind, error);
        this.methods.AddRange(methods);

        if (!templateArguments.IsDefault)
            this.templateArguments.AddRange(templateArguments);
    }

    private void PopulateHelper(BoundExpression receiver, LookupResultKind resultKind, BelteDiagnostic error) {
        this.receiver = receiver;
        this.error = error;
        this.resultKind = resultKind;
    }

    internal void Clear() {
        receiver = null;
        methods.Clear();
        templateArguments.Clear();
        error = null;
        resultKind = LookupResultKind.Empty;
    }

    internal static MethodGroup GetInstance() {
        return Pool.Allocate();
    }

    internal void Free() {
        Clear();
        Pool.Free(this);
    }

    private static ObjectPool<MethodGroup> CreatePool() {
        ObjectPool<MethodGroup> pool = null;
        pool = new ObjectPool<MethodGroup>(() => new MethodGroup(), 10);
        return pool;
    }
}
