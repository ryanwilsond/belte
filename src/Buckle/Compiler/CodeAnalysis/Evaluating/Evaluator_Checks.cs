using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using static Buckle.CodeAnalysis.Binding.Binder;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed partial class Evaluator {
    [Conditional("DEBUG")]
    private void CheckResultIsCoherent(BoundExpression node, bool used, EvaluatorValue value) {
        if (!used)
            // We allow the Evaluator to produce garbage results here for performance
            return;

        if (ExpressionContainsInvariants(node, value))
            return;

        var type = node.type;
        var isRef = false;

        if (node.kind == BoundKind.ThisExpression) {
            if (node.type.StrippedType().IsStructType()) {
                Debug.Assert(value.kind == ValueKind.Ref);
                isRef = true;
            } else if (value.kind == ValueKind.Ref) {
                var refValue = value.loc[value.ptr];

                if (refValue.kind == ValueKind.Struct)
                    isRef = true;
            }
        }

        CheckTypeIsCoherent(type, isRef, value);
    }

    private static bool ExpressionContainsInvariants(BoundExpression node, EvaluatorValue value) {
        // If the expression contains pointer nodes which knowable break certain Evaluator assumptions
        var hasPointerInvariants = ExpressionInvariantVisitor.Instance.Visit(node, null);

        if (hasPointerInvariants)
            return true;

        var hasExpanderInvariants = ExpressionContainsExpanderInvariants(node, value);

        if (hasExpanderInvariants)
            return true;

        return false;
    }

    private static bool ExpressionContainsExpanderInvariants(BoundExpression node, EvaluatorValue value) {
        // This checks for patterns created by lowering that violate normal null guarantees

        if (value.kind != ValueKind.Null)
            return false;

        if (node.syntax.kind == SyntaxKind.IsPatternExpression) {
            if (node.kind is BoundKind.AsOperator or
                             BoundKind.StackSlotExpression or
                             BoundKind.DataContainerExpression or
                             BoundKind.AssignmentOperator) {
                return true;
            }
        }

        return false;
    }

    [Conditional("DEBUG")]
    private void CheckTypeIsCoherent(TypeSymbol type, bool isRef, EvaluatorValue value) {
        if (type.IsNullableType() && value.kind == ValueKind.Null)
            return;

        if (type.IsVoidType()) {
            // Script mode propagates values from void method calls
            Debug.Assert(value.kind == ValueKind.Null || _isScript);
            return;
        }

        var strippedType = type.StrippedType();

        if (strippedType is TemplateParameterSymbol t) {
            if (t.underlyingType.specialType != SpecialType.Type) {
                var substituted = SubstituteTemplateParameter(t);

                var comparison = EvaluateEqualityOperator(
                    rightIsLiteralNull: false,
                    isEqual: true,
                    substituted,
                    value,
                    RelationalOperatorType(t.underlyingType.type)
                );

                Debug.Assert(comparison.kind == ValueKind.Bool && comparison.@bool);
                return;
            } else {
                strippedType = SubstituteTemplateParameterType(t);
            }

            if (strippedType.IsNullableType() && value.kind == ValueKind.Null)
                return;

            strippedType = strippedType.StrippedType();
        }

        // TODO
        // Unfortunately its too late to properly construct all types
        // Most are okay but some require substitutes from higher stack frames
        // If we really want to verify these values we would have to ensure types are substituted earlier
        if (strippedType.ContainsTemplateParameter())
            return;

        if (strippedType.IsEnumType())
            strippedType = ((NamedTypeSymbol)strippedType).enumUnderlyingType;

        if (strippedType.IsPointerOrFunctionPointer() ||
            strippedType.specialType is SpecialType.IntPtr or SpecialType.UIntPtr) {
            Debug.Assert(value.kind == ValueKind.Ref);
            return;
        }

        Debug.Assert(value.kind == ValueKind.Ref == isRef);

        if (value.kind == ValueKind.Ref)
            value = value.loc[value.ptr];

        Debug.Assert(value.kind != ValueKind.Ref);

        if (strippedType.specialType is SpecialType.Any or SpecialType.Object) {
            Debug.Assert(value.kind is not ValueKind.Null);
            return;
        }

        switch (value.kind) {
            case ValueKind.Null:
                Debug.Assert(false);
                break;
            case ValueKind.Int8:
                Debug.Assert(strippedType.specialType == SpecialType.Int8);
                break;
            case ValueKind.Int16:
                Debug.Assert(strippedType.specialType == SpecialType.Int16);
                break;
            case ValueKind.Int32:
                Debug.Assert(strippedType.specialType is SpecialType.Int32 or SpecialType.WinBool);
                break;
            case ValueKind.Int64:
                Debug.Assert(strippedType.specialType is SpecialType.Int64 or SpecialType.Int);
                break;
            case ValueKind.UInt8:
                Debug.Assert(strippedType.specialType == SpecialType.UInt8);
                break;
            case ValueKind.UInt16:
                Debug.Assert(strippedType.specialType == SpecialType.UInt16);
                break;
            case ValueKind.UInt32:
                Debug.Assert(strippedType.specialType == SpecialType.UInt32);
                break;
            case ValueKind.UInt64:
                Debug.Assert(strippedType.specialType == SpecialType.UInt64);
                break;
            case ValueKind.Float32:
                Debug.Assert(strippedType.specialType == SpecialType.Float32);
                break;
            case ValueKind.Float64:
                Debug.Assert(strippedType.specialType is SpecialType.Float64 or SpecialType.Decimal);
                break;
            case ValueKind.Bool:
                Debug.Assert(strippedType.specialType == SpecialType.Bool);
                break;
            case ValueKind.Char:
                Debug.Assert(strippedType.specialType == SpecialType.Char);
                break;
            case ValueKind.String:
                Debug.Assert(strippedType.specialType == SpecialType.String);
                break;
            case ValueKind.Type:
                Debug.Assert(strippedType.specialType == SpecialType.Type);
                break;
            case ValueKind.Struct:
                Debug.Assert(strippedType.IsStructType());
                break;
            case ValueKind.HeapPtr:
                Debug.Assert(strippedType.isReferenceType);
                var heapObject = _context.heap[value.ptr];

                if (strippedType.IsInterfaceType())
                    Debug.Assert(heapObject.type.ImplementsInterface(strippedType));
                else
                    Debug.Assert(heapObject.type.IsEqualToOrDerivedFrom(strippedType, TypeCompareKind.AllIgnoreOptions));

                break;
            case ValueKind.MethodGroup:
                // TODO EvaluatorValue.methodGroup doesn't store type information so is there any way to verify this is not malformed?
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(value.kind);
        }
    }

    [Conditional("DEBUG")]
    private void CheckArgumentsAreCoherent(MethodSymbol method, EvaluatorValue[] arguments) {
        Debug.Assert(method.parameterCount == arguments.Length);

        var parameters = method.parameters;

        for (var i = 0; i < method.parameterCount; i++) {
            var parameter = parameters[i];
            var paramValue = arguments[i];
            CheckTypeIsCoherent(parameter.type, parameter.refKind != RefKind.None, paramValue);
        }
    }

    private TypeSymbol SubstituteAsType(TypeSymbol type) {
        var value = SubstituteType(type);
        Debug.Assert(value.kind == ValueKind.Type);
        return (TypeSymbol)value.type;
    }

    private EvaluatorValue SubstituteType(TypeSymbol type) {
        if (type is null)
            return EvaluatorValue.None;

        TypeSymbol result;

        switch (type.kind) {
            case SymbolKind.NamedType:
                result = SubstituteNamedType((NamedTypeSymbol)type);
                break;
            case SymbolKind.TemplateParameter:
                return SubstituteTemplateParameter((TemplateParameterSymbol)type);
            case SymbolKind.ArrayType:
                result = SubstituteArrayType((ArrayTypeSymbol)type);
                break;
            case SymbolKind.PointerType:
                result = SubstitutePointerType((PointerTypeSymbol)type);
                break;
            case SymbolKind.FunctionType:
                result = SubstituteFunctionType((FunctionTypeSymbol)type);
                break;
            case SymbolKind.FunctionPointerType:
            case SymbolKind.ErrorType:
                throw ExceptionUtilities.UnexpectedValue(type.kind);
            default:
                result = type;
                break;
        }

        if (result is null)
            return EvaluatorValue.None;

        return EvaluatorValue.Type(result);
    }

    private TypeOrConstant SubstituteTypeOrConstant(TypeOrConstant typeOrConstant) {
        if (typeOrConstant.isType)
            return EvaluatorValueToTypeOrConstant(SubstituteTypeWithAnnotations(typeOrConstant.type));

        if (typeOrConstant.constant is TemplateConstantValue t)
            // ! We figure the chances of this stalling is basically 0 so its okay to not pass abort
            return EvaluatorValueToTypeOrConstant(EvaluateExpression(t.expression, true, abort: false));

        return typeOrConstant;
    }

    private TypeOrConstant EvaluatorValueToTypeOrConstant(EvaluatorValue value) {
        if (value.kind == ValueKind.Type)
            return new TypeOrConstant((TypeSymbol)value.type);

        return new TypeOrConstant(
            new ConstantValue(
                EvaluatorValue.Format(value, context: null),
                ValueKindExtensions.ToSpecialType(value.kind)
            )
        );
    }

    private NamedTypeSymbol SubstituteNamedType(NamedTypeSymbol type) {
        if (type is null)
            return null;

        var oldConstructedFrom = type.constructedFrom;
        var newConstructedFrom = SubstituteTypeDeclaration(oldConstructedFrom);

        var oldTemplateArguments = type.templateArguments;
        var changed = !ReferenceEquals(oldConstructedFrom, newConstructedFrom);
        var newTypeArguments = ArrayBuilder<TypeOrConstant>.GetInstance(oldTemplateArguments.Length);

        for (var i = 0; i < oldTemplateArguments.Length; i++) {
            var oldArgument = oldTemplateArguments[i];
            var newArgument = SubstituteTypeOrConstant(oldArgument);

            if (!changed && !oldArgument.IsSameAs(newArgument))
                changed = true;

            newTypeArguments.Add(newArgument);
        }

        if (!changed)
            return type;

        return newConstructedFrom.ConstructIfGeneric(newTypeArguments.ToImmutableAndFree())
            .WithTupleDataFrom(type);
    }

    private NamedTypeSymbol SubstituteTypeDeclaration(NamedTypeSymbol previous) {
        var newContainingType = SubstituteNamedType(previous.containingType);

        if ((object)newContainingType is null)
            return previous;

        return previous.originalDefinition.AsMember(newContainingType);
    }

    private PointerTypeSymbol SubstitutePointerType(PointerTypeSymbol t) {
        var oldPointedAtType = t.pointedAtTypeWithAnnotations;
        var pointedAtType = SubstituteTypeWithAnnotationsAsType(oldPointedAtType);

        if (pointedAtType.IsSameAs(oldPointedAtType))
            return t;

        return new PointerTypeSymbol(pointedAtType);
    }

    private TypeWithAnnotations SubstituteTypeWithAnnotationsAsType(TypeWithAnnotations typeWithAnnotations) {
        var value = SubstituteTypeWithAnnotations(typeWithAnnotations);
        Debug.Assert(value.kind == ValueKind.Type);
        return new TypeWithAnnotations((TypeSymbol)value.type);
    }

    private EvaluatorValue SubstituteTypeWithAnnotations(TypeWithAnnotations typeWithAnnotations) {
        var typeSymbol = typeWithAnnotations.type.StrippedType();
        var newTypeValue = SubstituteType(typeSymbol);

        if (newTypeValue.kind != ValueKind.Type)
            return newTypeValue;

        var newType = new TypeWithAnnotations((TypeSymbol)newTypeValue.type);

        if (typeWithAnnotations.type.IsNullableType() && !newType.IsNullableType())
            newType = newType.SetIsAnnotated();

        if (!typeSymbol.IsTemplateParameter()) {
            if (typeSymbol.Equals(newType.type, TypeCompareKind.ConsiderEverything))
                return EvaluatorValue.Type(typeWithAnnotations.type);
            else if (typeSymbol.IsNullableType() && typeWithAnnotations.isNullable)
                return EvaluatorValue.Type(newType.type);

            // TODO Its okay to lose `isNullable` correct?
            return EvaluatorValue.Type(newType.type/*, isNullable*/);
        }

        if ((object)newType == (TemplateParameterSymbol)typeSymbol)
            return EvaluatorValue.Type(typeWithAnnotations.type);
        else if ((object)this == (TemplateParameterSymbol)typeSymbol)
            return EvaluatorValue.Type(newType.type);

        // TODO Its okay to lose `isNullable || newType.isNullable` correct?
        return EvaluatorValue.Type(newType.type/*, isNullable || newType.isNullable*/);
    }

    private ArrayTypeSymbol SubstituteArrayType(ArrayTypeSymbol type) {
        var oldElement = type.elementTypeWithAnnotations;
        var element = SubstituteTypeWithAnnotationsAsType(oldElement);

        if (element.IsSameAs(oldElement))
            return type;

        if (type.isSZArray)
            return ArrayTypeSymbol.CreateSZArray(element, type.baseType);

        return ArrayTypeSymbol.CreateMDArray(
            element,
            type.rank,
            type.sizes,
            type.lowerBounds,
            type.baseType
        );
    }

    private FunctionTypeSymbol SubstituteFunctionType(FunctionTypeSymbol f) {
        var substitutedReturnType = SubstituteTypeWithAnnotationsAsType(f.signature.returnTypeWithAnnotations);

        var parameterTypesWithAnnotations = f.signature.parameterTypesWithAnnotations;
        var substitutedParamTypes = SubstituteTypes(parameterTypesWithAnnotations);

        if (!CollectionsEqual(substitutedParamTypes, parameterTypesWithAnnotations) ||
            !f.signature.returnTypeWithAnnotations.IsSameAs(substitutedReturnType)) {
            f = f.SubstituteTypeSymbol(substitutedReturnType, substitutedParamTypes);
        }

        return f;
    }

    private ImmutableArray<TypeOrConstant> SubstituteTypes(ImmutableArray<TypeWithAnnotations> original) {
        if (original.IsDefault)
            return default;

        var result = ArrayBuilder<TypeOrConstant>.GetInstance(original.Length);

        foreach (var type in original)
            result.Add(new TypeOrConstant(SubstituteTypeWithAnnotationsAsType(type)));

        return result.ToImmutableAndFree();
    }

    private static bool CollectionsEqual(
        ImmutableArray<TypeOrConstant> collection1,
        ImmutableArray<TypeWithAnnotations> collection2) {
        if (collection1.Length != collection2.Length)
            return false;

        for (var i = 0; i < collection1.Length; i++) {
            var typeOrConstant = collection1[i];
            var typeWithAnnotations = collection2[i];

            if (!typeOrConstant.isType)
                return false;

            if (!typeOrConstant.type.Equals(typeWithAnnotations))
                return false;
        }

        return true;
    }
}
