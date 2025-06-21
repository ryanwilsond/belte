using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PEFieldSymbol : FieldSymbol {
    private readonly FieldDefinitionHandle _handle;
    private readonly string _name;
    private readonly FieldAttributes _flags;
    private readonly PENamedTypeSymbol _containingType;
    private ConstantValue _lazyConstantValue = ConstantValue.Unset;

    private TypeWithAnnotations _lazyType;
    private PackedFlags _packedFlags;

    internal PEFieldSymbol(
        PEModuleSymbol moduleSymbol,
        PENamedTypeSymbol containingType,
        FieldDefinitionHandle fieldDef) {
        _handle = fieldDef;
        _containingType = containingType;
        _packedFlags = new PackedFlags();

        try {
            moduleSymbol.module.GetFieldDefPropsOrThrow(fieldDef, out _name, out _flags);
        } catch (BadImageFormatException) {
            _name ??= "";
        }
    }

    public override string name => _name;

    public override RefKind refKind {
        get {
            EnsureSignatureIsLoaded();
            return _packedFlags.refKind;
        }
    }

    public override bool isConst => (_flags & FieldAttributes.InitOnly) != 0;

    public override bool isConstExpr {
        get {
            return (_flags & FieldAttributes.Literal) != 0 ||
                GetConstantValue(ConstantFieldsInProgress.Empty) is not null;
        }
    }

    internal override Symbol containingSymbol => _containingType;

    internal override NamedTypeSymbol containingType => _containingType;

    internal FieldAttributes flags => _flags;

    internal FieldDefinitionHandle handle => _handle;

    private PEModuleSymbol _containingPEModule => ((PENamespaceSymbol)containingNamespace).containingPEModule;

    internal override ImmutableArray<TextLocation> locations
        => _containingType.containingPEModule.metadataLocation.Cast<MetadataLocation, TextLocation>();

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => locations[0];

    internal override Accessibility declaredAccessibility {
        get {
            // TODO We have limited ways to represent full .NET accessibility
            var access = (_flags & FieldAttributes.FieldAccessMask) switch {
                FieldAttributes.Assembly => Accessibility.Private,// access = Accessibility.Internal;
                FieldAttributes.FamORAssem => Accessibility.Private,// access = Accessibility.ProtectedOrInternal;
                FieldAttributes.FamANDAssem => Accessibility.Private,// access = Accessibility.ProtectedAndInternal;
                FieldAttributes.Private or FieldAttributes.PrivateScope => Accessibility.Private,
                FieldAttributes.Public => Accessibility.Public,
                FieldAttributes.Family => Accessibility.Protected,
                _ => Accessibility.Private,
            };

            return access;
        }
    }

    internal override bool isStatic => (_flags & FieldAttributes.Static) != 0;

    internal sealed override Compilation declaringCompilation => null;

    private void EnsureSignatureIsLoaded() {
        if (_lazyType is null) {
            var moduleSymbol = _containingType.containingPEModule;
            var fieldInfo = new MetadataDecoder(moduleSymbol, _containingType).DecodeFieldSignature(_handle);
            var typeSymbol = fieldInfo.type;

            var type = new TypeWithAnnotations(typeSymbol);

            type = NullableTypeDecoder.TransformType(
                type,
                _handle,
                moduleSymbol,
                accessSymbol: this,
                nullableContext: _containingType
            );

            var refKind = fieldInfo.isByRef
                ? moduleSymbol.module.HasIsReadOnlyAttribute(_handle) ? RefKind.RefConst : RefKind.Ref
                : RefKind.None;

            _packedFlags.SetRefKind(refKind);
            Interlocked.CompareExchange(ref _lazyType, type, null);
        }
    }

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        EnsureSignatureIsLoaded();
        return _lazyType;
    }

    internal override ConstantValue GetConstantValue(ConstantFieldsInProgress inProgress) {
        if (_lazyConstantValue == ConstantValue.Unset) {
            ConstantValue value = null;

            if ((_flags & FieldAttributes.Literal) != 0)
                value = _containingType.containingPEModule.module.GetConstantFieldValue(_handle);

            Interlocked.CompareExchange(
                ref _lazyConstantValue,
                value,
                ConstantValue.Unset
            );
        }

        return _lazyConstantValue;
    }
}
