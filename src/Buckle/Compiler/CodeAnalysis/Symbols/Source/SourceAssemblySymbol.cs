using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using AssemblyFlags = System.Reflection.AssemblyFlags;
using CommonAssemblyWellKnownAttributeData = Buckle.CodeAnalysis.Symbols.CommonAssemblyWellKnownAttributeData<Buckle.CodeAnalysis.Symbols.NamedTypeSymbol>;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SourceAssemblySymbol : MetadataOrSourceAssemblySymbol {
    [ThreadStatic]
    private static AssemblySymbol AssemblyForWhichCurrentThreadIsComputingKeys;

    private readonly string _assemblySimpleName;

    private CustomAttributesBag<AttributeData> _lazySourceAttributesBag;
    private CustomAttributesBag<AttributeData> _lazyNetModuleAttributesBag;
    private StrongNameKeys _lazyStrongNameKeys;
    private NamespaceSymbol _lazyGlobalNamespace;
    private AssemblyIdentity _lazyAssemblyIdentity;
    private ConcurrentSet<int> _lazyOmittedAttributeIndices;

    private SymbolCompletionState _state;

    internal SourceAssemblySymbol(Compilation compilation, string assemblySimpleName) {
        declaringCompilation = compilation;
        _assemblySimpleName = assemblySimpleName;
    }

    internal override Compilation declaringCompilation { get; }

    internal override AssemblyIdentity identity {
        get {
            if (_lazyAssemblyIdentity is null)
                Interlocked.CompareExchange(ref _lazyAssemblyIdentity, ComputeIdentity(), null);

            return _lazyAssemblyIdentity;
        }
    }

    internal override NamespaceSymbol globalNamespace {
        get {
            if (_lazyGlobalNamespace is null) {
                var diagnostics = BelteDiagnosticQueue.GetInstance();
                var result = new SourceNamespaceSymbol(
                    this,
                    this,
                    declaringCompilation.mergedRootDeclaration,
                    diagnostics
                );

                Interlocked.CompareExchange(ref _lazyGlobalNamespace, result, null);

                AddDeclarationDiagnostics(diagnostics);
                diagnostics.Free();
            }

            return _lazyGlobalNamespace;
        }
    }

    private Version _assemblyVersionAttributeSetting {
        get {
            var defaultValue = (Version)null;
            var fieldValue = defaultValue;

            var data = GetSourceDecodedWellKnownAttributeData();

            if (data is not null)
                fieldValue = data.assemblyVersionAttributeSetting;

            if (fieldValue == defaultValue) {
                data = GetNetModuleDecodedWellKnownAttributeData();

                if (data is not null)
                    fieldValue = data.assemblyVersionAttributeSetting;
            }

            return fieldValue;
        }
    }

    internal StrongNameKeys strongNameKeys {
        get {
            if (_lazyStrongNameKeys is null) {
                try {
                    AssemblyForWhichCurrentThreadIsComputingKeys = this;
                    Interlocked.CompareExchange(ref _lazyStrongNameKeys, ComputeStrongNameKeys(), null);
                } finally {
                    AssemblyForWhichCurrentThreadIsComputingKeys = null;
                }
            }

            return _lazyStrongNameKeys;
        }
    }

    internal AssemblyFlags assemblyFlags {
        get {
            var defaultValue = default(AssemblyFlags);
            var fieldValue = defaultValue;

            var data = GetSourceDecodedWellKnownAttributeData();

            if (data is not null)
                fieldValue = data.assemblyFlagsAttributeSetting;

            data = GetNetModuleDecodedWellKnownAttributeData();

            if (data is not null)
                fieldValue |= data.assemblyFlagsAttributeSetting;

            return fieldValue;
        }
    }

    internal string signatureKey
        => GetWellKnownAttributeDataStringField(data => data.assemblySignatureKeyAttributeSetting,
            missingValue: null, QuickAttributes.AssemblySignatureKey);

    private string _assemblyCultureAttributeSetting
        => GetWellKnownAttributeDataStringField(data => data.assemblyCultureAttributeSetting);

    private string _assemblyKeyFileAttributeSetting
        => GetWellKnownAttributeDataStringField(data => data.assemblyKeyFileAttributeSetting,
            WellKnownAttributeData.StringMissingValue, QuickAttributes.AssemblyKeyFile);

    private string _assemblyKeyContainerAttributeSetting
        => GetWellKnownAttributeDataStringField(data => data.assemblyKeyContainerAttributeSetting,
            WellKnownAttributeData.StringMissingValue, QuickAttributes.AssemblyKeyName);


    internal override bool HasComplete(CompletionParts part) {
        return _state.HasComplete(part);
    }

    internal override void ForceComplete(TextLocation location) {
        while (true) {
            var incompletePart = _state.nextIncompletePart;

            switch (incompletePart) {
                case CompletionParts.Attributes:
                    EnsureAttributesAreBound();
                    break;
                case CompletionParts.StartAttributeChecks:
                case CompletionParts.FinishAttributeChecks:
                    if (_state.NotePartComplete(CompletionParts.StartAttributeChecks)) {
                        var diagnostics = BelteDiagnosticQueue.GetInstance();
                        ValidateAttributeSemantics(diagnostics);
                        AddDeclarationDiagnostics(diagnostics);
                        _state.NotePartComplete(CompletionParts.FinishAttributeChecks);
                        diagnostics.Free();
                    }
                    break;
                case CompletionParts.Module:
                    globalNamespace.ForceComplete(location);

                    if (globalNamespace.HasComplete(CompletionParts.MembersCompleted)) {
                        _state.NotePartComplete(CompletionParts.Module);
                        break;
                    } else {
                        return;
                    }
                case CompletionParts.StartValidatingAddedModules:
                case CompletionParts.FinishValidatingAddedModules:
                    if (_state.NotePartComplete(CompletionParts.StartValidatingAddedModules)) {
                        ReportDiagnosticsForAddedModules();
                        _state.NotePartComplete(CompletionParts.FinishValidatingAddedModules);
                    }

                    break;
                case CompletionParts.None:
                    return;
                default:
                    _state.NotePartComplete(CompletionParts.All & ~CompletionParts.AssemblySymbolAll);
                    break;
            }

            _state.SpinWaitComplete(incompletePart);
        }
    }

    private void ReportDiagnosticsForAddedModules() {
        // TODO
    }

    private void ValidateAttributeSemantics(BelteDiagnosticQueue diagnostics) {
        // TODO
    }

    private AssemblyIdentity ComputeIdentity() {
        return new AssemblyIdentity(
            _assemblySimpleName,
            VersionHelper.GenerateVersionFromPatternAndCurrentTime(default, _assemblyVersionAttributeSetting),
            _assemblyCultureAttributeSetting,
            strongNameKeys.publicKey,
            hasPublicKey: !strongNameKeys.publicKey.IsDefault,
            isRetargetable: (assemblyFlags & AssemblyFlags.Retargetable) == AssemblyFlags.Retargetable
        );
    }

    internal CommonAssemblyWellKnownAttributeData GetSourceDecodedWellKnownAttributeData() {
        var attributesBag = _lazySourceAttributesBag;

        if (attributesBag is null || !attributesBag.isDecodedWellKnownAttributeDataComputed)
            attributesBag = GetSourceAttributesBag();

        return (CommonAssemblyWellKnownAttributeData)attributesBag.decodedWellKnownAttributeData;
    }

    private CommonAssemblyWellKnownAttributeData GetSourceDecodedWellKnownAttributeData(QuickAttributes attribute) {
        var attributesBag = _lazySourceAttributesBag;

        if (attributesBag?.isDecodedWellKnownAttributeDataComputed == true)
            return (CommonAssemblyWellKnownAttributeData)attributesBag.decodedWellKnownAttributeData;

        attributesBag = null;
        Func<AttributeSyntax, bool> attributeMatches = attribute switch {
            QuickAttributes.AssemblySignatureKey => IsPossibleAssemblySignatureKeyAttribute,
            QuickAttributes.AssemblyKeyName => IsPossibleAssemblyKeyNameAttribute,
            QuickAttributes.AssemblyKeyFile => IsPossibleAssemblyKeyFileAttribute,
            _ => throw ExceptionUtilities.UnexpectedValue(attribute)
        };

        LoadAndValidateAttributes(
            OneOrMany.Create(GetAttributeDeclarations()),
            ref attributesBag,
            attributeMatchesOpt: attributeMatches
        );

        return (CommonAssemblyWellKnownAttributeData)attributesBag?.decodedWellKnownAttributeData;

        bool IsPossibleAssemblySignatureKeyAttribute(AttributeSyntax node) {
            var checker = declaringCompilation.GetBinderFactory(node.syntaxTree).GetBinder(node).quickAttributeChecker;
            return checker.IsPossibleMatch(node, QuickAttributes.AssemblySignatureKey);
        }

        bool IsPossibleAssemblyKeyNameAttribute(AttributeSyntax node) {
            var checker = declaringCompilation.GetBinderFactory(node.syntaxTree).GetBinder(node).quickAttributeChecker;
            return checker.IsPossibleMatch(node, QuickAttributes.AssemblyKeyName);
        }

        bool IsPossibleAssemblyKeyFileAttribute(AttributeSyntax node) {
            var checker = declaringCompilation.GetBinderFactory(node.syntaxTree).GetBinder(node).quickAttributeChecker;
            return checker.IsPossibleMatch(node, QuickAttributes.AssemblyKeyFile);
        }
    }

    internal ImmutableArray<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        var builder = ArrayBuilder<SyntaxList<AttributeListSyntax>>.GetInstance();
        var declarations = declaringCompilation.mergedRootDeclaration.declarations;

        foreach (var rootNs in declarations.Cast<RootSingleNamespaceDeclaration>()) {
            if (rootNs.hasAssemblyAttributes) {
                var tree = rootNs.location.tree;
                var root = (CompilationUnitSyntax)tree.GetRoot();
                builder.Add(root.attributeLists);
            }
        }

        return builder.ToImmutableAndFree();
    }

    private string GetWellKnownAttributeDataStringField(
        Func<CommonAssemblyWellKnownAttributeData, string> fieldGetter,
        string missingValue = null,
        QuickAttributes? attributeMatchesOpt = null) {
        var fieldValue = missingValue;

        var data = attributeMatchesOpt is null
            ? GetSourceDecodedWellKnownAttributeData()
            : GetSourceDecodedWellKnownAttributeData(attributeMatchesOpt.Value);

        if (data != null)
            fieldValue = fieldGetter(data);

        if (fieldValue == (object?)missingValue) {
            data = (attributeMatchesOpt is null || _lazyNetModuleAttributesBag is not null)
                ? GetNetModuleDecodedWellKnownAttributeData()
                : GetLimitedNetModuleDecodedWellKnownAttributeData(attributeMatchesOpt.Value);

            if (data != null)
                fieldValue = fieldGetter(data);
        }

        return fieldValue;
    }

    private CustomAttributesBag<AttributeData> GetSourceAttributesBag() {
        EnsureAttributesAreBound();
        return _lazySourceAttributesBag;
    }

    private void EnsureAttributesAreBound() {
        if ((_lazySourceAttributesBag is null || !_lazySourceAttributesBag.isSealed) &&
            LoadAndValidateAttributes(OneOrMany.Create(GetAttributeDeclarations()), ref _lazySourceAttributesBag)) {
            _state.NotePartComplete(CompletionParts.Attributes);
        }
    }

    internal CommonAssemblyWellKnownAttributeData GetNetModuleDecodedWellKnownAttributeData() {
        var attributesBag = GetNetModuleAttributesBag();
        return (CommonAssemblyWellKnownAttributeData)attributesBag.decodedWellKnownAttributeData;
    }

    private CustomAttributesBag<AttributeData> GetNetModuleAttributesBag() {
        if (_lazyNetModuleAttributesBag is null)
            LoadAndValidateNetModuleAttributes(ref _lazyNetModuleAttributesBag);

        return _lazyNetModuleAttributesBag;
    }

    private void LoadAndValidateNetModuleAttributes(ref CustomAttributesBag<AttributeData> lazyNetModuleAttributesBag) {
        var diagnostics = BelteDiagnosticQueue.GetInstance();

        var attributesFromNetModules = GetNetModuleAttributes(out var netModuleNames);

        WellKnownAttributeData wellKnownData = null;

        if (attributesFromNetModules.Any()) {
            wellKnownData = ValidateAttributeUsageAndDecodeWellKnownAttributes(
                attributesFromNetModules,
                netModuleNames,
                diagnostics
            );
        } else {
            var unused = GetUniqueSourceAssemblyAttributes();
        }

        HashSet<NamedTypeSymbol> forwardedTypes = null;

        // TODO
        // for (int i = _modules.Length - 1; i > 0; i--) {
        //     var peModuleSymbol = (PEModuleSymbol)_modules[i];

        //     foreach (NamedTypeSymbol forwarded in peModuleSymbol.GetForwardedTypes()) {
        //         if (forwardedTypes == null) {
        //             if (wellKnownData == null) {
        //                 wellKnownData = new CommonAssemblyWellKnownAttributeData();
        //             }

        //             forwardedTypes = ((CommonAssemblyWellKnownAttributeData)wellKnownData).ForwardedTypes;
        //             if (forwardedTypes == null) {
        //                 forwardedTypes = new HashSet<NamedTypeSymbol>();
        //                 ((CommonAssemblyWellKnownAttributeData)wellKnownData).ForwardedTypes = forwardedTypes;
        //             }
        //         }

        //         if (forwardedTypes.Add(forwarded)) {
        //             if (forwarded.IsErrorType()) {
        //                 if (!diagnostics.ReportUseSite(forwarded, NoLocation.Singleton)) {
        //                     DiagnosticInfo info = ((ErrorTypeSymbol)forwarded).ErrorInfo;

        //                     if ((object)info != null) {
        //                         diagnostics.Add(info, NoLocation.Singleton);
        //                     }
        //                 }
        //             }
        //         }
        //     }
        // }

        CustomAttributesBag<AttributeData> netModuleAttributesBag;

        if (wellKnownData != null || attributesFromNetModules.Any()) {
            netModuleAttributesBag = new CustomAttributesBag<AttributeData>();

            // netModuleAttributesBag.SetEarlyDecodedWellKnownAttributeData(null);
            netModuleAttributesBag.SetDecodedWellKnownAttributeData(wellKnownData);
            netModuleAttributesBag.SetAttributes(attributesFromNetModules);

            if (netModuleAttributesBag.isEmpty)
                netModuleAttributesBag = CustomAttributesBag<AttributeData>.Empty;
        } else {
            netModuleAttributesBag = CustomAttributesBag<AttributeData>.Empty;
        }

        if (Interlocked.CompareExchange(ref lazyNetModuleAttributesBag, netModuleAttributesBag, null) is null)
            AddDeclarationDiagnostics(diagnostics);

        diagnostics.Free();
    }

    private HashSet<AttributeData> GetUniqueSourceAssemblyAttributes() {
        var appliedSourceAttributes = GetSourceAttributesBag().attributes;

        HashSet<AttributeData> uniqueAttributes = null;

        for (var i = 0; i < appliedSourceAttributes.Length; i++) {
            var attribute = appliedSourceAttributes[i];

            if (!attribute.hasErrors) {
                if (!AddUniqueAssemblyAttribute(attribute, ref uniqueAttributes))
                    AddOmittedAttributeIndex(i);
            }
        }

        return uniqueAttributes;
    }

    private static bool AddUniqueAssemblyAttribute(
        AttributeData attribute,
        ref HashSet<AttributeData> uniqueAttributes) {
        uniqueAttributes ??= new HashSet<AttributeData>(CommonAttributeDataComparer.Instance);
        return uniqueAttributes.Add(attribute);
    }

    private ImmutableArray<AttributeData> GetNetModuleAttributes(out ImmutableArray<string> netModuleNames) {
        ArrayBuilder<AttributeData> moduleAssemblyAttributesBuilder = null;
        ArrayBuilder<string> netModuleNameBuilder = null;

        // TODO
        // for (var i = 1; i < _modules.Length; i++) {
        //     var peModuleSymbol = (Metadata.PE.PEModuleSymbol)_modules[i];
        //     string netModuleName = peModuleSymbol.Name;
        //     foreach (var attributeData in peModuleSymbol.GetAssemblyAttributes()) {
        //         if (netModuleNameBuilder == null) {
        //             netModuleNameBuilder = ArrayBuilder<string>.GetInstance();
        //             moduleAssemblyAttributesBuilder = ArrayBuilder<CSharpAttributeData>.GetInstance();
        //         }

        //         netModuleNameBuilder.Add(netModuleName);
        //         moduleAssemblyAttributesBuilder.Add(attributeData);
        //     }
        // }

        if (netModuleNameBuilder is null) {
            netModuleNames = [];
            return [];
        }

        netModuleNames = netModuleNameBuilder.ToImmutableAndFree();
        return moduleAssemblyAttributesBuilder.ToImmutableAndFree();
    }

    private StrongNameKeys ComputeStrongNameKeys() {
        var keyFile = declaringCompilation.options.cryptoKeyFile;

        if (declaringCompilation.options.publicSign) {
            if (!string.IsNullOrEmpty(keyFile) && !PathUtilities.IsAbsolute(keyFile))
                return StrongNameKeys.None;

            return StrongNameKeys.Create(keyFile);
        }

        if (string.IsNullOrEmpty(keyFile)) {
            keyFile = _assemblyKeyFileAttributeSetting;

            if ((object)keyFile == (object)WellKnownAttributeData.StringMissingValue)
                keyFile = null;
        }

        var keyContainer = declaringCompilation.options.cryptoKeyContainer;

        if (string.IsNullOrEmpty(keyContainer)) {
            keyContainer = _assemblyKeyContainerAttributeSetting;

            if ((object)keyContainer == (object)WellKnownAttributeData.StringMissingValue)
                keyContainer = null;
        }

        var hasCounterSignature = !string.IsNullOrEmpty(signatureKey);
        return StrongNameKeys.Create(
            declaringCompilation.options.strongNameProvider,
            keyFile,
            keyContainer,
            hasCounterSignature
        );
    }

    private CommonAssemblyWellKnownAttributeData GetLimitedNetModuleDecodedWellKnownAttributeData(
        QuickAttributes attributeMatches) {
        var attributesFromNetModules = GetNetModuleAttributes(out var netModuleNames);

        WellKnownAttributeData wellKnownData = null;

        if (attributesFromNetModules.Any()) {
            wellKnownData = LimitedDecodeWellKnownAttributes(
                attributesFromNetModules,
                netModuleNames,
                attributeMatches
            );
        }

        return (CommonAssemblyWellKnownAttributeData)wellKnownData;

        WellKnownAttributeData LimitedDecodeWellKnownAttributes(
            ImmutableArray<AttributeData> attributesFromNetModules,
            ImmutableArray<string> netModuleNames,
            QuickAttributes attributeMatches) {
            var netModuleAttributesCount = attributesFromNetModules.Length;

            HashSet<AttributeData> uniqueAttributes = null;
            CommonAssemblyWellKnownAttributeData result = null;

            for (var i = netModuleAttributesCount - 1; i >= 0; i--) {
                var attribute = attributesFromNetModules[i];

                if (!attribute.hasErrors && ValidateAttributeUsageForNetModuleAttribute(
                    attribute,
                    netModuleNames[i],
                    BelteDiagnosticQueue.Discarded,
                    ref uniqueAttributes
                )) {
                    LimitedDecodeWellKnownAttribute(attribute, attributeMatches, ref result);
                }
            }

            return result;
        }

        void LimitedDecodeWellKnownAttribute(
            AttributeData attribute,
            QuickAttributes attributeMatches,
            ref CommonAssemblyWellKnownAttributeData result) {
            // TODO
            // if (attributeMatches is QuickAttributes.AssemblySignatureKey &&
            //     attribute.IsTargetAttribute(AttributeDescription.AssemblySignatureKeyAttribute)) {
            //     result ??= new CommonAssemblyWellKnownAttributeData();
            //     result.AssemblySignatureKeyAttributeSetting = (string)attribute.CommonConstructorArguments[0].ValueInternal;
            // } else if (attributeMatches is QuickAttributes.AssemblyKeyFile &&
            //       attribute.IsTargetAttribute(AttributeDescription.AssemblyKeyFileAttribute)) {
            //     result ??= new CommonAssemblyWellKnownAttributeData();
            //     result.AssemblyKeyFileAttributeSetting = (string)attribute.CommonConstructorArguments[0].ValueInternal;
            // } else if (attributeMatches is QuickAttributes.AssemblyKeyName &&
            //       attribute.IsTargetAttribute(AttributeDescription.AssemblyKeyNameAttribute)) {
            //     result ??= new CommonAssemblyWellKnownAttributeData();
            //     result.AssemblyKeyContainerAttributeSetting = (string)attribute.CommonConstructorArguments[0].ValueInternal;
            // }
        }
    }

    private bool ValidateAttributeUsageForNetModuleAttribute(
        AttributeData attribute,
        string netModuleName,
        BelteDiagnosticQueue diagnostics,
        ref HashSet<AttributeData> uniqueAttributes) {
        return true;
        // TODO
        // var attributeClass = attribute.attributeClass;

        // if (attributeClass.GetAttributeUsageInfo().AllowMultiple) {
        //     // Duplicate attributes are allowed, but native compiler doesn't emit duplicate attributes, i.e. attributes with same constructor and arguments.
        //     return AddUniqueAssemblyAttribute(attribute, ref uniqueAttributes);
        // } else {
        //     // Duplicate attributes with same attribute type are not allowed.
        //     // Check if there is an existing assembly attribute with same attribute type.
        //     if (uniqueAttributes == null || !uniqueAttributes.Contains((a) => TypeSymbol.Equals(a.AttributeClass, attributeClass, TypeCompareKind.ConsiderEverything2))) {
        //         // Attribute with unique attribute type, not a duplicate.
        //         bool success = AddUniqueAssemblyAttribute(attribute, ref uniqueAttributes);
        //         Debug.Assert(success);
        //         return true;
        //     } else {
        //         // Duplicate attribute with same attribute type, we should report an error.

        //         // Native compiler suppresses the error for
        //         // (a) Duplicate well-known assembly attributes and
        //         // (b) Identical duplicates, i.e. attributes with same constructor and arguments.

        //         // For (a), native compiler picks the last of these duplicate well-known netmodule attributes, but these can vary based on the ordering of referenced netmodules.

        //         if (IsKnownAssemblyAttribute(attribute)) {
        //             if (!uniqueAttributes.Contains(attribute)) {
        //                 // This attribute application will be ignored.
        //                 diagnostics.Add(ErrorCode.WRN_AssemblyAttributeFromModuleIsOverridden, NoLocation.Singleton, attribute.AttributeClass, netModuleName);
        //             }
        //         } else if (AddUniqueAssemblyAttribute(attribute, ref uniqueAttributes)) {
        //             // Error
        //             diagnostics.Add(ErrorCode.ERR_DuplicateAttributeInNetModule, NoLocation.Singleton, attribute.AttributeClass.Name, netModuleName);
        //         }

        //         return false;
        //     }
        // }
    }

    private WellKnownAttributeData ValidateAttributeUsageAndDecodeWellKnownAttributes(
        ImmutableArray<AttributeData> attributesFromNetModules,
        ImmutableArray<string> netModuleNames,
        BelteDiagnosticQueue diagnostics) {
        var netModuleAttributesCount = attributesFromNetModules.Length;
        var sourceAttributesCount = GetSourceAttributesBag().attributes.Length;

        var uniqueAttributes = GetUniqueSourceAssemblyAttributes();

        var arguments = new DecodeWellKnownAttributeArguments<AttributeSyntax, AttributeData, AttributeLocation> {
            attributesCount = netModuleAttributesCount,
            diagnostics = diagnostics,
            symbolPart = AttributeLocation.None
        };

        for (var i = netModuleAttributesCount - 1; i >= 0; i--) {
            var totalIndex = i + sourceAttributesCount;
            var attribute = attributesFromNetModules[i];

            // diagnostics.Add(attribute.ErrorInfo, NoLocation.Singleton);

            if (!attribute.hasErrors && ValidateAttributeUsageForNetModuleAttribute(
                attribute,
                netModuleNames[i],
                diagnostics,
                ref uniqueAttributes
            )) {
                arguments.attribute = attribute;
                arguments.index = i;
                arguments.attributeSyntax = null;

                DecodeWellKnownAttribute(ref arguments, totalIndex, isFromNetModule: true);
            } else {
                AddOmittedAttributeIndex(totalIndex);
            }
        }

        return arguments.hasDecodedData ? arguments.decodedData : null;
    }

    private void DecodeWellKnownAttribute(
        ref DecodeWellKnownAttributeArguments<AttributeSyntax, AttributeData, AttributeLocation> arguments,
        int index,
        bool isFromNetModule) {
        // TODO
    }

    private void AddOmittedAttributeIndex(int index) {
        if (_lazyOmittedAttributeIndices is null)
            Interlocked.CompareExchange(ref _lazyOmittedAttributeIndices, new ConcurrentSet<int>(), null);

        _lazyOmittedAttributeIndices.Add(index);
    }
}
