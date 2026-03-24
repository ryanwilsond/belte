using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class FunctionPointerTypeSymbol : TypeSymbol {
    private FunctionPointerTypeSymbol(FunctionPointerMethodSymbol signature) {
        this.signature = signature;
    }

    internal static FunctionPointerTypeSymbol CreateFromSource(
        FunctionPointerSyntax syntax,
        Binder typeBinder,
        BelteDiagnosticQueue diagnostics,
        ConsList<TypeSymbol> basesBeingResolved) {
        return new FunctionPointerTypeSymbol(
            FunctionPointerMethodSymbol.CreateFromSource(
                syntax,
                typeBinder,
                diagnostics,
                basesBeingResolved
            )
        );
    }

    internal static FunctionPointerTypeSymbol CreateFromParts(
        CallingConvention callingConvention,
        TypeWithAnnotations returnType,
        RefKind returnRefKind,
        ImmutableArray<TypeWithAnnotations> parameterTypes,
        ImmutableArray<RefKind> parameterRefKinds) {
        return new FunctionPointerTypeSymbol(
            FunctionPointerMethodSymbol.CreateFromParts(
                callingConvention,
                returnType,
                returnRefKind,
                parameterTypes,
                parameterRefKinds
            )
        );
    }

    internal FunctionPointerTypeSymbol SubstituteTypeSymbol(
        TypeWithAnnotations substitutedReturnType,
        ImmutableArray<TypeOrConstant> substitutedParameterTypes) {
        return new FunctionPointerTypeSymbol(
            signature.SubstituteParameterSymbols(substitutedReturnType, substitutedParameterTypes)
        );
    }

    internal FunctionPointerMethodSymbol signature { get; }

    public override bool isObjectType => false;

    public override bool isPrimitiveType => true;

    public override TypeKind typeKind => TypeKind.FunctionPointer;

    internal override bool isRefLikeType => false;

    public override SymbolKind kind => SymbolKind.FunctionPointerType;

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

    internal override void Accept(SymbolVisitor visitor) => visitor.VisitFunctionPointerType(this);
    internal override ImmutableArray<Symbol> GetMembers() => [];
    internal override ImmutableArray<Symbol> GetMembers(string name) => [];
    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() => [];
    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) => [];
    internal override TResult Accept<TArgument, TResult>(SymbolVisitor<TArgument, TResult> visitor, TArgument a) => visitor.VisitFunctionPointerType(this, a);

    internal override bool Equals(TypeSymbol t2, TypeCompareKind compareKind) {
        if (ReferenceEquals(this, t2)) {
            return true;
        }

        if (!(t2 is FunctionPointerTypeSymbol other)) {
            return false;
        }

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
