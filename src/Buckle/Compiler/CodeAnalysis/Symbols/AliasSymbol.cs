using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class AliasSymbol : Symbol {
    private readonly ImmutableArray<TextLocation> _locations;

    private protected AliasSymbol(
        string aliasName,
        Symbol containingSymbol,
        ImmutableArray<TextLocation> locations) {
        _locations = locations;
        name = aliasName;
        this.containingSymbol = containingSymbol;
    }

    internal static AliasSymbol CreateGlobalNamespaceAlias(NamespaceSymbol globalNamespace) {
        return new AliasSymbolFromResolvedTarget(globalNamespace, "global", globalNamespace, []);
    }

    internal AliasSymbol ToNewSubmission(Compilation compilation) {
        var previousTarget = target;

        if (previousTarget.kind != SymbolKind.Namespace)
            return this;

        var expandedGlobalNamespace = compilation.globalNamespaceInternal;
        var expandedNamespace = Imports.ExpandPreviousSubmissionNamespace(
            (NamespaceSymbol)previousTarget,
            expandedGlobalNamespace
        );

        return new AliasSymbolFromResolvedTarget(expandedNamespace, name, containingSymbol, _locations);
    }

    public sealed override string name { get; }

    public override SymbolKind kind => SymbolKind.Alias;

    public abstract NamespaceOrTypeSymbol target { get; }

    internal override TextLocation location => _locations[0];

    internal override SyntaxReference syntaxReference
        => GetDeclaringSyntaxReferenceHelper<UsingDirectiveSyntax>(_locations)[0];

    internal override bool isSealed => false;

    internal override bool isAbstract => false;

    internal override bool isOverride => false;

    internal override bool isVirtual => false;

    internal override bool isStatic => false;

    internal override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal sealed override Symbol containingSymbol { get; }

    internal override void Accept(SymbolVisitor visitor) {
        visitor.VisitAlias(this);
    }

    internal override TResult Accept<TArg, TResult>(SymbolVisitor<TArg, TResult> visitor, TArg a) {
        return visitor.VisitAlias(this, a);
    }

    internal abstract NamespaceOrTypeSymbol GetAliasTarget(ConsList<TypeSymbol> basesBeingResolved);

    internal void CheckConstraints(BelteDiagnosticQueue diagnostics) {
        var target = this.target as TypeSymbol;

        if (target is not null && _locations.Length > 0)
            target.CheckAllConstraints(location, diagnostics);
    }

    internal override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if (ReferenceEquals(this, obj))
            return true;

        if (obj is null)
            return false;

        return obj is AliasSymbol other &&
            Equals(location, other.location) &&
            Equals(containingSymbol, other.containingSymbol, compareKind);
    }

    public override int GetHashCode() {
        return location?.GetHashCode() ?? name.GetHashCode();
    }

    internal abstract override bool requiresCompletion { get; }
}
