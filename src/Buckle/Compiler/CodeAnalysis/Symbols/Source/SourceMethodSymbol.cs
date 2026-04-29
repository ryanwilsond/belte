using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceMethodSymbol : MethodSymbol, IAttributeTargetSymbol {
    private CustomAttributesBag<AttributeData> _lazyAttributesBag;
    private CustomAttributesBag<AttributeData> _lazyReturnTypeAttributesBag;

    private protected SourceMethodSymbol(SyntaxReference syntaxReference) {
        this.syntaxReference = syntaxReference;
    }

    internal sealed override bool hidesBaseMethodsByName => false;

    internal override SyntaxReference syntaxReference { get; }

    internal override bool hasSpecialName => methodKind switch {
        MethodKind.Constructor => true,
        MethodKind.StaticConstructor => true,
        MethodKind.Operator => true,
        MethodKind.Conversion => true,
        _ => false,
    };

    internal virtual Binder outerBinder => null;

    internal virtual Binder withTemplateParametersBinder => null;

    internal BelteSyntaxNode syntaxNode => (BelteSyntaxNode)syntaxReference.node;

    internal SyntaxTree syntaxTree => syntaxReference.syntaxTree;

    internal abstract ImmutableArray<ImmutableArray<TypeWithAnnotations>> GetTypeParameterConstraintTypes();

    internal abstract ImmutableArray<TypeParameterConstraintKinds> GetTypeParameterConstraintKinds();

    // internal sealed override bool hasUnscopedRefAttribute => GetDecodedWellKnownAttributeData()?.hasUnscopedRefAttribute == true;
    internal sealed override bool hasUnscopedRefAttribute => false;

    private protected virtual AttributeLocation _attributeLocationForLoadAndValidateAttributes
        => AttributeLocation.None;

    internal override ImmutableArray<AttributeData> GetAttributes() {
        return GetAttributesBag().attributes;
    }

    internal override ImmutableArray<AttributeData> GetReturnTypeAttributes() {
        return GetReturnTypeAttributesBag().attributes;
    }

    private CustomAttributesBag<AttributeData> GetReturnTypeAttributesBag() {
        var bag = _lazyReturnTypeAttributesBag;

        if (bag is not null && bag.isSealed)
            return bag;

        return GetAttributesBag(ref _lazyReturnTypeAttributesBag, forReturnType: true);
    }

    private protected virtual IAttributeTargetSymbol _attributeOwner => this;

    IAttributeTargetSymbol IAttributeTargetSymbol.attributesOwner => _attributeOwner;

    AttributeLocation IAttributeTargetSymbol.defaultAttributeLocation => AttributeLocation.Method;

    AttributeLocation IAttributeTargetSymbol.allowedAttributeLocations {
        get {
            switch (methodKind) {
                case MethodKind.Constructor:
                case MethodKind.StaticConstructor:
                    return AttributeLocation.Method;
                default:
                    return AttributeLocation.Method | AttributeLocation.Return;
            }
        }
    }

    internal static void ReportErrorIfHasConstraints(
        TemplateConstraintClauseListSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        if (syntax is not null && syntax.constraintClauses.Count > 0) {
            // TODO Do we even want an error here?
            // I can't imagine a situation where you could add an error-free constraint clause without having templates
            // However this would speed up compilation slightly as you wouldn't need to actually bind the constraints
            // Just push this error instead
            // EDIT: It *would* be legal to do something like `where { 3 == 3; }` and that would require no templates
        }
    }

    private CustomAttributesBag<AttributeData> GetAttributesBag() {
        var bag = _lazyAttributesBag;

        if (bag is not null && bag.isSealed)
            return bag;

        return GetAttributesBag(ref _lazyAttributesBag, forReturnType: false);
    }

    internal virtual OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations() {
        return OneOrMany.Create(default(SyntaxList<AttributeListSyntax>));
    }

    private CustomAttributesBag<AttributeData> GetAttributesBag(
        ref CustomAttributesBag<AttributeData> lazyAttributesBag,
        bool forReturnType) {
        var (declarations, symbolPart) = forReturnType
            ? (GetReturnTypeAttributeDeclarations(), AttributeLocation.Return)
            : (GetAttributeDeclarations(), _attributeLocationForLoadAndValidateAttributes);

        if (LoadAndValidateAttributes(
            declarations,
            ref lazyAttributesBag,
            symbolPart,
            binderOpt: outerBinder
        )) {
            NoteAttributesComplete(forReturnType);
        }

        return lazyAttributesBag;
    }

    internal virtual OneOrMany<SyntaxList<AttributeListSyntax>> GetReturnTypeAttributeDeclarations() {
        return GetAttributeDeclarations();
    }

    private protected abstract void NoteAttributesComplete(bool forReturnType);

    private protected BelteSyntaxNode GetInMethodSyntaxNode() {
        return syntaxNode switch {
            ConstructorDeclarationSyntax constructor
                => constructor.constructorInitializer ?? (BelteSyntaxNode)constructor.body,
            BaseMethodDeclarationSyntax method => method.body,
            CompilationUnitSyntax _ when this is SynthesizedEntryPoint entryPoint
                => (BelteSyntaxNode)entryPoint.returnTypeSyntax,
            LocalFunctionStatementSyntax localFunction => localFunction.body,
            ClassDeclarationSyntax classDeclaration => classDeclaration,
            _ => null,
        };
    }

    internal override DllImportData? GetDllImportData() {
        var data = GetDecodedWellKnownAttributeData();
        return data?.dllImportPlatformInvokeData;
    }

    internal sealed override UnmanagedCallersOnlyAttributeData GetUnmanagedCallersOnlyAttributeData(bool forceComplete) {
        if (syntaxReference is null)
            return null;

        if (forceComplete)
            _ = GetAttributes();

        var lazyAttributesBag = _lazyAttributesBag;

        // TODO What is the purpose of this second check
        if (lazyAttributesBag is null/* || !lazyAttributesBag.isEarlyDecodedWellKnownAttributeDataComputed*/)
            return UnmanagedCallersOnlyAttributeData.Uninitialized;

        if (lazyAttributesBag.isDecodedWellKnownAttributeDataComputed) {
            var lateData = (MethodWellKnownAttributeData)lazyAttributesBag.decodedWellKnownAttributeData;
            return lateData?.unmanagedCallersOnlyAttributeData;
        }

        return null;
    }

    private protected override void DecodeWellKnownAttributeImpl(
        ref DecodeWellKnownAttributeArguments<AttributeSyntax, AttributeData, AttributeLocation> arguments) {
        if (arguments.symbolPart == AttributeLocation.None) {
            DecodeWellKnownAttributeAppliedToMethod(ref arguments);
        } else {
            // DecodeWellKnownAttributeAppliedToReturnValue(ref arguments);
            throw ExceptionUtilities.Unreachable();
        }
    }

    private protected MethodWellKnownAttributeData GetDecodedWellKnownAttributeData() {
        var attributesBag = _lazyAttributesBag;

        if (attributesBag is null || !attributesBag.isDecodedWellKnownAttributeDataComputed)
            attributesBag = GetAttributesBag();

        return (MethodWellKnownAttributeData)attributesBag.decodedWellKnownAttributeData;
    }

    private void DecodeWellKnownAttributeAppliedToMethod(
        ref DecodeWellKnownAttributeArguments<AttributeSyntax, AttributeData, AttributeLocation> arguments) {
        var attribute = arguments.attribute;

        if (attribute.IsTargetAttribute(AttributeDescription.DllImportAttribute))
            DecodeDllImportAttribute(ref arguments);
        else if (attribute.IsTargetAttribute(AttributeDescription.UnmanagedAttribute))
            DecodeUnmanagedAttribute(ref arguments);
    }

    private void DecodeUnmanagedAttribute(
        ref DecodeWellKnownAttributeArguments<AttributeSyntax, AttributeData, AttributeLocation> arguments) {
        var diagnostics = arguments.diagnostics;

        arguments.GetOrCreateData<MethodWellKnownAttributeData>().unmanagedCallersOnlyAttributeData =
            DecodeUnmanagedAttributeData(
                this,
                arguments.attribute,
                arguments.attributeSyntax.location,
                diagnostics
            );

        var reportedError = CheckAndReportValidUnmanagedCallersOnlyTarget(arguments.attributeSyntax.name, diagnostics);
        var returnTypeSyntax = ExtractReturnTypeSyntax();

        if (ReferenceEquals(returnTypeSyntax, SyntaxTree.Dummy.GetRoot()))
            return;

        CheckAndReportManagedTypes(returnType, refKind, returnTypeSyntax, isParam: false, diagnostics);

        foreach (var param in parameters) {
            CheckAndReportManagedTypes(
                param.type,
                param.refKind,
                param.GetNonNullSyntaxNode(),
                isParam: true,
                diagnostics
            );
        }

        static void CheckAndReportManagedTypes(
            TypeSymbol type,
            RefKind refKind,
            SyntaxNode syntax,
            bool isParam,
            BelteDiagnosticQueue diagnostics) {
            if (refKind != RefKind.None) {
                // TODO error
                // diagnostics.Add(ErrorCode.ERR_CannotUseRefInUnmanagedCallersOnly, syntax.location);
            }

            // TODO error
            // switch (type.managedKindNoUseSiteDiagnostics) {
            //     case ManagedKind.Unmanaged:
            //     case ManagedKind.UnmanagedWithGenerics:
            //         // Note that this will let through some things that are technically unmanaged, but not
            //         // actually blittable. However, we don't have a formal concept of blittable in C#
            //         // itself, so checking for purely unmanaged types is the best we can do here.
            //         return;

            //     case ManagedKind.Managed:
            //         // Cannot use '{0}' as a {1} type on a method attributed with 'UnmanagedCallersOnly.
            //         diagnostics.Add(ErrorCode.ERR_CannotUseManagedTypeInUnmanagedCallersOnly, syntax.Location, type, (isParam ? MessageID.IDS_Parameter : MessageID.IDS_Return).Localize());
            //         return;

            //     default:
            //         throw ExceptionUtilities.UnexpectedValue(type.ManagedKindNoUseSiteDiagnostics);
            // }
        }

        static UnmanagedCallersOnlyAttributeData DecodeUnmanagedAttributeData(
            SourceMethodSymbol @this,
            AttributeData attribute,
            TextLocation location,
            BelteDiagnosticQueue diagnostics) {
            ImmutableHashSet<CallingConvention> callingConventionTypes = null;

            if (attribute._commonNamedArguments is { IsDefaultOrEmpty: false } namedArgs) {
                var callingConvention = CallingConvention.Default;

                foreach (var namedArg in attribute._commonNamedArguments) {
                    switch (namedArg.Key) {
                        case "CallingConvention":
                            callingConvention = namedArg.Value.value switch {
                                1 => CallingConvention.Winapi,
                                2 => CallingConvention.Cdecl,
                                _ => CallingConvention.Winapi,
                            };

                            break;
                    }
                }

                callingConventionTypes = [callingConvention];

                // TODO .NET UnmanagedCallersOnly args
                // var systemType = CorLibrary.GetSpecialType(SpecialType.Type);

                // foreach (var (key, value) in attribute._commonNamedArguments) {
                //     bool isField = attribute.attributeClass.GetMembers(key).Any(
                //         static (m, systemType) => m is FieldSymbol { Type: ArrayTypeSymbol { ElementType: NamedTypeSymbol elementType } } && elementType.Equals(systemType, TypeCompareKind.ConsiderEverything),
                //         systemType);

                //     var namedArgumentDecoded = TryDecodeUnmanagedCallersOnlyCallConvsField(key, value, isField, location, diagnostics);

                //     if (namedArgumentDecoded.IsCallConvs) {
                //         callingConventionTypes = namedArgumentDecoded.CallConvs;
                //     }
                // }
            } else {
                callingConventionTypes = [CallingConvention.Cdecl];
            }

            return UnmanagedCallersOnlyAttributeData.Create(callingConventionTypes);
        }
    }

    internal SyntaxNode ExtractReturnTypeSyntax() {
        if (this is SynthesizedEntryPoint synthesized)
            return synthesized.returnTypeSyntax;

        var node = syntaxReference.node;

        if (node is MethodDeclarationSyntax methodDeclaration)
            return methodDeclaration.returnType;
        else if (node is LocalFunctionStatementSyntax statement)
            return statement.returnType;

        return SyntaxTree.Dummy.GetRoot();
    }

    internal bool CheckAndReportValidUnmanagedCallersOnlyTarget(SyntaxNode node, BelteDiagnosticQueue diagnostics) {
        if (!isStatic || isAbstract || isVirtual || methodKind is not (MethodKind.Ordinary or MethodKind.LocalFunction)) {
            diagnostics?.Push(Error.UnmanagedRequiresStatic(node.location));
            return true;
        }

        if (IsTemplateMethod(this) || containingType.isTemplateType) {
            diagnostics?.Push(Error.UnmanagedCannotBeTemplate(node.location));
            return true;
        }

        return false;

        static bool IsTemplateMethod(MethodSymbol method) {
            do {
                if (method.isTemplateMethod)
                    return true;

                method = method.containingSymbol as MethodSymbol;
            } while (method is not null);

            return false;
        }
    }

    private void DecodeDllImportAttribute(
        ref DecodeWellKnownAttributeArguments<AttributeSyntax, AttributeData, AttributeLocation> arguments) {
        var attribute = arguments.attribute;
        var diagnostics = (BelteDiagnosticQueue)arguments.diagnostics;
        var hasErrors = false;

        var implementationPart = this;

        if (!isExtern || !isStatic) {
            diagnostics.Push(Error.DllImportOnInvalidMethod(arguments.attributeSyntax.name.location));
            hasErrors = true;
        }

        var isAnyNestedMethodGeneric = false;

        for (MethodSymbol current = this; current is not null; current = current.containingSymbol as MethodSymbol) {
            if (current.isTemplateMethod) {
                isAnyNestedMethodGeneric = true;
                break;
            }
        }

        if (isAnyNestedMethodGeneric || containingType?.isTemplateType == true) {
            diagnostics.Push(Error.DllImportOnTemplateMethod(arguments.attributeSyntax.name.location));
            hasErrors = true;
        }

        var moduleName = attribute.GetConstructorArgument<string>(0, SpecialType.String);

        if (!MetadataHelpers.IsValidMetadataIdentifier(moduleName)) {
            diagnostics.Push(Error.InvalidAttributeArgument(
                attribute.GetAttributeArgumentLocation(0),
                arguments.attributeSyntax.name.ErrorDisplayName()
            ));

            hasErrors = true;
            moduleName = null;
        }

        var charSet = GetEffectiveDefaultMarshallingCharSet() ?? (CharSet)1;

        string importName = null;
        var preserveSig = true;
        var callingConvention = CallingConvention.Winapi;
        var setLastError = false;
        var exactSpelling = false;
        bool? bestFitMapping = null;
        bool? throwOnUnmappable = null;

        var position = 1;
        foreach (var namedArg in attribute._commonNamedArguments) {
            switch (namedArg.Key) {
                // TODO Implement as needed
                // case "EntryPoint":
                //     importName = namedArg.Value.ValueInternal as string;
                //     if (!MetadataHelpers.IsValidMetadataIdentifier(importName)) {
                //         // Dev10 reports CS0647: "Error emitting attribute ..."
                //         diagnostics.Add(ErrorCode.ERR_InvalidNamedArgument, arguments.AttributeSyntaxOpt.ArgumentList.Arguments[position].Location, namedArg.Key);
                //         hasErrors = true;
                //         importName = null;
                //     }

                //     break;

                // case "CharSet":
                //     // invalid values will be ignored
                //     charSet = namedArg.Value.DecodeValue<CharSet>(SpecialType.System_Enum);
                //     break;

                // case "SetLastError":
                //     // invalid values will be ignored
                //     setLastError = namedArg.Value.DecodeValue<bool>(SpecialType.System_Boolean);
                //     break;

                // case "ExactSpelling":
                //     // invalid values will be ignored
                //     exactSpelling = namedArg.Value.DecodeValue<bool>(SpecialType.System_Boolean);
                //     break;

                // case "PreserveSig":
                //     preserveSig = namedArg.Value.DecodeValue<bool>(SpecialType.System_Boolean);
                //     break;

                case "CallingConvention":
                    // invalid values will be ignored
                    callingConvention = namedArg.Value.value switch {
                        1 => CallingConvention.Winapi,
                        2 => CallingConvention.Cdecl,
                        _ => CallingConvention.Winapi,
                    };
                    // callingConvention = namedArg.Value.DecodeValue<CallingConvention>(SpecialType.UInt32);
                    break;

                    // case "BestFitMapping":
                    //     bestFitMapping = namedArg.Value.DecodeValue<bool>(SpecialType.System_Boolean);
                    //     break;

                    // case "ThrowOnUnmappableChar":
                    //     throwOnUnmappable = namedArg.Value.DecodeValue<bool>(SpecialType.System_Boolean);
                    //     break;
            }

            position++;
        }

        if (!hasErrors) {
            arguments.GetOrCreateData<MethodWellKnownAttributeData>().SetDllImport(
                arguments.index,
                moduleName,
                importName ?? name,
                DllImportData.MakeFlags(
                    exactSpelling,
                    charSet,
                    setLastError,
                    callingConvention,
                    bestFitMapping,
                    throwOnUnmappable),
                preserveSig
            );
        }
    }
}
