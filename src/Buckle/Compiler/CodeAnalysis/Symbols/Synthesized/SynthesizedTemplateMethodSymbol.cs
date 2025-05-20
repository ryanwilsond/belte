using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedTemplateMethodSymbol : SynthesizedInstanceMethodSymbol {
    internal SynthesizedTemplateMethodSymbol(
        string name,
        NamedTypeSymbol containingType,
        TypeWithAnnotations returnType,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<ParameterSymbol> parameters,
        MethodKind methodKind) {
        this.name = name;
        returnTypeWithAnnotations = returnType;
        this.parameters = parameters;
        this.templateParameters = templateParameters;
        this.containingType = containingType;
        this.methodKind = methodKind;
    }

    public override string name { get; }

    public override ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    public override ImmutableArray<BoundExpression> templateConstraints => [];

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override MethodKind methodKind { get; }

    public override bool returnsVoid => returnType.IsVoidType();

    public override RefKind refKind => RefKind.None;

    public override int arity => templateParameters.Length;

    internal override TypeWithAnnotations returnTypeWithAnnotations { get; }

    internal override ImmutableArray<ParameterSymbol> parameters { get; }

    internal override NamedTypeSymbol containingType { get; }

    internal override Symbol containingSymbol => containingType;

    internal override bool isAbstract => false;

    internal override bool isOverride => false;

    internal override bool isStatic => false;

    internal override bool isSealed => false;

    internal override bool isVirtual => false;

    internal override Accessibility declaredAccessibility => Accessibility.Public;

    internal override bool isDeclaredConst => false;

    internal override bool hidesBaseMethodsByName => false;

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal override bool hasSpecialName => true;

    internal override bool hasUnscopedRefAttribute => false;

    internal override CallingConvention callingConvention => CallingConvention.Template;

    internal override bool IsMetadataVirtual(bool forceComplete = false) => false;
}
