using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class ImplicitNamedTypeSymbol : SourceMemberContainerTypeSymbol {
    internal ImplicitNamedTypeSymbol(
        NamespaceOrTypeSymbol containingSymbol,
        MergedTypeDeclaration declaration,
        BelteDiagnosticQueue diagnostics)
        : base(containingSymbol, declaration, diagnostics) {
        _state.NotePartComplete(CompletionParts.EnumUnderlyingType);
    }

    internal override NamedTypeSymbol baseType => CorLibrary.GetSpecialType(SpecialType.Object);

    private protected override void CheckBase(BelteDiagnosticQueue diagnostics) { }

    internal override ImmutableArray<AttributeData> GetAttributes() {
        _state.NotePartComplete(CompletionParts.Attributes);
        return [];
    }

    internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<TypeSymbol> basesBeingResolved) {
        return baseType;
    }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    private protected override NamedTypeSymbol WithTupleDataCore(TupleExtraData newData) {
        throw ExceptionUtilities.Unreachable();
    }
}
