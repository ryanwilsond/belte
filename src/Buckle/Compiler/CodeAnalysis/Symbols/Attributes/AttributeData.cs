using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class AttributeData {
    internal virtual bool hasErrors => false;

    internal bool IsTargetAttribute(AttributeDescription description) {
        return GetTargetAttributeSignatureIndex(description) != -1;
    }

    protected internal abstract ImmutableArray<TypedConstant> _commonConstructorArguments { get; }

    protected internal abstract ImmutableArray<KeyValuePair<string, TypedConstant>> _commonNamedArguments { get; }

    internal abstract int GetTargetAttributeSignatureIndex(AttributeDescription description);

    internal abstract TextLocation GetAttributeArgumentLocation(int parameterIndex);

    internal T GetConstructorArgument<T>(int i, SpecialType specialType) {
        var constructorArgs = _commonConstructorArguments;
        return constructorArgs[i].DecodeValue<T>(specialType);
    }

    internal abstract bool IsTargetAttribute(string namespaceName, string typeName);
}
