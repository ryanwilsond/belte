using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Libraries;

namespace Buckle.CodeAnalysis.Symbols;


internal sealed class SourceAttributeData : AttributeData {
    private readonly Compilation _compilation;
    private readonly NamedTypeSymbol _attributeClass;
    private readonly MethodSymbol? _attributeConstructor;
    private readonly ImmutableArray<TypedConstant> _constructorArguments;
    private readonly ImmutableArray<int> _constructorArgumentsSourceIndices;
    private readonly ImmutableArray<KeyValuePair<string, TypedConstant>> _namedArguments;
    private readonly bool _isConditionallyOmitted;
    private readonly bool _hasErrors;
    private readonly SyntaxReference _applicationNode;

    private SourceAttributeData(
        Compilation compilation,
        SyntaxReference applicationNode,
        NamedTypeSymbol attributeClass,
        MethodSymbol attributeConstructor,
        ImmutableArray<TypedConstant> constructorArguments,
        ImmutableArray<int> constructorArgumentsSourceIndices,
        ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments,
        bool hasErrors,
        bool isConditionallyOmitted) {
        _compilation = compilation;
        _attributeClass = attributeClass;
        _attributeConstructor = attributeConstructor;
        _constructorArguments = constructorArguments;
        _constructorArgumentsSourceIndices = constructorArgumentsSourceIndices;
        _namedArguments = namedArguments;
        _isConditionallyOmitted = isConditionallyOmitted;
        _hasErrors = hasErrors;
        _applicationNode = applicationNode;
    }

    internal SourceAttributeData(
        Compilation compilation,
        AttributeSyntax attributeSyntax,
        NamedTypeSymbol attributeClass,
        MethodSymbol attributeConstructor,
        bool hasErrors)
        : this(
        compilation,
        attributeSyntax,
        attributeClass,
        attributeConstructor,
        constructorArguments: ImmutableArray<TypedConstant>.Empty,
        constructorArgumentsSourceIndices: default,
        namedArguments: ImmutableArray<KeyValuePair<string, TypedConstant>>.Empty,
        hasErrors: hasErrors,
        isConditionallyOmitted: false) {
    }

    internal SourceAttributeData(
        Compilation compilation,
        AttributeSyntax attributeSyntax,
        NamedTypeSymbol attributeClass,
        MethodSymbol? attributeConstructor,
        ImmutableArray<TypedConstant> constructorArguments,
        ImmutableArray<int> constructorArgumentsSourceIndices,
        ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments,
        bool hasErrors,
        bool isConditionallyOmitted)
        : this(
            compilation,
            new SyntaxReference(attributeSyntax),
            attributeClass,
            attributeConstructor,
            constructorArguments,
            constructorArgumentsSourceIndices,
            namedArguments,
            hasErrors,
            isConditionallyOmitted) {
    }

    internal NamedTypeSymbol attributeClass => _attributeClass;

    protected internal sealed override ImmutableArray<TypedConstant> _commonConstructorArguments
        => _constructorArguments;

    protected internal sealed override ImmutableArray<KeyValuePair<string, TypedConstant>> _commonNamedArguments
        => _namedArguments;

    internal override TextLocation GetAttributeArgumentLocation(int parameterIndex) {
        return GetAttributeArgumentSyntax(parameterIndex).location;
    }

    internal BelteSyntaxNode GetAttributeArgumentSyntax(int parameterIndex) {
        var attributeSyntax = (AttributeSyntax)_applicationNode.node;

        if (_constructorArgumentsSourceIndices.IsDefault) {
            return attributeSyntax.argumentList.arguments[parameterIndex];
        } else {
            var sourceArgIndex = _constructorArgumentsSourceIndices[parameterIndex];

            if (sourceArgIndex == -1) {
                return attributeSyntax.name;
            } else {
                return attributeSyntax.argumentList.arguments[sourceArgIndex];
            }
        }
    }

    internal override int GetTargetAttributeSignatureIndex(AttributeDescription description) {
        return GetTargetAttributeSignatureIndex(_compilation, _attributeClass, _attributeConstructor, description);
    }

    internal static int GetTargetAttributeSignatureIndex(
        Compilation compilation,
        NamedTypeSymbol attributeClass,
        MethodSymbol attributeConstructor,
        AttributeDescription description) {
        if (!IsTargetAttribute(attributeClass, description.@namespace, description.name))
            return -1;

        // TODO Temporary, treated as intrinsic
        if (description.name == "DllImportAttribute")
            return 1;

        var ctor = attributeConstructor;

        if (ctor is null)
            return -1;

        TypeSymbol? lazySystemType = null;
        var parameters = ctor.parameters;

        for (var signatureIndex = 0; signatureIndex < description.signatures.Length; signatureIndex++) {
            var targetSignature = description.signatures[signatureIndex];

            if (Matches(targetSignature, parameters, ref lazySystemType))
                return signatureIndex;
        }

        return -1;

        bool Matches(byte[] targetSignature, ImmutableArray<ParameterSymbol> parameters, ref TypeSymbol? lazySystemType) {
            if (targetSignature[0] != (byte)SignatureAttributes.Instance)
                return false;

            var parameterCount = targetSignature[1];

            if (parameterCount != parameters.Length)
                return false;

            if ((SignatureTypeCode)targetSignature[2] != SignatureTypeCode.Void) {
                return false;
            }

            var parameterIndex = 0;

            for (var signatureByteIndex = 3; signatureByteIndex < targetSignature.Length; signatureByteIndex++) {
                if (parameterIndex >= parameters.Length)
                    return false;

                var parameterType = parameters[parameterIndex].type;
                var specType = parameterType.specialType;
                var targetType = targetSignature[signatureByteIndex];

                if (targetType == (byte)SignatureTypeCode.TypeHandle) {
                    signatureByteIndex++;

                    if (parameterType.kind != SymbolKind.NamedType && parameterType.kind != SymbolKind.ErrorType)
                        return false;

                    var namedType = (NamedTypeSymbol)parameterType;
                    var targetInfo = AttributeDescription.TypeHandleTargets[targetSignature[signatureByteIndex]];

                    if (!string.Equals(namedType.metadataName, targetInfo.name, System.StringComparison.Ordinal) ||
                        !namedType.HasNameQualifier(targetInfo.@namespace)) {
                        return false;
                    }

                    targetType = (byte)targetInfo.underlying;
                } else if (targetType != (byte)SignatureTypeCode.SZArray && parameterType.IsArray()) {
                    if (targetSignature[signatureByteIndex - 1] != (byte)SignatureTypeCode.SZArray) {
                        return false;
                    }

                    specType = ((ArrayTypeSymbol)parameterType).elementType.specialType;
                }

                switch (targetType) {
                    case (byte)SignatureTypeCode.Boolean:
                        if (specType != SpecialType.Bool) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.Char:
                        if (specType != SpecialType.Char) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.SByte:
                        if (specType != SpecialType.Int8) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.Byte:
                        if (specType != SpecialType.UInt8) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.Int16:
                        if (specType != SpecialType.Int16) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.UInt16:
                        if (specType != SpecialType.UInt16) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.Int32:
                        if (specType != SpecialType.Int32) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.UInt32:
                        if (specType != SpecialType.UInt32) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.Int64:
                        if (specType != SpecialType.Int64) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.UInt64:
                        if (specType != SpecialType.UInt64) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.Single:
                        if (specType != SpecialType.Float32) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.Double:
                        if (specType != SpecialType.Float64) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.String:
                        if (specType != SpecialType.String) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.Object:
                        if (specType != SpecialType.Object) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SerializationTypeCode.Type:
                        // lazySystemType ??= compilation.GetWellKnownType(WellKnownType.Type);
                        lazySystemType ??= CorLibrary.GetSpecialType(SpecialType.Type);

                        if (!TypeSymbol.Equals(parameterType, lazySystemType, TypeCompareKind.ConsiderEverything)) {
                            return false;
                        }
                        parameterIndex += 1;
                        break;

                    case (byte)SignatureTypeCode.SZArray:
                        // Skip over and check the next byte
                        if (!parameterType.IsArray()) {
                            return false;
                        }
                        break;

                    default:
                        return false;
                }
            }

            return true;
        }
    }

    internal override bool IsTargetAttribute(string namespaceName, string typeName) {
        return IsTargetAttribute(_attributeClass, namespaceName, typeName);
    }

    internal static bool IsTargetAttribute(NamedTypeSymbol attributeClass, string namespaceName, string typeName) {
        // TODO This is temporary. Attributes are treated as intrinsics currently
        if (typeName.StartsWith(attributeClass.name))
            return true;

        if (!attributeClass.name.Equals(typeName))
            return false;

        if (attributeClass.IsErrorType() && attributeClass is not MissingMetadataTypeSymbol)
            return false;

        return attributeClass.HasNameQualifier(namespaceName);
    }
}
