using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class MetadataAttributeData : AttributeData {
    private readonly Compilation _compilation;
    private readonly NamedTypeSymbol _attributeClass;
    private readonly MethodSymbol _attributeConstructor;
    private readonly ImmutableArray<TypedConstant> _constructorArguments;

    internal MetadataAttributeData(
        Compilation compilation,
        NamedTypeSymbol attributeClass,
        MethodSymbol attributeConstructor,
        ImmutableArray<TypedConstant> constructorArguments) {
        _compilation = compilation;
        _attributeClass = attributeClass;
        _attributeConstructor = attributeConstructor;
        _constructorArguments = constructorArguments;
    }

    internal override NamedTypeSymbol attributeClass => _attributeClass;

    internal override MethodSymbol attributeConstructor => _attributeConstructor;

    protected internal override INamedTypeSymbol _commonAttributeClass => _attributeClass;

    protected internal override IMethodSymbol _commonAttributeConstructor => _attributeConstructor;

    protected internal sealed override ImmutableArray<TypedConstant> _commonConstructorArguments
        => _constructorArguments;

    protected internal sealed override ImmutableArray<KeyValuePair<string, TypedConstant>> _commonNamedArguments => [];

    internal override bool hasErrors => false;

    internal override TextLocation GetAttributeArgumentLocation(int parameterIndex) {
        return new MetadataLocation(attributeClass.containingModule);
    }

    internal override bool IsTargetAttribute(string namespaceName, string typeName) {
        return SourceAttributeData.IsTargetAttribute(_attributeClass, namespaceName, typeName);
    }

    internal override int GetTargetAttributeSignatureIndex(AttributeDescription description) {
        return SourceAttributeData.GetTargetAttributeSignatureIndex(
            _compilation,
            _attributeClass,
            _attributeConstructor,
            description
        );
    }
}
