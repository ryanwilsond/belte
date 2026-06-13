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
        bool isConst,
        ScopedKind scope,
        string name,
        SyntaxReference syntaxReference,
        TextLocation location)
        : base(owner, ordinal) {
        this.refKind = refKind;
        effectiveScope = scope;
        this.name = name;
        this.isConst = isConst;
        this.syntaxReference = syntaxReference;
        this.location = location;
    }

    public sealed override string name { get; }

    public sealed override RefKind refKind { get; }

    internal sealed override bool requiresCompletion => true;

    internal override SyntaxReference syntaxReference { get; }

    internal override TextLocation location { get; }

    internal override bool isImplicitlyDeclared => false;

    internal override ScopedKind effectiveScope { get; }

    internal abstract bool hasDefaultArgumentSyntax { get; }

    internal override bool isMetadataOut => refKind == RefKind.Out;

    internal override bool isConst { get; }

    internal abstract SyntaxList<AttributeListSyntax> attributeDeclarationList { get; }

    internal static SourceParameterSymbol Create(
        Symbol owner,
        TypeWithAnnotations parameterType,
        ParameterSyntax syntax,
        RefKind refKind,
        bool isConst,
        string name,
        int ordinal,
        ScopedKind scope) {
        if (syntax.defaultValue is null && scope == ScopedKind.None && syntax.attributeLists.Count == 0) {
            return new SourceSimpleParameterSymbol(
                owner,
                parameterType,
                ordinal,
                refKind,
                isConst,
                name,
                new SyntaxReference(syntax),
                syntax.identifier.location
            );
        }

        return new SourceComplexParameterSymbol(
            owner,
            ordinal,
            parameterType,
            refKind,
            isConst,
            name,
            syntax,
            scope
        );
    }

    internal static SourceParameterSymbol CreateReverseParameter(
        Symbol owner,
        TypeWithAnnotations parameterType,
        SyntaxNode syntax,
        TextLocation location,
        RefKind refKind,
        string name) {
        return new SourceSimpleParameterSymbol(
            owner,
            parameterType,
            0,
            refKind,
            false,
            name,
            new SyntaxReference(syntax),
            location
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
