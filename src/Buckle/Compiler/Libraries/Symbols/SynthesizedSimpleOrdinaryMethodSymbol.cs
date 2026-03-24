using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.Libraries;

internal sealed class SynthesizedSimpleOrdinaryMethodSymbol : MethodSymbol {
    private readonly DeclarationModifiers _modifiers;

    internal SynthesizedSimpleOrdinaryMethodSymbol(
        string name,
        TypeWithAnnotations returnType,
        RefKind refKind,
        DeclarationModifiers modifiers) {
        this.name = name;
        returnTypeWithAnnotations = returnType;
        this.refKind = refKind;
        _modifiers = modifiers;
    }

    public override string name { get; }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters => [];

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override RefKind refKind { get; }

    public override bool returnsVoid => returnTypeWithAnnotations.IsVoidType();

    public override MethodKind methodKind => MethodKind.Ordinary;

    public override int arity => 0;

    internal override TypeWithAnnotations returnTypeWithAnnotations { get; }

    internal override ImmutableArray<ParameterSymbol> parameters => throw new InvalidOperationException();

    internal override bool hidesBaseMethodsByName => false;

    internal override bool hasSpecialName => false;

    internal override bool isDeclaredConst => (_modifiers & DeclarationModifiers.Const) != 0;

    internal override Accessibility declaredAccessibility => ModifierHelpers.EffectiveAccessibility(_modifiers);

    internal override Symbol containingSymbol => throw new InvalidOperationException();

    internal override bool isStatic => (_modifiers & DeclarationModifiers.Static) != 0;

    internal override bool isVirtual => (_modifiers & DeclarationModifiers.Virtual) != 0;

    internal override bool isAbstract => (_modifiers & DeclarationModifiers.Abstract) != 0;

    internal override bool isOverride => (_modifiers & DeclarationModifiers.Override) != 0;

    internal override bool isSealed => (_modifiers & DeclarationModifiers.Sealed) != 0;

    internal override bool isExtern => (_modifiers & DeclarationModifiers.Extern) != 0;

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal override CallingConvention callingConvention => CallingConvention.Default;

    internal override bool hasUnscopedRefAttribute => false;

    internal override bool IsMetadataVirtual(bool forceComplete = false) => false;

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override DllImportData GetDllImportData() {
        return null;
    }
}
