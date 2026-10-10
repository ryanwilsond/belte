using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Threading;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PEAttributeData : AttributeData {
    private readonly MetadataDecoder _decoder;
    private readonly CustomAttributeHandle _handle;
    private NamedTypeSymbol _lazyAttributeClass = ErrorTypeSymbol.UnknownResultType;
    private MethodSymbol _lazyAttributeConstructor;
    private ImmutableArray<TypedConstant> _lazyConstructorArguments;
    private ImmutableArray<KeyValuePair<string, TypedConstant>> _lazyNamedArguments;
    private ThreeState _lazyHasErrors = ThreeState.Unknown;

    internal PEAttributeData(PEModuleSymbol moduleSymbol, CustomAttributeHandle handle) {
        _decoder = new MetadataDecoder(moduleSymbol);
        _handle = handle;
    }

    internal override NamedTypeSymbol attributeClass {
        get {
            EnsureClassAndConstructorSymbolsAreLoaded();
            return _lazyAttributeClass;
        }
    }

    internal override MethodSymbol attributeConstructor {
        get {
            EnsureClassAndConstructorSymbolsAreLoaded();
            return _lazyAttributeConstructor;
        }
    }

    protected internal override ImmutableArray<KeyValuePair<string, TypedConstant>> _commonNamedArguments {
        get {
            EnsureAttributeArgumentsAreLoaded();
            return _lazyNamedArguments;
        }
    }

    protected internal override ImmutableArray<TypedConstant> _commonConstructorArguments {
        get {
            EnsureAttributeArgumentsAreLoaded();
            return _lazyConstructorArguments;
        }
    }

    protected internal override INamedTypeSymbol _commonAttributeClass => attributeClass;

    protected internal override IMethodSymbol _commonAttributeConstructor => attributeConstructor;

    internal override bool hasErrors {
        get {
            if (_lazyHasErrors == ThreeState.Unknown) {
                EnsureClassAndConstructorSymbolsAreLoaded();
                EnsureAttributeArgumentsAreLoaded();

                if (_lazyHasErrors == ThreeState.Unknown)
                    _lazyHasErrors = ThreeState.False;
            }

            return _lazyHasErrors.Value();
        }
    }

    internal override int GetTargetAttributeSignatureIndex(AttributeDescription description) {
        return _decoder.GetTargetAttributeSignatureIndex(_handle, description);
    }

    internal override TextLocation GetAttributeArgumentLocation(int parameterIndex) {
        return new MetadataLocation(_decoder.moduleSymbol);
    }

    internal override bool IsTargetAttribute(string namespaceName, string typeName) {
        return _decoder.IsTargetAttribute(_handle, namespaceName, typeName);
    }

    private void EnsureClassAndConstructorSymbolsAreLoaded() {
        if ((object)_lazyAttributeClass == ErrorTypeSymbol.UnknownResultType) {
            if (!_decoder.GetCustomAttribute(_handle, out var attributeClass, out var attributeConstructor))
                _lazyHasErrors = ThreeState.True;
            else if (attributeClass is null || attributeClass.IsErrorType() || attributeConstructor is null)
                _lazyHasErrors = ThreeState.True;

            Interlocked.CompareExchange(ref _lazyAttributeConstructor, attributeConstructor, null);
            Interlocked.CompareExchange(
                ref _lazyAttributeClass,
                (NamedTypeSymbol)attributeClass,
                ErrorTypeSymbol.UnknownResultType
            );
        }
    }

    private void EnsureAttributeArgumentsAreLoaded() {
        // TODO Volatile read?
        if (_lazyConstructorArguments.IsDefault || _lazyNamedArguments.IsDefault) {
            if (!_decoder.GetCustomAttribute(
                _handle,
                attributeConstructor,
                out var lazyConstructorArguments,
                out var lazyNamedArguments)) {
                _lazyHasErrors = ThreeState.True;
            }

            Debug.Assert(lazyConstructorArguments is not null && lazyNamedArguments is not null);

            ImmutableInterlocked.InterlockedInitialize(
                ref _lazyConstructorArguments,
                ImmutableArray.Create(lazyConstructorArguments)
            );

            ImmutableInterlocked.InterlockedInitialize(
                ref _lazyNamedArguments,
                ImmutableArray.Create(lazyNamedArguments)
            );
        }
    }
}
