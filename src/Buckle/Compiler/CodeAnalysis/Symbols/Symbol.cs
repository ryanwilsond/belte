using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A base symbol.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
internal abstract class Symbol : ISymbol {
    /// <summary>
    /// Name of the symbol.
    /// </summary>
    public virtual string name => "";

    public virtual string metadataName => name;

    /// <summary>
    /// The accessibility/protection level of the symbol.
    /// </summary> <summary>
    internal abstract Accessibility declaredAccessibility { get; }

    internal abstract Symbol containingSymbol { get; }

    /// <summary>
    /// The type that contains this symbol, or null if nothing is containing this symbol.
    /// </summary>
    internal virtual NamedTypeSymbol containingType {
        get {
            var container = containingSymbol;
            var containerAsType = container as NamedTypeSymbol;

            if ((object)containerAsType == container)
                return containerAsType;

            return container.containingType;
        }
    }

    internal virtual bool requiresCompletion => false;

    internal virtual bool isImplicitlyDeclared => false;

    /// <summary>
    /// Gets the original definition of the symbol.
    /// </summary>
    internal Symbol originalDefinition => _originalSymbolDefinition;

    internal bool isDefinition => (object)this == originalDefinition;

    private protected virtual Symbol _originalSymbolDefinition => this;

    /// <summary>
    /// The type of symbol this is (see <see cref="SymbolKind" />).
    /// </summary>
    public abstract SymbolKind kind { get; }

    /// <summary>
    /// If the symbol is "static", i.e. declared with the static modifier.
    /// </summary>
    internal abstract bool isStatic { get; }

    /// <summary>
    /// If the symbol is "virtual", i.e. is defined but can be overridden
    /// </summary>
    internal abstract bool isVirtual { get; }

    /// <summary>
    /// If the symbol is "abstract", i.e. must be overridden or cannot be constructed directly.
    /// </summary>
    internal abstract bool isAbstract { get; }

    /// <summary>
    /// If the symbol is "override", i.e. overriding a virtual or abstract symbol.
    /// </summary>
    internal abstract bool isOverride { get; }

    /// <summary>
    /// If the symbol has an external implementation, i.e. declared with the extern modifier.
    /// </summary>
    internal abstract bool isExtern { get; }

    /// <summary>
    /// If the symbol is "sealed", i.e. cannot have child classes.
    /// </summary>
    internal abstract bool isSealed { get; }

    internal virtual Compilation declaringCompilation {
        get {
            if (!isDefinition)
                return originalDefinition.declaringCompilation;

            switch (kind) {
                case SymbolKind.ErrorType:
                case SymbolKind.Assembly:
                    return null;
            }

            switch (containingModule) {
                case SourceModuleSymbol sourceModuleSymbol:
                    return sourceModuleSymbol.declaringCompilation;
                case PEModuleSymbol:
                    return containingSymbol?.declaringCompilation;
            }

            return null;
        }
    }

    internal virtual AssemblySymbol containingAssembly => containingSymbol?.containingAssembly;

    internal virtual ModuleSymbol containingModule => containingSymbol?.containingModule;

    internal virtual NamespaceSymbol containingNamespace {
        get {
            for (var container = containingSymbol; container is not null; container = container.containingSymbol) {
                if (container is NamespaceSymbol ns)
                    return ns;
            }

            return null;
        }
    }

    internal virtual ImmutableArray<SyntaxReference> declaringSyntaxReferences => [syntaxReference];

    internal virtual ImmutableArray<TextLocation> locations => [location];

    internal abstract SyntaxReference syntaxReference { get; }

    internal abstract TextLocation location { get; }

    internal bool canBeReferencedByName {
        get {
            switch (kind) {
                case SymbolKind.Local:
                case SymbolKind.Label:
                case SymbolKind.Token:
                case SymbolKind.Alias:
                    return true;
                case SymbolKind.Namespace:
                case SymbolKind.Field:
                case SymbolKind.ErrorType:
                case SymbolKind.Parameter:
                case SymbolKind.TemplateParameter:
                case SymbolKind.NamedType:
                    break;
                case SymbolKind.Method:
                    var method = (MethodSymbol)this;

                    switch (method.methodKind) {
                        case MethodKind.Ordinary:
                        case MethodKind.LocalFunction:
                            break;
                        case MethodKind.Destructor:
                        case MethodKind.Finalizer:
                            return true;
                        default:
                            return false;
                    }

                    break;
                case SymbolKind.ArrayType:
                case SymbolKind.PointerType:
                case SymbolKind.FunctionPointerType:
                case SymbolKind.FunctionType:
                case SymbolKind.Assembly:
                    return false;
                default:
                    throw ExceptionUtilities.UnexpectedValue(kind);
            }

            return !string.IsNullOrEmpty(name);
        }
    }

    internal abstract void Accept(SymbolVisitor visitor);

    internal abstract TResult Accept<TArgument, TResult>(SymbolVisitor<TArgument, TResult> visitor, TArgument a);

    internal virtual void AddDeclarationDiagnostics(BelteDiagnosticQueue diagnostics) {
        if (diagnostics.Count > 0)
            declaringCompilation.declarationDiagnostics.Move(diagnostics);
    }

    internal virtual void ForceComplete(TextLocation location) { }

    internal virtual bool HasComplete(CompletionParts part) {
        return true;
    }

    internal bool IsImplementableInterfaceMember() {
        return !isSealed && (isAbstract || isVirtual) && (containingType?.isInterface ?? false);
    }

    internal bool MustCallMethodsDirectly() {
        switch (kind) {
            // TODO This gets more interesting with events and properties
            default:
                return false;
        }
    }

    internal ImmutableArray<Symbol> GetExplicitInterfaceImplementations() {
        switch (kind) {
            case SymbolKind.Method:
                return ((MethodSymbol)this).explicitInterfaceImplementations.Cast<MethodSymbol, Symbol>();
            default:
                return [];
        }
    }

    internal virtual void AfterAddingTypeMembersChecks(
        ConversionsBase conversions,
        BelteDiagnosticQueue diagnostics) { }

    internal virtual ImmutableArray<AttributeData> GetAttributes() {
        return [];
    }

    internal bool HasAttributeWithName(string namespaceName, string typeName) {
        return GetAttributes().Any(a => a.IsTargetAttribute(namespaceName, typeName));
    }

    internal TemplateParameterSymbol FindEnclosingTemplateParameter(string name) {
        var methodOrType = this;

        while (methodOrType is not null) {
            switch (methodOrType.kind) {
                case SymbolKind.Method:
                case SymbolKind.NamedType:
                case SymbolKind.ErrorType:
                case SymbolKind.Field:
                    break;
                default:
                    return null;
            }

            foreach (var templateParameter in methodOrType.GetMemberTemplateParameters()) {
                if (templateParameter.name == name)
                    return templateParameter;
            }

            methodOrType = methodOrType.containingSymbol;
        }

        return null;
    }

    internal bool IsExplicitInterfaceImplementation() {
        switch (kind) {
            case SymbolKind.Method:
                return ((MethodSymbol)this).isExplicitInterfaceImplementation;
            default:
                return false;
        }
    }

    internal NamespaceOrTypeSymbol ContainingNamespaceOrType() {
        if (containingSymbol is not null) {
            switch (containingSymbol.kind) {
                case SymbolKind.Namespace:
                case SymbolKind.NamedType:
                case SymbolKind.ErrorType:
                    return (NamespaceOrTypeSymbol)containingSymbol;
            }
        }

        return null;
    }

    internal CharSet? GetEffectiveDefaultMarshallingCharSet() {
        return containingModule.defaultMarshallingCharSet;
    }

    internal virtual LexicalSortKey GetLexicalSortKey() {
        if (syntaxReference is null)
            return LexicalSortKey.NotInSource;

        return new LexicalSortKey(syntaxReference, declaringCompilation);
    }

    internal int GetMemberArity() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).arity,
            SymbolKind.NamedType or SymbolKind.ErrorType => ((NamedTypeSymbol)this).arity,
            _ => 0,
        };
    }

    internal ImmutableArray<TemplateParameterSymbol> GetMemberTemplateParameters() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).templateParameters,
            SymbolKind.NamedType or SymbolKind.ErrorType => ((NamedTypeSymbol)this).templateParameters,
            SymbolKind.Field => [],
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal bool IsConstExpr() {
        return kind switch {
            SymbolKind.Field => ((FieldSymbol)this).isConstExpr,
            SymbolKind.Local => ((DataContainerSymbol)this).isConstExpr,
            _ => throw ExceptionUtilities.UnexpectedValue(kind)
        };
    }

    internal int GetParameterCount() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).parameterCount,
            SymbolKind.Field => 0,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal ImmutableArray<TypeWithAnnotations> GetParameterTypes() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).parameterTypesWithAnnotations,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal ImmutableArray<RefKind> GetParameterRefKinds() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).parameterRefKinds,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal ImmutableArray<bool> GetParameterConstnesses() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).parameterConstnesses,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal ImmutableArray<ParameterSymbol> GetParameters() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).parameters,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal TypeWithAnnotations GetTypeOrReturnType() {
        GetTypeOrReturnType(out _, out var returnType);
        return returnType;
    }

    internal bool IsOptional() {
        return kind switch {
            SymbolKind.Parameter => ((ParameterSymbol)this).isOptional,
            SymbolKind.TemplateParameter => ((TemplateParameterSymbol)this).isOptional,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }

    internal void GetTypeOrReturnType(
        out RefKind refKind,
        out TypeWithAnnotations returnType) {
        switch (kind) {
            case SymbolKind.Field:
                var field = (FieldSymbol)this;
                // TODO Why is this None?
                refKind = RefKind.None;
                returnType = field.typeWithAnnotations;
                break;
            case SymbolKind.Method:
                var method = (MethodSymbol)this;
                refKind = method.refKind;
                returnType = method.returnTypeWithAnnotations;
                break;
            case SymbolKind.Local:
                var local = (DataContainerSymbol)this;
                refKind = local.refKind;
                returnType = local.typeWithAnnotations;
                break;
            case SymbolKind.Parameter:
                var parameter = (ParameterSymbol)this;
                refKind = parameter.refKind;
                returnType = parameter.typeWithAnnotations;
                break;
            case SymbolKind.ErrorType:
                refKind = RefKind.None;
                returnType = new TypeWithAnnotations((TypeSymbol)this);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(kind);
        }
    }

    internal bool RequiresInstanceReceiver() {
        return kind switch {
            SymbolKind.Method => ((MethodSymbol)this).requiresInstanceReceiver,
            SymbolKind.Field => ((FieldSymbol)this).requiresInstanceReceiver,
            _ => throw ExceptionUtilities.UnexpectedValue(kind)
        };
    }

    internal RefKind GetRefKind() {
        return this switch {
            ParameterSymbol p => p.refKind,
            DataContainerSymbol d => d.refKind,
            FieldSymbol f => f.refKind,
            _ => throw ExceptionUtilities.UnexpectedValue(kind)
        };
    }

    internal bool IsLowLevel() {
        return this switch {
            FieldSymbol f => f.isLowLevel,
            NamedTypeSymbol n => n.isLowLevel,
            SourceMemberMethodSymbol s => s.isLowLevel,
            _ => false
        };
    }

    internal bool IsOperator() {
        return this is MethodSymbol m && m.IsOperator();
    }

    internal ParameterSymbol EnclosingThisSymbol() {
        var symbol = this;

        while (true) {
            switch (symbol.kind) {
                case SymbolKind.Method:
                    var method = (MethodSymbol)symbol;

                    if (method.methodKind == MethodKind.LocalFunction) {
                        symbol = method.containingSymbol;
                        continue;
                    }

                    return method.thisParameter;
                default:
                    return null;
            }
        }
    }

    internal static bool IsSymbolAccessible(
        Symbol symbol,
        AssemblySymbol within) {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(within);
        return AccessCheck.IsSymbolAccessible(symbol, within);
    }

    internal bool IsHiddenByCodeAnalysisEmbeddedAttribute() {
        var upperLevelType = kind == SymbolKind.NamedType ? (NamedTypeSymbol)this : containingType;

        if (upperLevelType is null)
            return false;

        while (upperLevelType.containingType is not null)
            upperLevelType = upperLevelType.containingType;

        // TODO attribute
        // return upperLevelType.hasCodeAnalysisEmbeddedAttribute;
        return false;
    }

    internal bool IsNoMoreVisibleThan(TypeSymbol type) {
        return type.StrippedType().IsAtLeastAsVisibleAs(this);
    }

    internal bool IsAsRestrictive(Symbol symbol2) {
        var acc1 = declaredAccessibility;

        if (acc1 == Accessibility.Public)
            return true;

        for (var s2 = symbol2; s2.kind != SymbolKind.Namespace; s2 = s2.containingSymbol) {
            var acc2 = s2.declaredAccessibility;

            switch (acc1) {
                case Accessibility.Internal: {
                        if ((acc2 is Accessibility.Private or Accessibility.Internal or Accessibility.InternalAndProtected) &&
                            s2.containingAssembly.HasInternalAccessTo(containingAssembly)) {
                            return true;
                        }

                        break;
                    }
                case Accessibility.InternalAndProtected:
                    if ((acc2 is Accessibility.Private or Accessibility.Internal or Accessibility.InternalAndProtected) &&
                        s2.containingAssembly.HasInternalAccessTo(containingAssembly)) {
                        goto case Accessibility.Protected;
                    }

                    break;
                case Accessibility.Protected: {
                        var parent1 = containingType;

                        if (parent1 is null) {
                        } else if (acc2 == Accessibility.Private) {
                            for (var parent2 = s2.containingType; parent2 is not null; parent2 = parent2.containingType) {
                                if (parent1.IsAccessibleViaInheritance(parent2))
                                    return true;
                            }
                        } else if (acc2 is Accessibility.Protected or Accessibility.InternalAndProtected) {
                            var parent2 = s2.containingType;

                            if (parent2 is not null && parent1.IsAccessibleViaInheritance(parent2))
                                return true;
                        }

                        break;
                    }
                case Accessibility.InternalOrProtected: {
                        var parent1 = containingType;

                        if (parent1 is null)
                            break;

                        switch (acc2) {
                            case Accessibility.Private:
                                if (s2.containingAssembly.HasInternalAccessTo(containingAssembly))
                                    return true;

                                for (var parent2 = s2.containingType; parent2 is not null; parent2 = parent2.containingType) {
                                    if (parent1.IsAccessibleViaInheritance(parent2))
                                        return true;
                                }

                                break;
                            case Accessibility.Internal:
                                if (s2.containingAssembly.HasInternalAccessTo(containingAssembly))
                                    return true;

                                break;
                            case Accessibility.Protected:
                                if (parent1.IsAccessibleViaInheritance(s2.containingType))
                                    return true;

                                break;
                            case Accessibility.InternalAndProtected:
                                if (s2.containingAssembly.HasInternalAccessTo(containingAssembly) ||
                                    parent1.IsAccessibleViaInheritance(s2.containingType)) {
                                    return true;
                                }

                                break;
                            case Accessibility.InternalOrProtected:
                                if (s2.containingAssembly.HasInternalAccessTo(containingAssembly) &&
                                    parent1.IsAccessibleViaInheritance(s2.containingType)) {
                                    return true;
                                }

                                break;
                        }

                        break;
                    }
                case Accessibility.Private:
                    if (acc2 == Accessibility.Private) {
                        var parent1 = containingType;

                        if (parent1 is null)
                            break;

                        var parent1OriginalDefinition = parent1.originalDefinition;

                        for (var parent2 = s2.containingType; parent2 is not null; parent2 = parent2.containingType) {
                            if (ReferenceEquals(parent2.originalDefinition, parent1OriginalDefinition))
                                return true;
                        }
                    }

                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(acc1);
            }
        }

        return false;
    }

    internal Symbol GetLeastOverriddenMember(NamedTypeSymbol accessingTypeOpt) {
        switch (kind) {
            case SymbolKind.Method:
                var method = (MethodSymbol)this;
                return method.GetConstructedLeastOverriddenMethod(accessingTypeOpt, requireSameReturnType: false);
            default:
                return this;
        }
    }

    internal bool LoadAndValidateAttributes(
        OneOrMany<SyntaxList<AttributeListSyntax>> attributesSyntaxLists,
        ref CustomAttributesBag<AttributeData> lazyAttributesBag,
        AttributeLocation symbolPart = AttributeLocation.None,
        bool earlyDecodingOnly = false,
        Binder binderOpt = null,
        Func<AttributeSyntax, bool> attributeMatchesOpt = null,
        Action<AttributeSyntax> beforeAttributePartBound = null,
        Action<AttributeSyntax> afterAttributePartBound = null) {
        var diagnostics = BelteDiagnosticQueue.GetInstance();
        var compilation = declaringCompilation;

        BoundAttribute[] boundAttributeArray;
        var attributesToBind = GetAttributesToBind(
            attributesSyntaxLists,
            symbolPart,
            diagnostics,
            compilation,
            attributeMatchesOpt,
            binderOpt,
            out var binders
        );

        var totalAttributesCount = attributesToBind.Length;

        ImmutableArray<AttributeData> boundAttributes;
        WellKnownAttributeData? wellKnownAttributeData;

        if (totalAttributesCount != 0) {
            if (lazyAttributesBag is null)
                Interlocked.CompareExchange(ref lazyAttributesBag, new CustomAttributesBag<AttributeData>(), null);

            var attributeTypesBuilder = new NamedTypeSymbol[totalAttributesCount];

            Binder.BindAttributeTypes(
                binders,
                attributesToBind,
                this,
                attributeTypesBuilder,
                beforeAttributePartBound,
                afterAttributePartBound,
                diagnostics
            );

            var interestedInDiagnostics = !earlyDecodingOnly && attributeMatchesOpt is null;
            var boundAttributeTypes = attributeTypesBuilder.AsImmutableOrNull();

            EarlyDecodeWellKnownAttributeTypes(boundAttributeTypes, attributesToBind);
            PostEarlyDecodeWellKnownAttributeTypes();

            var attributeDataArray = new AttributeData[totalAttributesCount];
            boundAttributeArray = interestedInDiagnostics ? new BoundAttribute[totalAttributesCount] : null;

            var earlyData = EarlyDecodeWellKnownAttributes(
                binders,
                boundAttributeTypes,
                attributesToBind,
                symbolPart,
                attributeDataArray,
                boundAttributeArray
            );

            lazyAttributesBag.SetEarlyDecodedWellKnownAttributeData(earlyData);

            if (earlyDecodingOnly) {
                diagnostics.Free();
                return false;
            }

            Binder.GetAttributes(
                binders,
                attributesToBind,
                boundAttributeTypes,
                attributeDataArray,
                boundAttributeArray,
                beforeAttributePartBound,
                afterAttributePartBound,
                diagnostics
            );

            boundAttributes = attributeDataArray.AsImmutableOrNull();

            wellKnownAttributeData = ValidateAttributeUsageAndDecodeWellKnownAttributes(
                binders,
                attributesToBind,
                boundAttributes,
                diagnostics,
                symbolPart
            );

            lazyAttributesBag.SetDecodedWellKnownAttributeData(wellKnownAttributeData);
        } else if (earlyDecodingOnly) {
            diagnostics.Free();
            return false;
        } else {
            boundAttributes = [];
            boundAttributeArray = null;
            wellKnownAttributeData = null;
            Interlocked.CompareExchange(ref lazyAttributesBag, CustomAttributesBag<AttributeData>.WithEmptyData(), null);
            PostEarlyDecodeWellKnownAttributeTypes();
        }

        var lazyAttributesStoredOnThisThread = false;

        if (lazyAttributesBag.SetAttributes(boundAttributes)) {
            if (attributeMatchesOpt is null) {
                PostDecodeWellKnownAttributes(
                    boundAttributes,
                    attributesToBind,
                    diagnostics,
                    symbolPart,
                    wellKnownAttributeData
                );

                RecordPresenceOfBadAttributes(boundAttributes);

                if (totalAttributesCount != 0) {
                    for (var i = 0; i < totalAttributesCount; i++) {
                        var boundAttribute = boundAttributeArray[i];
                        var attributeBinder = binders[i];

                        if (boundAttribute.constructor is { } ctor) {
                            // Binder.CheckRequiredMembersInObjectInitializer(ctor, ImmutableArray<BoundExpression>.CastUp(boundAttribute.NamedArguments), boundAttribute.Syntax, diagnostics);
                            // attributeBinder.ReportDiagnosticsIfObsolete(diagnostics, ctor, boundAttribute.Syntax, hasBaseReceiver: false);
                        }
                    }
                }

                AddDeclarationDiagnostics(diagnostics);
            }

            lazyAttributesStoredOnThisThread = true;

            if (lazyAttributesBag.isEmpty)
                lazyAttributesBag = CustomAttributesBag<AttributeData>.Empty;
        }

        diagnostics.Free();
        return lazyAttributesStoredOnThisThread;
    }

    private void RecordPresenceOfBadAttributes(ImmutableArray<AttributeData> boundAttributes) {
        foreach (var attribute in boundAttributes) {
            if (attribute.hasErrors) {
                var compilation = declaringCompilation;
                ((SourceModuleSymbol)compilation.sourceModule).RecordPresenceOfBadAttributes();
                break;
            }
        }
    }

    internal virtual void PostDecodeWellKnownAttributes(
        ImmutableArray<AttributeData> boundAttributes,
        ImmutableArray<AttributeSyntax> allAttributeSyntaxNodes,
        BelteDiagnosticQueue diagnostics,
        AttributeLocation symbolPart,
        WellKnownAttributeData decodedData) { }

    internal EarlyWellKnownAttributeData EarlyDecodeWellKnownAttributes(
        ImmutableArray<Binder> binders,
        ImmutableArray<NamedTypeSymbol> boundAttributeTypes,
        ImmutableArray<AttributeSyntax> attributesToBind,
        AttributeLocation symbolPart,
        AttributeData[] attributeDataArray,
        BoundAttribute[] boundAttributeArray) {
        var earlyBinder = new EarlyWellKnownAttributeBinder(binders[0]);
        var arguments = new EarlyDecodeWellKnownAttributeArguments<EarlyWellKnownAttributeBinder, NamedTypeSymbol, AttributeSyntax, AttributeLocation> {
            symbolPart = symbolPart
        };

        for (var i = 0; i < boundAttributeTypes.Length; i++) {
            var boundAttributeType = boundAttributeTypes[i];

            if (!boundAttributeType.IsErrorType()) {
                if (binders[i] != earlyBinder.next)
                    earlyBinder = new EarlyWellKnownAttributeBinder(binders[i]);

                arguments.binder = earlyBinder;
                arguments.attributeType = boundAttributeType;
                arguments.attributeSyntax = attributesToBind[i];

                var (earlyAttributeDataOpt, boundAttributeOpt) = EarlyDecodeWellKnownAttribute(ref arguments);
                attributeDataArray[i] = earlyAttributeDataOpt;

                boundAttributeArray?[i] = boundAttributeOpt;
            }
        }

        return arguments.hasDecodedData ? arguments.decodedData : null;
    }

    internal virtual (AttributeData, BoundAttribute) EarlyDecodeWellKnownAttribute(
        ref EarlyDecodeWellKnownAttributeArguments<EarlyWellKnownAttributeBinder, NamedTypeSymbol, AttributeSyntax, AttributeLocation> arguments) {
        return (null, null);
    }

    private void EarlyDecodeWellKnownAttributeTypes(
        ImmutableArray<NamedTypeSymbol> attributeTypes,
        ImmutableArray<AttributeSyntax> attributeSyntaxList) {
        for (var i = 0; i < attributeTypes.Length; i++) {
            var attributeType = attributeTypes[i];

            if (!attributeType.IsErrorType())
                EarlyDecodeWellKnownAttributeType(attributeType, attributeSyntaxList[i]);
        }
    }

    internal virtual void EarlyDecodeWellKnownAttributeType(
        NamedTypeSymbol attributeType,
        AttributeSyntax attributeSyntax) { }

    internal virtual void PostEarlyDecodeWellKnownAttributeTypes() { }

    private ImmutableArray<AttributeSyntax> GetAttributesToBind(
        OneOrMany<SyntaxList<AttributeListSyntax>> attributeDeclarationSyntaxLists,
        AttributeLocation symbolPart,
        BelteDiagnosticQueue diagnostics,
        Compilation compilation,
        Func<AttributeSyntax, bool> attributeMatchesOpt,
        Binder rootBinderOpt,
        out ImmutableArray<Binder> binders) {
        ArrayBuilder<AttributeSyntax> syntaxBuilder = null;
        ArrayBuilder<Binder> bindersBuilder = null;
        var attributesToBindCount = 0;
        var attributeTarget = (IAttributeTargetSymbol)this;

        for (var listIndex = 0; listIndex < attributeDeclarationSyntaxLists.Count; listIndex++) {
            var attributeDeclarationSyntaxList = attributeDeclarationSyntaxLists[listIndex];

            if (attributeDeclarationSyntaxList is not null && attributeDeclarationSyntaxList.Any()) {
                var prevCount = attributesToBindCount;

                foreach (var attributeDeclarationSyntax in attributeDeclarationSyntaxList) {
                    if (MatchAttributeTarget(attributeTarget, symbolPart, attributeDeclarationSyntax, diagnostics) &&
                        ShouldBindAttributes(attributeDeclarationSyntax, diagnostics)) {
                        if (syntaxBuilder is null) {
                            syntaxBuilder = new ArrayBuilder<AttributeSyntax>();
                            bindersBuilder = new ArrayBuilder<Binder>();
                        }

                        var attributesToBind = attributeDeclarationSyntax.attributes;

                        if (attributeMatchesOpt is null) {
                            syntaxBuilder.AddRange(attributesToBind);
                            attributesToBindCount += attributesToBind.Count;
                        } else {
                            foreach (var attribute in attributesToBind) {
                                if (attributeMatchesOpt(attribute)) {
                                    syntaxBuilder.Add(attribute);
                                    attributesToBindCount++;
                                }
                            }
                        }
                    }
                }

                if (attributesToBindCount != prevCount) {
                    var binder = GetAttributeBinder(attributeDeclarationSyntaxList, compilation, rootBinderOpt);

                    for (var i = 0; i < attributesToBindCount - prevCount; i++)
                        bindersBuilder.Add(binder);
                }
            }
        }

        if (syntaxBuilder is not null) {
            binders = bindersBuilder.ToImmutableAndFree();
            return syntaxBuilder.ToImmutableAndFree();
        } else {
            binders = [];
            return [];
        }
    }

    private static bool MatchAttributeTarget(
        IAttributeTargetSymbol attributeTarget,
        AttributeLocation symbolPart,
        AttributeListSyntax attributeList,
        BelteDiagnosticQueue diagnostics) {
        var defaultAttributeLocation = attributeTarget.defaultAttributeLocation;
        var attributesOwner = attributeTarget.attributesOwner;

        var isOwner = symbolPart == AttributeLocation.None && ReferenceEquals(attributesOwner, attributeTarget);

        return isOwner;
    }

    private protected virtual bool ShouldBindAttributes(
        AttributeListSyntax attributeDeclarationSyntax,
        BelteDiagnosticQueue diagnostics) {
        return true;
    }

    private Binder GetAttributeBinder(
        SyntaxList<AttributeListSyntax> attributeDeclarationSyntaxList,
        Compilation compilation,
        Binder rootBinder = null) {
        var binder = rootBinder ?? compilation.GetBinderFactory(attributeDeclarationSyntaxList.node.syntaxTree)
            .GetBinder(attributeDeclarationSyntaxList.node);

        binder = new ContextualAttributeBinder(binder, this);
        return binder;
    }

    // For PE interop use only
    internal virtual byte? GetNullableContextValue() {
        return GetLocalNullableContextValue() ?? containingSymbol?.GetNullableContextValue();
    }

    // For PE interop use only
    internal virtual byte? GetLocalNullableContextValue() {
        return null;
    }

    private protected static bool IsLocationContainedWithin(
        TextLocation location,
        SyntaxTree tree,
        TextSpan declarationSpan,
        out bool wasZeroWidthMatch) {
        if (location.isInSource && location.tree == tree && declarationSpan.Contains(location.span)) {
            wasZeroWidthMatch = location.span.length == 0 && location.span.end == declarationSpan.start;
            return true;
        }

        wasZeroWidthMatch = false;
        return false;
    }

    private protected void DecodeWellKnownAttribute(
        ref DecodeWellKnownAttributeArguments<AttributeSyntax, AttributeData, AttributeLocation> arguments) {
        DecodeWellKnownAttributeImpl(ref arguments);
    }

    private WellKnownAttributeData ValidateAttributeUsageAndDecodeWellKnownAttributes(
        ImmutableArray<Binder> binders,
        ImmutableArray<AttributeSyntax> attributeSyntaxList,
        ImmutableArray<AttributeData> boundAttributes,
        BelteDiagnosticQueue diagnostics,
        AttributeLocation symbolPart) {
        var totalAttributesCount = boundAttributes.Length;
        var uniqueAttributeTypes = new HashSet<NamedTypeSymbol>();
        var arguments = new DecodeWellKnownAttributeArguments<AttributeSyntax, AttributeData, AttributeLocation> {
            diagnostics = diagnostics,
            attributesCount = totalAttributesCount,
            symbolPart = symbolPart
        };

        for (var i = 0; i < totalAttributesCount; i++) {
            var boundAttribute = boundAttributes[i];
            var attributeSyntax = attributeSyntaxList[i];
            var binder = binders[i];

            if (!boundAttribute.hasErrors && ValidateAttributeUsage(
                    boundAttribute,
                    attributeSyntax,
                    binder.compilation,
                    symbolPart,
                    diagnostics,
                    uniqueAttributeTypes)) {
                arguments.attribute = boundAttribute;
                arguments.attributeSyntax = attributeSyntax;
                arguments.index = i;

                DecodeWellKnownAttribute(ref arguments);
            }
        }

        return arguments.hasDecodedData ? arguments.decodedData : null;
    }

    private bool ValidateAttributeUsage(
        AttributeData attribute,
        AttributeSyntax node,
        Compilation compilation,
        AttributeLocation symbolPart,
        BelteDiagnosticQueue diagnostics,
        HashSet<NamedTypeSymbol> uniqueAttributeTypes) {
        var attributeType = (NamedTypeSymbol)attribute.attributeClass;
        var attributeUsageInfo = attributeType.GetAttributeUsageInfo();

        if (!uniqueAttributeTypes.Add(attributeType.originalDefinition) && !attributeUsageInfo.allowMultiple) {
            diagnostics.Push(Error.DuplicateAttribute(node.name.location, node.GetErrorDisplayName()));
            return false;
        }

        AttributeTargets attributeTarget;

        if (symbolPart == AttributeLocation.Return)
            attributeTarget = AttributeTargets.ReturnValue;
        else
            attributeTarget = GetAttributeTarget();

        if ((attributeTarget & attributeUsageInfo.validTargets) == 0) {
            diagnostics.Push(Error.AttributeOnBadSymbolType(
                node.name.location,
                node.GetErrorDisplayName(),
                attributeUsageInfo.GetValidTargetsErrorArgument()
            ));

            return false;
        }

        return true;
    }

    internal virtual AttributeTargets GetAttributeTarget() {
        switch (kind) {
            case SymbolKind.Assembly:
                return AttributeTargets.Assembly;
            case SymbolKind.Field:
                return AttributeTargets.Field;
            case SymbolKind.Method:
                var method = (MethodSymbol)this;

                switch (method.methodKind) {
                    case MethodKind.Constructor:
                    case MethodKind.StaticConstructor:
                        return AttributeTargets.Constructor;
                    default:
                        return AttributeTargets.Method;
                }
            case SymbolKind.NamedType:
                var namedType = (NamedTypeSymbol)this;

                switch (namedType.typeKind) {
                    case TypeKind.Class:
                        return AttributeTargets.Class;
                    case TypeKind.Enum:
                        return AttributeTargets.Enum;
                    case TypeKind.Interface:
                        return AttributeTargets.Interface;
                    case TypeKind.Struct:
                        return AttributeTargets.Struct;
                    case TypeKind.TemplateParameter:
                        return AttributeTargets.GenericParameter;
                }

                break;
            case SymbolKind.Module:
                return AttributeTargets.Module;
            case SymbolKind.Parameter:
                return AttributeTargets.Parameter;
            case SymbolKind.Property:
                return AttributeTargets.Property;
            case SymbolKind.TemplateParameter:
                return AttributeTargets.GenericParameter;
        }

        return 0;
    }

    private protected virtual void DecodeWellKnownAttributeImpl(
        ref DecodeWellKnownAttributeArguments<AttributeSyntax, AttributeData, AttributeLocation> arguments) { }

    internal static ImmutableArray<SyntaxReference> GetDeclaringSyntaxReferenceHelper<TNode>(
        ImmutableArray<TextLocation> locations)
        where TNode : BelteSyntaxNode {
        if (locations.IsEmpty)
            return [];

        var builder = ArrayBuilder<SyntaxReference>.GetInstance();

        foreach (var location in locations) {
            if (location is null || !location.isInSource) {
                continue;
            }

            if (location.span.length != 0) {
                var token = location.tree.GetRoot().FindToken(location.span.start);

                if (token.kind != SyntaxKind.None) {
                    var node = token.parent.FirstAncestorOrSelf<TNode>();

                    if (node is not null)
                        builder.Add(new SyntaxReference(node));
                }
            } else {
                SyntaxNode parent = location.tree.GetRoot();
                SyntaxNode found = null;

                foreach (var descendant in parent.DescendantNodesAndSelf(
                    c => c.location.span.Contains(location.span))) {
                    if (descendant is TNode && descendant.location.span.Contains(location.span))
                        found = descendant;
                }

                if (found is not null)
                    builder.Add(new SyntaxReference(found));
            }
        }

        return builder.ToImmutableAndFree();
    }

    internal bool IsFromCompilation(Compilation compilation) {
        return compilation == declaringCompilation;
    }

    internal ImmutableArray<BoundExpression> GetEnclosingTemplateConstraints() {
        var builder = ArrayBuilder<BoundExpression>.GetInstance();

        // Don't include this symbols constraints
        for (var current = containingSymbol; current is not null; current = current.containingSymbol) {
            if (current is ISymbolWithTemplates tm) {
                Debug.Assert(!tm.templateConstraints.IsDefault);
                builder.AddRange(tm.templateConstraints);
            }
        }

        return builder.ToImmutableAndFree();
    }

    internal bool Equals(Symbol other) {
        return Equals(other, SymbolEqualityComparer.Default.compareKind);
    }

    internal bool Equals(Symbol other, SymbolEqualityComparer comparer) {
        return Equals(other, comparer.compareKind);
    }

    internal static bool Equals(Symbol first, Symbol second, TypeCompareKind compareKind) {
        if (first is null)
            return second is null;

        return first.Equals(second, compareKind);
    }

    internal virtual bool Equals(Symbol other, TypeCompareKind compareKind) {
        return (object)this == other;
    }

    private string GetDebuggerDisplay() {
        return $"{kind} {ToDisplayString(SymbolDisplayFormat.ErrorMessageFormat)}";
    }

    public string ToDisplayString(SymbolDisplayFormat format) {
        return SymbolDisplay.ToDisplayString(this, format);
    }

    public ImmutableArray<DisplayTextSegment> ToDisplaySegments(SymbolDisplayFormat format) {
        return SymbolDisplay.ToDisplaySegments(this, format);
    }

    public override string ToString() {
        return ToDisplayString(null);
    }

    public override int GetHashCode() {
        return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    public sealed override bool Equals(object obj) {
        return Equals(obj as Symbol, SymbolEqualityComparer.Default.compareKind);
    }

    public static bool operator ==(Symbol left, Symbol right) {
        if (right is null)
            return left is null;

        return (object)left == right || right.Equals(left);
    }

    public static bool operator !=(Symbol left, Symbol right) {
        if (right is null)
            return left is not null;

        return (object)left != right && !right.Equals(left);
    }

    ISymbol ISymbol.containingSymbol => containingSymbol;

    Compilation ISymbol.declaringCompilation => declaringCompilation;

    bool ISymbol.canBeReferencedByName => canBeReferencedByName;
}
