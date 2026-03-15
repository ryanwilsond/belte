using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceParameterSymbol : SourceParameterSymbolBase {
    private protected SymbolCompletionState _state;

    private protected SourceParameterSymbol(
        Symbol owner,
        int ordinal,
        RefKind refKind,
        ScopedKind scope,
        string name,
        ParameterSyntax syntax)
        : base(owner, ordinal) {
        this.refKind = refKind;
        effectiveScope = scope;
        this.name = name;
        syntaxReference = new SyntaxReference(syntax);
        location = syntax.identifier.location;
    }

    public sealed override string name { get; }

    public sealed override RefKind refKind { get; }

    internal sealed override bool requiresCompletion => true;

    internal override SyntaxReference syntaxReference { get; }

    internal override TextLocation location { get; }

    internal override bool isImplicitlyDeclared => false;

    internal override ScopedKind effectiveScope { get; }

    internal abstract bool hasDefaultArgumentSyntax { get; }

    internal abstract SyntaxList<AttributeListSyntax> attributeDeclarationList { get; }

    internal static SourceParameterSymbol Create(
        Symbol owner,
        TypeWithAnnotations parameterType,
        ParameterSyntax syntax,
        RefKind refKind,
        SyntaxToken identifier,
        int ordinal,
        ScopedKind scope) {
        var name = identifier.text;

        if (syntax.defaultValue is null && scope == ScopedKind.None)
            return new SourceSimpleParameterSymbol(owner, parameterType, ordinal, refKind, name, syntax);

        return new SourceComplexParameterSymbol(
            owner,
            ordinal,
            parameterType,
            refKind,
            name,
            syntax,
            scope
        );
    }

    internal override void AddDeclarationDiagnostics(BelteDiagnosticQueue diagnostics) {
        containingSymbol.AddDeclarationDiagnostics(diagnostics);
    }

    internal sealed override bool HasComplete(CompletionParts part) {
        return _state.HasComplete(part);
    }

    internal override void ForceComplete(TextLocation location) {
        _state.DefaultForceComplete();
    }

    internal sealed override ImmutableArray<AttributeData> GetAttributes() {
        return GetAttributesBag().attributes;
    }

    internal abstract CustomAttributesBag<AttributeData> GetAttributesBag();

    private protected ScopedKind CalculateEffectiveScopeIgnoringAttributes() {
        // TODO
        // var declaredScope = this.declaredScope;

        // if (declaredScope == ScopedKind.None) {
        //     if (ParameterHelpers.IsRefScopedByDefault(this)) {
        //         return ScopedKind.ScopedRef;
        //     } else if (HasParamsModifier && Type.IsRefLikeOrAllowsRefLikeType()) {
        //         return ScopedKind.ScopedValue;
        //     }
        // }

        // return declaredScope;
        return ScopedKind.None;
    }
}
