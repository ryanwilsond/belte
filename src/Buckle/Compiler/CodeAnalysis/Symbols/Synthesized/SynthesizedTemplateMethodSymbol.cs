using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedTemplateMethodSymbol : MethodSymbol {
    private readonly DeclarationModifiers _modifiers;

    internal SynthesizedTemplateMethodSymbol(
        string name,
        NamedTypeSymbol containingType,
        TypeWithAnnotations returnType,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<ParameterSymbol> parameters,
        MethodKind methodKind,
        DeclarationModifiers modifiers) {
        this.name = name;
        returnTypeWithAnnotations = returnType;
        this.parameters = parameters;
        this.templateParameters = templateParameters;
        this.containingType = containingType;
        this.methodKind = methodKind;
        _modifiers = modifiers;
    }

    public override string name { get; }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override ImmutableArray<TypeOrConstant> templateArguments => GetTemplateParametersAsTemplateArguments();

    public override MethodKind methodKind { get; }

    public override bool returnsVoid => returnType.IsVoidType();

    public override RefKind refKind => RefKind.None;

    public override int arity => templateParameters.Length;

    internal override TypeWithAnnotations returnTypeWithAnnotations { get; }

    internal override ImmutableArray<ParameterSymbol> parameters { get; }

    internal override NamedTypeSymbol containingType { get; }

    internal override Symbol containingSymbol => containingType;

    internal override bool isStatic => (_modifiers & DeclarationModifiers.Static) != 0;

    internal override bool isVirtual => (_modifiers & DeclarationModifiers.Virtual) != 0;

    internal override bool isAbstract => (_modifiers & DeclarationModifiers.Abstract) != 0;

    internal override bool isOverride => (_modifiers & DeclarationModifiers.Override) != 0;

    internal override bool isSealed => (_modifiers & DeclarationModifiers.Sealed) != 0;

    internal override Accessibility declaredAccessibility => Accessibility.Public;

    internal override bool isDeclaredConst => false;

    internal override bool hidesBaseMethodsByName => false;

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal override bool hasSpecialName => true;

    internal override bool hasUnscopedRefAttribute => false;

    internal override CallingConvention callingConvention => CallingConvention.Template;

    internal override bool IsMetadataVirtual(bool forceComplete = false) => false;

    internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
        throw ExceptionUtilities.Unreachable();
    }
}
