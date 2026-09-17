using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

public sealed partial class Compilation {
    internal class SpecialMembersSignatureComparer
            : SignatureComparer<MethodSymbol, FieldSymbol, PropertySymbol, TypeSymbol, ParameterSymbol> {
        internal static readonly SpecialMembersSignatureComparer Instance = new SpecialMembersSignatureComparer();

        private protected SpecialMembersSignatureComparer() { }

        private protected override TypeSymbol GetMDArrayElementType(TypeSymbol type) {
            if (type.kind != SymbolKind.ArrayType)
                return null;

            var array = (ArrayTypeSymbol)type;

            if (array.isSZArray)
                return null;

            return array.elementType;
        }

        private protected override TypeSymbol GetFieldType(FieldSymbol field) {
            return field.type;
        }

        private protected override TypeSymbol GetPropertyType(PropertySymbol property) {
            return property.type;
        }

        private protected override TypeSymbol GetGenericTypeArgument(TypeSymbol type, int argumentIndex) {
            if (type.kind != SymbolKind.NamedType)
                return null;

            var named = (NamedTypeSymbol)type;

            if (named.arity <= argumentIndex)
                return null;

            if (named.containingType is not null)
                return null;

            return named.templateArguments[argumentIndex].type.type;
        }

        private protected override TypeSymbol? GetGenericTypeDefinition(TypeSymbol type) {
            if (type.kind != SymbolKind.NamedType)
                return null;

            var named = (NamedTypeSymbol)type;

            if (named.containingType is not null)
                return null;

            if (named.arity == 0)
                return null;

            return (NamedTypeSymbol)named.originalDefinition;
        }

        private protected override ImmutableArray<ParameterSymbol> GetParameters(MethodSymbol method) {
            return method.parameters;
        }

        private protected override ImmutableArray<ParameterSymbol> GetParameters(PropertySymbol property) {
            return property.parameters;
        }

        private protected override TypeSymbol GetParamType(ParameterSymbol parameter) {
            return parameter.type;
        }

        private protected override TypeSymbol GetPointedToType(TypeSymbol type) {
            return type.kind == SymbolKind.PointerType ? ((PointerTypeSymbol)type).pointedAtType : null;
        }

        private protected override TypeSymbol GetReturnType(MethodSymbol method) {
            return method.returnType;
        }

        private protected override TypeSymbol GetSZArrayElementType(TypeSymbol type) {
            if (type.kind != SymbolKind.ArrayType)
                return null;

            var array = (ArrayTypeSymbol)type;

            if (!array.isSZArray)
                return null;

            return array.elementType;
        }

        private protected override bool IsByRefParam(ParameterSymbol parameter) {
            return parameter.refKind != RefKind.None;
        }

        private protected override bool IsByRefMethod(MethodSymbol method) {
            return method.refKind != RefKind.None;
        }

        private protected override bool IsByRefProperty(PropertySymbol property) {
            return property.refKind != RefKind.None;
        }

        private protected override bool IsGenericMethodTypeParam(TypeSymbol type, int paramPosition) {
            if (type.kind != SymbolKind.TemplateParameter)
                return false;

            var typeParam = (TemplateParameterSymbol)type;

            if (typeParam.containingSymbol.kind != SymbolKind.Method)
                return false;

            return typeParam.ordinal == paramPosition;
        }

        private protected override bool IsGenericTypeParam(TypeSymbol type, int paramPosition) {
            if (type.kind != SymbolKind.TemplateParameter)
                return false;

            var typeParam = (TemplateParameterSymbol)type;

            if (typeParam.containingSymbol.kind != SymbolKind.NamedType)
                return false;

            return typeParam.ordinal == paramPosition;
        }

        private protected override bool MatchArrayRank(TypeSymbol type, int countOfDimensions) {
            if (type.kind != SymbolKind.ArrayType)
                return false;

            var array = (ArrayTypeSymbol)type;

            return array.rank == countOfDimensions;
        }

        private protected override bool MatchTypeToTypeId(TypeSymbol type, int typeId) {
            if ((int)type.originalDefinition.specialType == typeId) {
                if (type.isDefinition)
                    return true;

                return type.Equals(type.originalDefinition, TypeCompareKind.ConsiderEverything);
            }

            return false;
        }
    }
}
