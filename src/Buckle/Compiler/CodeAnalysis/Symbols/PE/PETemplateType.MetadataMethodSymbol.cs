using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using TemplateMethodDecoder = Buckle.CodeAnalysis.TemplateMetadataReader.TemplateMetadata.TemplateMethodDecoder;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PETemplateType {
    internal sealed class MetadataMethodSymbol : MethodSymbol {
        private readonly TemplateMethodDecoder _decoder;
        private readonly string _name;
        private readonly ushort _arity;
        private readonly PETemplateType _containingType;
        private readonly TemplateMetadataWriter.ReturnFlags _returnFlags;
        private readonly TypeWithAnnotations _returnType;
        private readonly MethodAttributes _flags;
        private readonly TemplateMetadataWriter.MethodFlags _additionalFlags;

        private bool _lazyMethodKindIsPopulated;
        private MethodKind _lazyMethodKind;
        private ImmutableArray<TemplateParameterSymbol> _lazyTemplateParameters;
        private ImmutableArray<ParameterSymbol> _lazyParameters;

        private bool _lazyMethodBodyIsPopulated;
        private BoundBlockStatement _lazyMethodBody;

        private readonly MethodSymbol _methodToLink;

        internal MetadataMethodSymbol(PETemplateType containingType, TemplateMethodDecoder decoder) {
            decoder.SetEnclosingContext(this);
            _containingType = containingType;
            _decoder = decoder;
            _name = decoder.GetMetadataName();
            _returnFlags = decoder.GetReturnFlags();
            _flags = decoder.GetFlags();
            _arity = decoder.GetArity();
            _additionalFlags = decoder.GetAdditionalFlags();
            _returnType = new TypeWithAnnotations(decoder.GetReturnType());
        }

        internal MetadataMethodSymbol(
            PETemplateType containingType,
            TemplateMethodDecoder decoder,
            TypeSymbol typeToLink)
            : this(containingType, decoder) {
            var candidates = typeToLink.GetMembers(_name);

            foreach (var candidate in candidates) {
                if (candidate is not MethodSymbol m)
                    continue;

                if (m.arity != _arity ||
                    m.returnsByRef != returnsByRef ||
                    !m.returnType.Equals(_returnType.type, TypeCompareKind.ConsiderEverything)) {
                    continue;
                }

                if (m.parameterCount != parameterCount)
                    continue;

                var sameSignature = true;

                for (var i = 0; i < m.parameterCount; i++) {
                    var param1 = m.parameters[i];
                    var param2 = parameters[i];

                    if (!param1.type.Equals(param2.type, TypeCompareKind.ConsiderEverything)) {
                        sameSignature = false;
                        break;
                    }
                }

                if (sameSignature) {
                    _methodToLink = m;
                    break;
                }
            }

            Debug.Assert(_methodToLink is not null);
        }

        public override int arity => _arity;

        public override string name => _name;

        public override RefKind refKind
            => (_returnFlags & TemplateMetadataWriter.ReturnFlags.ByRef) != 0 ? RefKind.Ref : RefKind.None;

        public override bool returnsVoid => returnType.IsVoidType();

        // TODO
        public override ImmutableArray<BoundExpression> templateConstraints => [];

        public override ImmutableArray<TemplateParameterSymbol> templateParameters
            => EnsureTemplateParametersAreLoaded();

        public override ImmutableArray<TypeOrConstant> templateArguments
            => isTemplateMethod ? GetTemplateParametersAsTemplateArguments() : [];

        public override MethodKind methodKind {
            get {
                if (!_lazyMethodKindIsPopulated) {
                    ComputeMethodKind();
                    Debug.Assert(_lazyMethodKindIsPopulated);
                }

                return _lazyMethodKind;
            }
        }

        internal override Symbol containingSymbol => _containingType;

        internal override NamedTypeSymbol containingType => _containingType;

        internal override ImmutableArray<ParameterSymbol> parameters => EnsureParametersAreLoaded();

        internal override TypeWithAnnotations returnTypeWithAnnotations => _returnType;

        internal override MethodSymbol originalDefinition => _methodToLink ?? base.originalDefinition;

        internal override ModuleSymbol containingModule => _containingType.containingModule;

        internal override bool hasSpecialName => HasFlag(MethodAttributes.SpecialName);

        internal override bool hasRuntimeSpecialName => HasFlag(MethodAttributes.RTSpecialName);

        internal override bool isExtern => HasFlag(MethodAttributes.PinvokeImpl);

        internal MethodAttributes flags => (MethodAttributes)_flags;

        internal override ImmutableArray<TextLocation> locations
            => _containingType.containingPEModule.metadataLocation.Cast<MetadataLocation, TextLocation>();

        internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

        internal override TextLocation location => locations[0];

        internal override SyntaxReference syntaxReference => null;

        internal override bool isAbstract => HasFlag(MethodAttributes.Abstract);

        internal override bool isVirtual
            => IsMetadataVirtual() && !_isFinalizer && !isMetadataFinal && !isAbstract &&
                (_containingType.isInterface ? (isStatic || IsMetadataNewSlot()) : !isOverride);

        internal override bool isOverride
            => !_containingType.isInterface &&
                IsMetadataVirtual() && !_isFinalizer &&
                ((!IsMetadataNewSlot() && _containingType.baseType is not null) || _isExplicitClassOverride);

        internal override bool isStatic => HasFlag(MethodAttributes.Static);

        private bool _isExplicitClassOverride {
            get {
                // TODO
                return false;
            }
        }

        internal override ImmutableArray<MethodSymbol> explicitInterfaceImplementations {
            get {
                // TODO
                return [];
            }
        }

        internal override bool hidesBaseMethodsByName => !HasFlag(MethodAttributes.HideBySig);

        // TODO TODO
        internal override CallingConvention callingConvention => CallingConvention.Default;

        private bool _isFinalizer => methodKind == MethodKind.Finalizer;

        internal override Accessibility declaredAccessibility {
            get {
                return (object)(flags & MethodAttributes.MemberAccessMask) switch {
                    MethodAttributes.Assembly => Accessibility.Public,// return Accessibility.Internal;
                    MethodAttributes.FamORAssem => Accessibility.Public,// return Accessibility.ProtectedOrInternal;
                    MethodAttributes.FamANDAssem => Accessibility.Public,// return Accessibility.ProtectedAndInternal;
                    MethodAttributes.Private or MethodAttributes.PrivateScope => Accessibility.Private,
                    MethodAttributes.Public => Accessibility.Public,
                    MethodAttributes.Family => Accessibility.Protected,
                    _ => Accessibility.Private,
                };
            }
        }

        internal override OverriddenOrHiddenMembersResult overriddenOrHiddenMembers {
            get {
                // TODO
                return null;
            }
        }

        internal override bool hasMustUseReturnValueAttribute => false;

        internal sealed override bool hasUnscopedRefAttribute {
            get {
                // TODO
                return false;
            }
        }

        internal override bool isDeclaredConst {
            get {
                // TODO
                return false;
            }
        }

        internal override bool isSealed
            => isMetadataFinal &&
                (_containingType.isInterface
                    ? isAbstract && IsMetadataVirtual() && !IsMetadataNewSlot()
                    : !isAbstract && isOverride);

        internal override bool isMetadataFinal => HasFlag(MethodAttributes.Final);

        internal override int parameterCount => _decoder.GetParameterCount();

        internal override bool isPure {
            get {
                // TODO
                return false;
            }
        }

        internal override bool isNoThrow {
            get {
                // TODO
                return false;
            }
        }

        internal override bool isNoAlloc {
            get {
                // TODO
                return false;
            }
        }

        internal BoundBlockStatement TryDecodeMethodBody() {
            if (!_lazyMethodBodyIsPopulated) {
                var body = _decoder.DecodeMethodBody(this);
                Debug.Assert(body is not null);

                if (Interlocked.CompareExchange(ref _lazyMethodBody, body, null) is null)
                    Interlocked.Exchange(ref _lazyMethodBodyIsPopulated, true);

                Debug.Assert(_lazyMethodBodyIsPopulated);
            }

            return _lazyMethodBody;
        }

        private void ComputeMethodKind() {
            var kind = ComputeMethodKindCore();

            if (!_lazyMethodKindIsPopulated) {
                if (Interlocked.Exchange(ref _lazyMethodKindIsPopulated, true) == false)
                    Interlocked.Exchange(ref _lazyMethodKind, kind);
            }
        }

        private MethodKind ComputeMethodKindCore() {
            if (hasSpecialName) {
                if (_name.StartsWith(".", StringComparison.Ordinal)) {
                    if ((_flags & (MethodAttributes.RTSpecialName | MethodAttributes.Virtual)) == MethodAttributes.RTSpecialName &&
                        _name.Equals(isStatic ? WellKnownMemberNames.StaticConstructorName : WellKnownMemberNames.InstanceConstructorName) &&
                        returnsVoid && arity == 0) {
                        if (isStatic) {
                            if (parameters.Length == 0)
                                return MethodKind.StaticConstructor;
                        } else {
                            return MethodKind.Constructor;
                        }
                    }

                    return MethodKind.Ordinary;
                }

                if (!hasRuntimeSpecialName && isStatic && declaredAccessibility == Accessibility.Public) {
                    switch (_name) {
                        case WellKnownMemberNames.AdditionOperatorName:
                        case WellKnownMemberNames.BitwiseAndOperatorName:
                        case WellKnownMemberNames.BitwiseOrOperatorName:
                        case WellKnownMemberNames.DivideOperatorName:
                        case WellKnownMemberNames.EqualityOperatorName:
                        case WellKnownMemberNames.BitwiseExclusiveOrOperatorName:
                        case WellKnownMemberNames.GreaterThanOperatorName:
                        case WellKnownMemberNames.GreaterThanOrEqualOperatorName:
                        case WellKnownMemberNames.InequalityOperatorName:
                        case WellKnownMemberNames.LeftShiftOperatorName:
                        case WellKnownMemberNames.LessThanOperatorName:
                        case WellKnownMemberNames.LessThanOrEqualOperatorName:
                        case WellKnownMemberNames.ModulusOperatorName:
                        case WellKnownMemberNames.MultiplyOperatorName:
                        case WellKnownMemberNames.RightShiftOperatorName:
                        case WellKnownMemberNames.UnsignedRightShiftOperatorName:
                        case WellKnownMemberNames.SubtractionOperatorName:
                            return IsValidUserDefinedOperatorSignature(2) ? MethodKind.Operator : MethodKind.Ordinary;
                        case WellKnownMemberNames.DecrementOperatorName:
                        case WellKnownMemberNames.IncrementOperatorName:
                        case WellKnownMemberNames.LogicalNotOperatorName:
                        case WellKnownMemberNames.BitwiseNotOperatorName:
                        case WellKnownMemberNames.UnaryNegationOperatorName:
                        case WellKnownMemberNames.UnaryPlusOperatorName:
                            return IsValidUserDefinedOperatorSignature(1) ? MethodKind.Operator : MethodKind.Ordinary;
                        case WellKnownMemberNames.ImplicitConversionName:
                        case WellKnownMemberNames.ExplicitConversionName:
                            return IsValidUserDefinedOperatorSignature(1) ? MethodKind.Conversion : MethodKind.Ordinary;

                            //case WellKnownMemberNames.ConcatenateOperatorName:
                            //case WellKnownMemberNames.ExponentOperatorName:
                            //case WellKnownMemberNames.IntegerDivisionOperatorName:
                            //case WellKnownMemberNames.LikeOperatorName:
                            //// Non-C#-supported overloaded operator
                            // return MethodKind.Ordinary;
                    }

                    return MethodKind.Ordinary;
                }
            }

            return MethodKind.Ordinary;
        }

        private bool IsValidUserDefinedOperatorSignature(int parameterCount) {
            if (returnsVoid || isTemplateMethod || this.parameterCount != parameterCount)
                return false;

            if (parameterRefKinds.IsDefault)
                return true;

            foreach (var kind in parameterRefKinds) {
                switch (kind) {
                    case RefKind.None:
                        continue;
                    case RefKind.Ref:
                    case RefKind.Out:
                    case RefKind.RefConst:
                    case RefKind.RefFinal:
                        return false;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(kind);
                }
            }

            return true;
        }

        private ImmutableArray<TemplateParameterSymbol> EnsureTemplateParametersAreLoaded() {
            var typeParams = _lazyTemplateParameters;

            if (!typeParams.IsDefault)
                return typeParams;

            return InterlockedOperations.Initialize(ref _lazyTemplateParameters, LoadTemplateParameters());
        }

        private ImmutableArray<TemplateParameterSymbol> LoadTemplateParameters() {
            var ownedParams = ArrayBuilder<TemplateParameterSymbol>.GetInstance(arity);
            ownedParams.Count = arity;

            for (var i = 0; i < ownedParams.Count; i++) {
                if (_methodToLink is null) {
                    ownedParams[i] = new MetadataTemplateParameterSymbol(_decoder, this, (ushort)i);
                } else {
                    ownedParams[i] = new MetadataTemplateParameterSymbol(
                        _decoder,
                        this,
                        (ushort)i,
                        _methodToLink.templateParameters[i]
                    );
                }
            }

            return ownedParams.ToImmutableAndFree();
        }

        private ImmutableArray<ParameterSymbol> EnsureParametersAreLoaded() {
            var parameters = _lazyParameters;

            if (!parameters.IsDefault)
                return parameters;

            return InterlockedOperations.Initialize(ref _lazyParameters, LoadParameters());
        }

        private ImmutableArray<ParameterSymbol> LoadParameters() {
            var ownedParams = ArrayBuilder<ParameterSymbol>.GetInstance(arity);
            ownedParams.Count = _decoder.GetParameterCount();

            for (var i = 0; i < ownedParams.Count; i++) {
                if (_methodToLink is null) {
                    ownedParams[i] = new MetadataParameterSymbol(_decoder, this, (ushort)i);
                } else {
                    ownedParams[i] = new MetadataParameterSymbol(
                        _decoder,
                        this,
                        (ushort)i,
                        _methodToLink.parameters[i]
                    );
                }
            }

            return ownedParams.ToImmutableAndFree();
        }

        private bool HasFlag(MethodAttributes flag) {
            return ((ushort)flag & (ushort)_flags) != 0;
        }

        internal override DllImportData GetDllImportData() {
            // TODO
            // return HasFlag(MethodAttributes.PinvokeImpl)
            // ? _containingType.containingPEModule.module.GetDllImportData(_handle)
            // : null;
            return null;
        }

        internal override bool IsMetadataVirtual(bool forceComplete = false) => HasFlag(MethodAttributes.Virtual);

        internal bool IsMetadataNewSlot(bool ignoreInterfaceImplementationChanges = false)
            => HasFlag(MethodAttributes.NewSlot);

        internal override int CalculateLocalSyntaxOffset(int localPosition, SyntaxTree localTree) {
            throw ExceptionUtilities.Unreachable();
        }

        internal override UnmanagedCallersOnlyAttributeData GetUnmanagedCallersOnlyAttributeData(bool forceComplete) {
            // TODO
            return null;
        }
    }
}
