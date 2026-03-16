using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PETemplateParameterSymbol : TemplateParameterSymbol {
    private readonly Symbol _containingSymbol;
    private readonly GenericParameterHandle _handle;
    private readonly string _name;
    private readonly ushort _ordinal;

    private readonly GenericParameterAttributes _flags;
    private ThreeState _lazyHasIsUnmanagedConstraint;
    private TypeParameterBounds _lazyBounds = TypeParameterBounds.Unset;
    private ImmutableArray<TypeWithAnnotations> _lazyDeclaredConstraintTypes;

    internal PETemplateParameterSymbol(
        PEModuleSymbol moduleSymbol,
        PENamedTypeSymbol definingNamedType,
        ushort ordinal,
        GenericParameterHandle handle)
        : this(moduleSymbol, (Symbol)definingNamedType, ordinal, handle) {
    }

    internal PETemplateParameterSymbol(
        PEModuleSymbol moduleSymbol,
        PEMethodSymbol definingMethod,
        ushort ordinal,
        GenericParameterHandle handle)
        : this(moduleSymbol, (Symbol)definingMethod, ordinal, handle) {
    }

    private PETemplateParameterSymbol(
        PEModuleSymbol moduleSymbol,
        Symbol definingSymbol,
        ushort ordinal,
        GenericParameterHandle handle) {
        _containingSymbol = definingSymbol;

        GenericParameterAttributes flags = 0;

        try {
            moduleSymbol.module.GetGenericParamPropsOrThrow(handle, out _name, out flags);
        } catch (BadImageFormatException) {
            _name ??= "";
        }

        _flags = ((flags & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0)
            ? flags
            : (flags & ~GenericParameterAttributes.DefaultConstructorConstraint);

        _ordinal = ordinal;
        _handle = handle;
        containingModule = moduleSymbol;
    }

    public override string name => _name;

    internal override TemplateParameterKind templateParameterKind
        => containingSymbol.kind == SymbolKind.Method
            ? TemplateParameterKind.Method
            : TemplateParameterKind.Type;

    internal override int ordinal => _ordinal;

    internal override PEModuleSymbol containingModule { get; }

    internal GenericParameterHandle handle => _handle;

    internal override Symbol containingSymbol => _containingSymbol;

    internal override AssemblySymbol containingAssembly => _containingSymbol.containingAssembly;

    internal override ImmutableArray<TextLocation> locations => _containingSymbol.locations;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal sealed override Compilation declaringCompilation => null;

    internal override bool isOptional => false;

    internal override bool hasPrimitiveTypeConstraint
        => (_flags & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0;

    internal override bool hasObjectTypeConstraint
        => (_flags & GenericParameterAttributes.ReferenceTypeConstraint) != 0;

    internal override bool isObjectTypeFromConstraintTypes
        => CalculateIsObjectTypeFromConstraintTypes(constraintTypes);

    internal override bool isPrimitiveTypeFromConstraintTypes
        => CalculateIsPrimitiveTypeFromConstraintTypes(constraintTypes);

    internal override TextLocation location => locations[0];

    internal override SyntaxReference syntaxReference => null;

    internal override TypeWithAnnotations underlyingType
        => new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Type));

    internal override TypeOrConstant defaultValue => null;

    internal override ImmutableArray<AttributeData> GetAttributes() {
        // TODO
        // if (_lazyCustomAttributes.IsDefault) {
        //     var containingPEModuleSymbol = (PEModuleSymbol)this.ContainingModule;

        //     var loadedCustomAttributes = containingPEModuleSymbol.GetCustomAttributesForToken(
        //         Handle,
        //         out _,
        //         // Filter out [IsUnmanagedAttribute]
        //         HasUnmanagedTypeConstraint ? AttributeDescription.IsUnmanagedAttribute : default);

        //     ImmutableInterlocked.InterlockedInitialize(ref _lazyCustomAttributes, loadedCustomAttributes);
        // }

        // return _lazyCustomAttributes;
        return [];
    }

    private ImmutableArray<TypeWithAnnotations> GetDeclaredConstraintTypes(
        ConsList<PETemplateParameterSymbol> inProgress) {
        if (_lazyDeclaredConstraintTypes.IsDefault) {
            ImmutableArray<TypeWithAnnotations> declaredConstraintTypes;

            var moduleSymbol = containingModule;
            var peModule = moduleSymbol.module;
            var constraints = GetConstraintHandleCollection(peModule);

            var hasUnmanagedModreqPattern = false;

            if (constraints.Count > 0) {
                var symbolsBuilder = ArrayBuilder<TypeWithAnnotations>.GetInstance();
                var tokenDecoder = GetDecoder(moduleSymbol);

                // TypeWithAnnotations bestObjectConstraint = default;

                var metadataReader = peModule.metadataReader;

                foreach (var constraintHandle in constraints) {
                    var type = GetConstraintTypeOrDefault(
                        moduleSymbol,
                        metadataReader,
                        tokenDecoder,
                        constraintHandle,
                        ref hasUnmanagedModreqPattern
                    );

                    if (!type.hasType)
                        continue;

                    // if (ConstraintsHelpers.IsObjectConstraint(type, ref bestObjectConstraint))
                    //     continue;

                    symbolsBuilder.Add(type);
                }

                // TODO
                // if (bestObjectConstraint.hasType) {
                //     if (ConstraintsHelpers.IsObjectConstraintSignificant(
                //         CalculateIsNotNullableFromNonTypeConstraints(),
                //         bestObjectConstraint)) {
                //         if (symbolsBuilder.Count != 0) {
                //             inProgress = inProgress.Prepend(this);

                //             foreach (var constraintType in symbolsBuilder) {
                //                 if (!ConstraintsHelpers.IsObjectConstraintSignificant(
                //                     IsNotNullableFromConstraintType(constraintType, inProgress, out _),
                //                     bestObjectConstraint)) {
                //                     bestObjectConstraint = default;
                //                     break;
                //                 }
                //             }
                //         }

                //         if (bestObjectConstraint.hasType)
                //             symbolsBuilder.Insert(0, bestObjectConstraint);
                //     }
                // }

                declaredConstraintTypes = symbolsBuilder.ToImmutableAndFree();
            } else {
                declaredConstraintTypes = [];
            }

            if (hasUnmanagedModreqPattern && (_flags & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0
                || hasUnmanagedModreqPattern != peModule.HasIsUnmanagedAttribute(_handle)) {
                hasUnmanagedModreqPattern = false;
            }

            _lazyHasIsUnmanagedConstraint = hasUnmanagedModreqPattern.ToThreeState();
            ImmutableInterlocked.InterlockedInitialize(ref _lazyDeclaredConstraintTypes, declaredConstraintTypes);
        }

        return _lazyDeclaredConstraintTypes;
    }

    private MetadataDecoder GetDecoder(PEModuleSymbol moduleSymbol) {
        var tokenDecoder = _containingSymbol.kind == SymbolKind.Method
            ? new MetadataDecoder(moduleSymbol, (PEMethodSymbol)_containingSymbol)
            : new MetadataDecoder(moduleSymbol, (PENamedTypeSymbol)_containingSymbol);

        return tokenDecoder;
    }

    private TypeWithAnnotations GetConstraintTypeOrDefault(
        PEModuleSymbol moduleSymbol,
        MetadataReader metadataReader,
        MetadataDecoder tokenDecoder,
        GenericParameterConstraintHandle constraintHandle,
        ref bool hasUnmanagedModreqPattern) {
        // var constraint = metadataReader.GetGenericParameterConstraint(constraintHandle);
        // var typeSymbol = tokenDecoder.DecodeGenericParameterConstraint(constraint.Type, out var modifiers);

        // if (!modifiers.IsDefaultOrEmpty && modifiers.Length > 1) {
        //     typeSymbol = new UnsupportedMetadataTypeSymbol();
        // } else if (typeSymbol.SpecialType == SpecialType.System_ValueType) {
        //     // recognize "(class [mscorlib]System.ValueType modreq([mscorlib]System.Runtime.InteropServices.UnmanagedType" pattern as "unmanaged"
        //     if (!modifiers.IsDefaultOrEmpty) {
        //         ModifierInfo<TypeSymbol> m = modifiers.Single();
        //         if (!m.IsOptional && m.Modifier.IsWellKnownTypeUnmanagedType()) {
        //             hasUnmanagedModreqPattern = true;
        //         } else {
        //             // Any other modifiers, optional or not, are not allowed: http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/528856
        //             typeSymbol = new UnsupportedMetadataTypeSymbol();
        //         }
        //     }

        //     // Drop 'System.ValueType' constraint type if the 'valuetype' constraint was also specified.
        //     if (typeSymbol.SpecialType == SpecialType.System_ValueType && ((_flags & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)) {
        //         return default;
        //     }
        // } else if (!modifiers.IsDefaultOrEmpty) {
        //     typeSymbol = new UnsupportedMetadataTypeSymbol();
        // }

        // var type = TypeWithAnnotations.Create(typeSymbol);
        // type = NullableTypeDecoder.TransformType(type, constraintHandle, moduleSymbol, accessSymbol: _containingSymbol, nullableContext: _containingSymbol);
        // type = TupleTypeDecoder.DecodeTupleTypesIfApplicable(type, constraintHandle, moduleSymbol);
        // return type;
        // TODO
        return null;
    }

    private GenericParameterConstraintHandleCollection GetConstraintHandleCollection(PEModule module) {
        GenericParameterConstraintHandleCollection constraints;

        try {
            constraints = module.metadataReader.GetGenericParameter(_handle).GetConstraints();
        } catch (BadImageFormatException) {
            constraints = default;
        }

        return constraints;
    }

    private GenericParameterConstraintHandleCollection GetConstraintHandleCollection() {
        return GetConstraintHandleCollection(containingModule.module);
    }

    internal override void EnsureConstraintsAreResolved() {
        if (!_lazyBounds.IsSet()) {
            var typeParameters = (_containingSymbol.kind == SymbolKind.Method)
                ? ((PEMethodSymbol)_containingSymbol).templateParameters
                : ((PENamedTypeSymbol)_containingSymbol).templateParameters;

            EnsureConstraintsAreResolved(typeParameters);
        }
    }

    internal override ImmutableArray<TypeWithAnnotations> GetConstraintTypes(
        ConsList<TemplateParameterSymbol> inProgress) {
        var bounds = GetBounds(inProgress);
        return (bounds is not null) ? bounds.constraintTypes : [];
    }

    internal override NamedTypeSymbol GetEffectiveBaseClass(ConsList<TemplateParameterSymbol> inProgress) {
        var bounds = GetBounds(inProgress);
        return (bounds is not null) ? bounds.effectiveBaseClass : GetDefaultBaseType();
    }

    internal override TypeSymbol GetDeducedBaseType(ConsList<TemplateParameterSymbol> inProgress) {
        var bounds = GetBounds(inProgress);
        return (bounds is not null) ? bounds.deducedBaseType : GetDefaultBaseType();
    }

    private TypeParameterBounds GetBounds(ConsList<TemplateParameterSymbol> inProgress) {
        if (_lazyBounds == TypeParameterBounds.Unset) {
            var constraintTypes = GetDeclaredConstraintTypes(ConsList<PETemplateParameterSymbol>.Empty);
            var diagnostics = BelteDiagnosticQueue.GetInstance();

            var inherited = (_containingSymbol.kind == SymbolKind.Method) &&
                ((MethodSymbol)_containingSymbol).isOverride;

            var bounds = this.ResolveBounds(
                inProgress.Prepend(this),
                constraintTypes,
                inherited,
                null,
                diagnostics,
                null
            );

            diagnostics.Free();
            Interlocked.CompareExchange(ref _lazyBounds, bounds, TypeParameterBounds.Unset);
        }

        return _lazyBounds;
    }

    private NamedTypeSymbol GetDefaultBaseType() {
        return CorLibrary.GetSpecialType(SpecialType.Object);
    }
}
