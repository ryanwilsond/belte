using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class FunctionTypeSymbol : TypeSymbol {
    private FunctionTypeSymbol(FunctionMethodSymbol signature) {
        this.signature = signature;
    }

    internal static FunctionTypeSymbol CreateFromSource(
        FunctionTypeSyntax syntax,
        Binder typeBinder,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved) {
        return new FunctionTypeSymbol(
            FunctionMethodSymbol.CreateFromSource(
                syntax,
                typeBinder,
                diagnostics,
                basesBeingResolved
            )
        );
    }

    internal static FunctionTypeSymbol CreateFromMetadata(
        ModuleSymbol containingModule,
        ImmutableArray<ParamInfo<TypeSymbol>> retAndParamTypes) {
        return new FunctionTypeSymbol(
            FunctionMethodSymbol.CreateFromMetadata(containingModule, retAndParamTypes)
        );
    }

    internal static FunctionTypeSymbol CreateFromParts(
        TypeWithAnnotations returnType,
        RefKind returnRefKind,
        ImmutableArray<TypeWithAnnotations> parameterTypes,
        ImmutableArray<RefKind> parameterRefKinds) {
        return new FunctionTypeSymbol(
            FunctionMethodSymbol.CreateFromParts(
                returnType,
                returnRefKind,
                parameterTypes,
                parameterRefKinds
            )
        );
    }

    internal FunctionTypeSymbol SubstituteTypeSymbol(
        TypeWithAnnotations substitutedReturnType,
        ImmutableArray<TypeOrConstant> substitutedParameterTypes) {
        return new FunctionTypeSymbol(
            signature.SubstituteParameterSymbols(substitutedReturnType, substitutedParameterTypes)
        );
    }

    internal FunctionMethodSymbol signature { get; }

    public override bool isObjectType => true;

    public override bool isPrimitiveType => false;

    public override TypeKind typeKind => TypeKind.Function;

    internal override bool isRefLikeType => false;

    public override SymbolKind kind => SymbolKind.FunctionType;

    internal override Symbol containingSymbol => null;

    internal override ImmutableArray<TextLocation> locations => [];

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal override bool isStatic => false;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    internal override NamedTypeSymbol baseType => null;

    internal override void Accept(SymbolVisitor visitor) => visitor.VisitFunctionType(this);
    internal override ImmutableArray<Symbol> GetMembers() => [];
    internal override ImmutableArray<Symbol> GetMembers(string name) => [];
    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() => [];
    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) => [];
    internal override TResult Accept<TArgument, TResult>(SymbolVisitor<TArgument, TResult> visitor, TArgument a) => visitor.VisitFunctionType(this, a);

    internal override bool ApplyNullableTransforms(
        byte defaultTransformFlag,
        ImmutableArray<byte> transforms,
        ref int position,
        out TypeSymbol result) {
        var newSignature = signature.ApplyNullableTransforms(defaultTransformFlag, transforms, ref position);
        var madeChanges = (object)signature != newSignature;
        result = madeChanges ? new FunctionTypeSymbol(newSignature) : this;
        return madeChanges;
    }

    internal override bool Equals(TypeSymbol t2, TypeCompareKind compareKind) {
        if (ReferenceEquals(this, t2))
            return true;

        if (t2 is not FunctionTypeSymbol other)
            return false;

        return signature.Equals(other.signature, compareKind);
    }

    public override int GetHashCode() {
        return Hash.Combine(1, signature.GetHashCode());
    }

    internal static RefKind GetRefKindForHashCode(RefKind refKind) {
        return refKind == RefKind.None ? RefKind.None : RefKind.Ref;
    }

    internal static bool RefKindEquals(TypeCompareKind compareKind, RefKind refKind1, RefKind refKind2) {
        return refKind1 == refKind2;
    }
}
