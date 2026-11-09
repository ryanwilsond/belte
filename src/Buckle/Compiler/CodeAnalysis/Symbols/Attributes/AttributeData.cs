using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class AttributeData {
    internal virtual bool hasErrors => false;

    internal bool IsTargetAttribute(AttributeDescription description) {
        return GetTargetAttributeSignatureIndex(description) != -1;
    }

    protected internal abstract ImmutableArray<TypedConstant> _commonConstructorArguments { get; }

    protected internal abstract ImmutableArray<KeyValuePair<string, TypedConstant>> _commonNamedArguments { get; }

    protected internal abstract INamedTypeSymbol _commonAttributeClass { get; }

    protected internal abstract IMethodSymbol _commonAttributeConstructor { get; }

    internal virtual INamedTypeSymbol attributeClass => _commonAttributeClass;

    internal virtual IMethodSymbol attributeConstructor => _commonAttributeConstructor;

    internal abstract int GetTargetAttributeSignatureIndex(AttributeDescription description);

    internal abstract TextLocation GetAttributeArgumentLocation(int parameterIndex);

    internal T GetConstructorArgument<T>(int i, SpecialType specialType) {
        var constructorArgs = _commonConstructorArguments;
        return constructorArgs[i].DecodeValue<T>(specialType);
    }

    internal abstract bool IsTargetAttribute(string namespaceName, string typeName);

    internal AttributeUsageInfo DecodeAttributeUsageAttribute() {
        return DecodeAttributeUsageAttribute(_commonConstructorArguments[0], _commonNamedArguments);
    }

    internal static AttributeUsageInfo DecodeAttributeUsageAttribute(
        TypedConstant positionalArg,
        ImmutableArray<KeyValuePair<string, TypedConstant>> namedArgs) {
        var validOn = (AttributeTargets)positionalArg.valueInternal;
        var allowMultiple = DecodeNamedArgument(namedArgs, "AllowMultiple", SpecialType.Bool, false);
        var inherited = DecodeNamedArgument(namedArgs, "Inherited", SpecialType.Bool, true);
        return new AttributeUsageInfo(validOn, allowMultiple, inherited);
    }

    private static T DecodeNamedArgument<T>(
        ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments,
        string name,
        SpecialType specialType,
        T defaultValue = default) {
        var index = IndexOfNamedArgument(namedArguments, name);
        return index >= 0 ? namedArguments[index].Value.DecodeValue<T>(specialType) : defaultValue;
    }

    private static int IndexOfNamedArgument(
        ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments,
        string name) {
        for (var i = namedArguments.Length - 1; i >= 0; i--) {
            if (string.Equals(namedArguments[i].Key, name, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    internal static bool IsTargetEarlyAttribute(
        NamedTypeSymbol attributeType,
        AttributeSyntax attributeSyntax,
        AttributeDescription description) {
        var argumentCount = (attributeSyntax.argumentList is not null)
            ? attributeSyntax.argumentList.arguments
                .Count(static (arg) => arg is not ArgumentSyntax a || a.colon is null)
            : 0;

        return IsTargetEarlyAttribute(attributeType, argumentCount, description);
    }

    internal static bool IsTargetEarlyAttribute(
        INamedTypeSymbol attributeType,
        int attributeArgCount,
        AttributeDescription description) {
        if (attributeType.containingSymbol?.kind != SymbolKind.Namespace)
            return false;

        var attributeCtorsCount = description.signatures.Length;

        for (var i = 0; i < attributeCtorsCount; i++) {
            var parameterCount = description.GetParameterCount(signatureIndex: i);

            if (attributeArgCount == parameterCount) {
                var options = description.matchIgnoringCase
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                return attributeType.name.Equals(description.name, options) &&
                    NamespaceMatch(attributeType.containingNamespace, description.@namespace, options);
            }
        }

        return false;

        static bool NamespaceMatch(INamespaceSymbol container, string namespaceName, StringComparison options) {
            var index = namespaceName.Length;
            var expectDot = false;

            while (true) {
                if (container.isGlobalNamespace)
                    return index == 0;

                if (expectDot) {
                    index--;

                    if (index < 0 || namespaceName[index] != '.')
                        return false;
                } else {
                    expectDot = true;
                }

                var name = container.name;
                var nameLength = name.Length;
                index -= nameLength;

                if (index < 0 || string.Compare(namespaceName, index, name, 0, nameLength, options) != 0)
                    return false;

                container = container.containingNamespace;

                if (container is null)
                    return false;
            }
        }
    }
}
