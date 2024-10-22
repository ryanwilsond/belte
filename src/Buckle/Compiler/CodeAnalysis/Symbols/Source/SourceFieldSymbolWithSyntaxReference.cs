using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceFieldSymbolWithSyntaxReference : SourceFieldSymbol {
    private ConstantValue _lazyConstantValue = ConstantValue.Unset;

    private protected SourceFieldSymbolWithSyntaxReference(
        SourceMemberContainerTypeSymbol containingType,
        string name,
        SyntaxReference syntaxReference)
        : base(containingType) {
        this.name = name;
        this.syntaxReference = syntaxReference;
    }

    public override string name { get; }

    internal sealed override SyntaxReference syntaxReference { get; }

    internal sealed override TextLocation errorLocation => syntaxReference.location;

    internal sealed override ConstantValue GetConstantValue(ConstantFieldsInProgress inProgress) {
        if ((object)_lazyConstantValue != ConstantValue.Unset)
            return _lazyConstantValue;

        if (!inProgress.IsEmpty) {
            inProgress.AddDependency(this);
            return ConstantValue.Unset;
        }

        var order = ArrayBuilder<ConstantEvaluationHelpers.FieldInfo>.GetInstance();
        ConstantEvaluationHelpers.OrderAllDependencies(this, order);

        foreach (var info in order) {
            var field = info.field;
            field.BindConstantValueIfNecessary(info.startsCycle);
        }

        order.Free();

        return _lazyConstantValue;
    }

    internal ImmutableHashSet<SourceFieldSymbolWithSyntaxReference> GetConstantValueDependencies() {
        if ((object)_lazyConstantValue != ConstantValue.Unset)
            return [];

        ImmutableHashSet<SourceFieldSymbolWithSyntaxReference> dependencies;
        var builder = PooledHashSet<SourceFieldSymbolWithSyntaxReference>.GetInstance();
        var diagnostics = BelteDiagnosticQueue.GetInstance();
        var value = MakeConstantValue(builder, diagnostics);

        if ((builder.Count == 0) &&
            (value is not null) &&
            ((object)value != ConstantValue.Unset)) {
            SetLazyConstantValue(
                value,
                diagnostics
            );

            dependencies = [];
        } else {
            dependencies = ImmutableHashSet<SourceFieldSymbolWithSyntaxReference>.Empty.Union(builder);
        }

        diagnostics.Free();
        builder.Free();
        return dependencies;
    }

    private void BindConstantValueIfNecessary(bool startsCycle) {
        if ((object)_lazyConstantValue != ConstantValue.Unset)
            return;

        var builder = PooledHashSet<SourceFieldSymbolWithSyntaxReference>.GetInstance();
        var diagnostics = BelteDiagnosticQueue.GetInstance();

        if (startsCycle)
            diagnostics.Add(ErrorCode.ERR_CircConstValue, syntaxReference.location, this);

        var value = MakeConstantValue(builder, diagnostics);
        SetLazyConstantValue(
            value,
            diagnostics
        );

        diagnostics.Free();
        builder.Free();
    }

    private void SetLazyConstantValue(ConstantValue value, BelteDiagnosticQueue diagnostics) {
        if ((object)Interlocked.CompareExchange(ref _lazyConstantValue, value, ConstantValue.Unset)
            == ConstantValue.Unset) {
            AddDeclarationDiagnostics(diagnostics);
            _state.NotePartComplete(CompletionParts.ConstantValue);
        }
    }

    private protected abstract ConstantValue MakeConstantValue(
        HashSet<SourceFieldSymbolWithSyntaxReference> dependencies,
        BelteDiagnosticQueue diagnostics);
}
