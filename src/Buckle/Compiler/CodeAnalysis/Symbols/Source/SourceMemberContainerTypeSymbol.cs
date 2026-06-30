using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMemberContainerTypeSymbol : NamedTypeSymbol {
    internal delegate void ReportMismatchInReturnType<TArg>(
        BelteDiagnosticQueue bag,
        MethodSymbol overriddenMethod,
        MethodSymbol overridingMethod,
        bool topLevel,
        TArg arg
    );

    internal delegate void ReportMismatchInParameterType<TArg>(
        BelteDiagnosticQueue bag,
        MethodSymbol overriddenMethod,
        MethodSymbol overridingMethod,
        ParameterSymbol parameter,
        bool topLevel,
        TArg arg
    );

    private static readonly Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> EmptyTypeMembers =
        new Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>>(EmptyReadOnlyMemoryOfCharComparer.Instance);

    private static readonly ReportMismatchInReturnType<TextLocation> ReportBadReturn =
        (diagnostics, overriddenMethod, overridingMethod, topLevel, location)
        => diagnostics.Push(
            topLevel
                ? Warning.TopLevelNullabilityMismatchInReturnTypeOnOverride(location)
                : Warning.NullabilityMismatchInReturnTypeOnOverride(location));

    private static readonly ReportMismatchInParameterType<TextLocation> ReportBadParameter =
        (diagnostics, overriddenMethod, overridingMethod, overridingParameter, topLevel, location)
        => diagnostics.Push(
            topLevel
                ? Warning.TopLevelNullabilityMismatchInParameterTypeOnOverride(location, overridingParameter)
                : Warning.NullabilityMismatchInParameterTypeOnOverride(location, overridingParameter));

    private readonly DeclarationModifiers _modifiers;
    private protected SymbolCompletionState _state;
    private protected readonly MergedTypeDeclaration _declaration;

    private ImmutableArray<SynthesizedEntryPoint> _lazySimpleProgramEntryPoints;
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> _lazyTypeMembers;
    private DeclaredMembersAndInitializers _lazyDeclaredMembersAndInitializers = DeclaredMembersAndInitializers.UninitializedSentinel;
    private MembersAndInitializers _lazyMembersAndInitializers;
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> _lazyMembersDictionary;
    private Dictionary<FieldSymbol, AnonymousUnionType> _lazyAnonymousUnionTypes;
    private Dictionary<AnonymousUnionType, FieldSymbol> _lazyAnonymousUnionFields;
    private ImmutableArray<Symbol> _lazyMembersFlattened;
    private SynthesizedExplicitImplementations _lazySynthesizedExplicitImplementations;
    private ThreeState _lazyAnyMemberHasAttributes;
    private Dictionary<SyntaxNode, ScopeInheritorInfo> _lazyScopeInheritorInfo;
    private int _lazyKnownCircularStruct;
    private int _lazyHasStructDefault;
    private int _lazyKnownToBeImmutable;

    private bool _fieldDefinitionsNoted;

    internal SourceMemberContainerTypeSymbol(
        NamespaceOrTypeSymbol containingSymbol,
        MergedTypeDeclaration declaration,
        BelteDiagnosticQueue diagnostics,
        TupleExtraData tupleData = null)
        : base(tupleData) {
        this.containingSymbol = containingSymbol;
        _declaration = declaration;
        location = declaration.nameLocations.First();
        typeKind = declaration.kind.ToTypeKind();

        var modifiers = MakeModifiers(diagnostics);
        var access = (int)(modifiers & DeclarationModifiers.AccessibilityMask);

        foreach (var singleDeclaration in declaration.declarations)
            diagnostics.PushRange(singleDeclaration.diagnostics);

        if ((access & (access - 1)) != 0) {
            access &= ~(access - 1);
            modifiers &= ~DeclarationModifiers.AccessibilityMask;
            modifiers |= (DeclarationModifiers)access;
        }

        _modifiers = modifiers;
        specialType = MakeSpecialType();

        var containingType = this.containingType;

        if (containingType?.isSealed == true && declaredAccessibility.HasFlag(Accessibility.Protected))
            diagnostics.Push(Warning.ProtectedInSealed(location, this));

        _state.NotePartComplete(CompletionParts.TemplateArguments);

        enumFlagsAttribute = syntaxReference.node is EnumDeclarationSyntax e && e.flagsKeyword is not null;

        if (syntaxReference.node is StructDeclarationSyntax s && s.packedArgument is not null)
            explicitAlignment = MakeExplicitAlignment(s.packedArgument, diagnostics);

        isFileScoped = declaration.syntaxReferences[0].node.kind == SyntaxKind.FileScopedClassDeclaration;
    }

    public override string name => _declaration.name;

    public override int arity => _declaration.arity;

    public override TypeKind typeKind { get; }

    public override SpecialType specialType { get; }

    internal sealed override bool mangleName => arity > 0;

    internal sealed override bool isStatic => HasFlag(DeclarationModifiers.Static);

    internal sealed override bool isAbstract => HasFlag(DeclarationModifiers.Abstract);

    internal sealed override bool isSealed => HasFlag(DeclarationModifiers.Sealed);

    internal sealed override bool requiresCompletion => true;

    internal sealed override Accessibility declaredAccessibility
        => ModifierHelpers.EffectiveAccessibility(_modifiers, isFileScoped);

    internal sealed override NamedTypeSymbol constructedFrom => this;

    internal override bool isLowLevel => HasFlag(DeclarationModifiers.LowLevel) || containingType?.isLowLevel == true;

    internal override Symbol containingSymbol { get; }

    internal override SyntaxReference syntaxReference => _declaration.syntaxReferences.First();

    internal override TextLocation location { get; }

    internal override IEnumerable<string> memberNames => GetMembers().Select(m => m.name);

    internal sealed override bool isRefLikeType => HasFlag(DeclarationModifiers.Ref);

    internal override bool enumFlagsAttribute { get; }

    internal override int? explicitAlignment { get; }

    internal bool anyMemberHasAttributes {
        get {
            if (!_lazyAnyMemberHasAttributes.HasValue()) {
                var anyMemberHasAttributes = _declaration.anyMemberHasAttributes;
                _lazyAnyMemberHasAttributes = anyMemberHasAttributes.ToThreeState();
            }

            return _lazyAnyMemberHasAttributes.Value();
        }
    }

    internal override bool isImplicitClass => _declaration.declarations[0].kind == DeclarationKind.ImplicitClass;

    internal override bool isImplicitlyDeclared => isImplicitClass;

    internal MergedTypeDeclaration mergedDeclaration => _declaration;

    internal ImmutableArray<ImmutableArray<FieldInitializer>> instanceInitializers
        => GetMembersAndInitializers().instanceInitializers;

    internal ImmutableArray<ImmutableArray<FieldInitializer>> staticInitializers
        => GetMembersAndInitializers().staticInitializers;

    internal Dictionary<FieldSymbol, AnonymousUnionType> anonymousUnionTypes => _lazyAnonymousUnionTypes;

    internal Dictionary<AnonymousUnionType, FieldSymbol> anonymousUnionFields => _lazyAnonymousUnionFields;

    internal bool isFileScoped { get; }

    internal override bool knownCircularStruct {
        get {
            if (_lazyKnownCircularStruct == (int)ThreeState.Unknown) {
                if (typeKind != TypeKind.Struct) {
                    Interlocked.CompareExchange(
                        ref _lazyKnownCircularStruct,
                        (int)ThreeState.False,
                        (int)ThreeState.Unknown
                    );
                } else {
                    var diagnostics = BelteDiagnosticQueue.GetInstance();
                    var value = (int)CheckStructCircularity(diagnostics).ToThreeState();

                    if (Interlocked.CompareExchange(ref _lazyKnownCircularStruct, value, (int)ThreeState.Unknown) ==
                        (int)ThreeState.Unknown) {
                        AddDeclarationDiagnostics(diagnostics);
                    }

                    diagnostics.Free();
                }
            }

            return _lazyKnownCircularStruct == (int)ThreeState.True;
        }
    }

    internal override bool hasStructDefault {
        get {
            if (_lazyHasStructDefault == (int)ThreeState.Unknown) {
                if (typeKind != TypeKind.Struct) {
                    Interlocked.CompareExchange(
                        ref _lazyHasStructDefault,
                        (int)ThreeState.False,
                        (int)ThreeState.Unknown
                    );
                } else {
                    var value = (int)CheckHasStructDefault().ToThreeState();
                    Interlocked.CompareExchange(ref _lazyHasStructDefault, value, (int)ThreeState.Unknown);
                }
            }

            return _lazyHasStructDefault == (int)ThreeState.True;
        }
    }

    internal override bool isKnownToBeImmutable {
        get {
            if (_lazyKnownToBeImmutable == (int)ThreeState.Unknown) {
                if (typeKind != TypeKind.Class) {
                    Interlocked.CompareExchange(
                        ref _lazyKnownToBeImmutable,
                        (int)ThreeState.False,
                        (int)ThreeState.Unknown
                    );
                } else {
                    var value = (int)CheckKnownToBeImmutable().ToThreeState();
                    Interlocked.CompareExchange(ref _lazyKnownToBeImmutable, value, (int)ThreeState.Unknown);
                }
            }

            return _lazyKnownToBeImmutable == (int)ThreeState.True;
        }
    }

    internal sealed override bool isInterface => typeKind == TypeKind.Interface;

    internal sealed override ImmutableArray<Symbol> GetMembers() {
        if (!_lazyMembersFlattened.IsDefault)
            return _lazyMembersFlattened;

        var members = GetMembersByName().Flatten();

        if (members.Length > 1) {
            members = members.Sort(LexicalOrderSymbolComparer.Instance);
            ImmutableInterlocked.InterlockedExchange(ref _lazyMembersFlattened, members);
        }

        return members;
    }

    internal sealed override ImmutableArray<Symbol> GetMembers(string name) {
        if (GetMembersByName().TryGetValue(name.AsMemory(), out var members))
            return members;

        return [];
    }

    internal override ImmutableArray<Symbol> GetMembersUnordered() {
        var result = _lazyMembersFlattened;

        if (result.IsDefault) {
            result = GetMembersByName().Flatten();
            ImmutableInterlocked.InterlockedInitialize(ref _lazyMembersFlattened, result);
            result = _lazyMembersFlattened;
        }

        return result;
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        return GetTypeMembersDictionary().Flatten(LexicalOrderSymbolComparer.Instance);
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        if (GetTypeMembersDictionary().TryGetValue(name, out var members))
            return members;

        return [];
    }

    internal override ImmutableArray<NamedTypeSymbol> GetTypeMembersUnordered() {
        return GetTypeMembersDictionary().Flatten();
    }

    internal override void ForceComplete(TextLocation location) {
        while (true) {
            var incompletePart = _state.nextIncompletePart;

            switch (incompletePart) {
                case CompletionParts.Attributes:
                    GetAttributes();
                    break;
                case CompletionParts.StartBaseType:
                case CompletionParts.FinishBaseType:
                    if (_state.NotePartComplete(CompletionParts.StartBaseType)) {
                        var diagnostics = BelteDiagnosticQueue.GetInstance();
                        CheckBase(diagnostics);
                        AddDeclarationDiagnostics(diagnostics);
                        _state.NotePartComplete(CompletionParts.FinishBaseType);
                        diagnostics.Free();
                    }

                    break;
                case CompletionParts.StartInterfaces:
                case CompletionParts.FinishInterfaces:
                    if (_state.NotePartComplete(CompletionParts.StartInterfaces)) {
                        var diagnostics = BelteDiagnosticQueue.GetInstance();
                        CheckInterfaces(diagnostics);
                        AddDeclarationDiagnostics(diagnostics);
                        _state.NotePartComplete(CompletionParts.FinishInterfaces);
                        diagnostics.Free();
                    }

                    break;
                case CompletionParts.EnumUnderlyingType:
                    _ = enumUnderlyingType;
                    break;
                case CompletionParts.TemplateArguments:
                    _ = templateArguments;
                    break;
                case CompletionParts.TemplateParameters:
                    foreach (var templateParameter in templateParameters)
                        templateParameter.ForceComplete(location);

                    _ = templateConstraints;
                    _state.NotePartComplete(CompletionParts.TemplateParameters);
                    break;
                case CompletionParts.Members:
                    GetMembersByName();
                    break;
                case CompletionParts.TypeMembers:
                    GetTypeMembersUnordered();
                    break;
                case CompletionParts.SynthesizedExplicitImplementations:
                    GetSynthesizedExplicitImplementations();
                    break;
                case CompletionParts.StartMemberChecks:
                case CompletionParts.FinishMemberChecks:
                    if (_state.NotePartComplete(CompletionParts.StartMemberChecks)) {
                        var diagnostics = BelteDiagnosticQueue.GetInstance();
                        AfterMembersChecks(diagnostics);
                        AddDeclarationDiagnostics(diagnostics);
                        _state.NotePartComplete(CompletionParts.FinishMemberChecks);
                        diagnostics.Free();
                    }

                    break;
                case CompletionParts.MembersCompletedChecksStarted:
                case CompletionParts.MembersCompleted: {
                        var members = GetMembersUnordered();

                        foreach (var member in members)
                            member.ForceComplete(location);

                        EnsureFieldDefinitionsNoted();

                        if (_state.NotePartComplete(CompletionParts.MembersCompletedChecksStarted)) {
                            var diagnostics = BelteDiagnosticQueue.GetInstance();
                            AfterMembersCompletedChecks(diagnostics);
                            AddDeclarationDiagnostics(diagnostics);
                            _state.NotePartComplete(CompletionParts.MembersCompleted);
                            diagnostics.Free();
                        }
                    }

                    break;
                case CompletionParts.None:
                    return;
                default:
                    _state.NotePartComplete(CompletionParts.All & ~CompletionParts.NamedTypeSymbolAll);
                    break;
            }

            _state.SpinWaitComplete(incompletePart);
        }

        throw ExceptionUtilities.Unreachable();
    }

    internal sealed override bool HasComplete(CompletionParts part) {
        return _state.HasComplete(part);
    }

    internal override ImmutableArray<Symbol> GetSimpleNonTypeMembers(string name) {
        if (_lazyMembersDictionary is not null ||
            _declaration.memberNames.Contains(name)) {
            return GetMembers(name);
        }

        return [];
    }

    internal Binder GetBinder(BelteSyntaxNode syntaxNode) {
        return declaringCompilation.GetBinder(syntaxNode);
    }

    internal ImmutableArray<SynthesizedEntryPoint> GetSimpleProgramEntryPoints() {
        if (_lazySimpleProgramEntryPoints.IsDefault) {
            var diagnostics = BelteDiagnosticQueue.GetInstance();
            var simpleProgramEntryPoints = BuildSimpleProgramEntryPoint(diagnostics);

            if (ImmutableInterlocked.InterlockedInitialize(ref _lazySimpleProgramEntryPoints, simpleProgramEntryPoints))
                AddDeclarationDiagnostics(diagnostics);

            diagnostics.Free();
        }

        return _lazySimpleProgramEntryPoints;

        ImmutableArray<SynthesizedEntryPoint> BuildSimpleProgramEntryPoint(BelteDiagnosticQueue diagnostics) {
            if (containingSymbol is not NamespaceSymbol { isGlobalNamespace: true }
                || name != WellKnownMemberNames.TopLevelStatementsEntryPointTypeName) {
                return [];
            }

            ArrayBuilder<SynthesizedEntryPoint> builder = null;

            foreach (var singleDecl in _declaration.declarations) {
                if (singleDecl.isSimpleProgram) {
                    if (builder is null)
                        builder = ArrayBuilder<SynthesizedEntryPoint>.GetInstance();
                    else
                        diagnostics.Push(Error.GlobalStatementsInMultipleFiles(singleDecl.nameLocation));

                    builder.Add(new SynthesizedEntryPoint(this, singleDecl));
                }
            }

            if (builder is null)
                return [];

            return builder.ToImmutableAndFree();
        }
    }

    internal SynthesizedExplicitImplementations GetSynthesizedExplicitImplementations() {
        if (_lazySynthesizedExplicitImplementations is null) {
            var diagnostics = BelteDiagnosticQueue.GetInstance();

            try {
                CheckMembersAgainstBaseType(diagnostics);
                CheckAbstractClassImplementations(diagnostics);
                CheckInterfaceUnification(diagnostics);

                // TODO Do we need this?
                // if (isInterface)
                //     CheckInterfaceVarianceSafety(diagnostics);

                AddDeclarationDiagnostics(diagnostics);

                if (Interlocked.CompareExchange(
                    ref _lazySynthesizedExplicitImplementations,
                    ComputeInterfaceImplementations(diagnostics),
                    null) is null) {
                    AddDeclarationDiagnostics(diagnostics);
                    _state.NotePartComplete(CompletionParts.SynthesizedExplicitImplementations);
                }
            } finally {
                diagnostics.Free();
            }
        }

        return _lazySynthesizedExplicitImplementations;
    }

    internal sealed override IEnumerable<(MethodSymbol Body, MethodSymbol Implemented)> SynthesizedInterfaceMethodImpls() {
        var synthesizedImplementations = GetSynthesizedExplicitImplementations();

        foreach (var methodImpl in synthesizedImplementations.methodImpls)
            yield return methodImpl;

        foreach (var forwardingMethod in synthesizedImplementations.forwardingMethods) {
            yield return (
                forwardingMethod.implementingMethod,
                forwardingMethod.explicitInterfaceImplementations.Single()
            );
        }
    }

    private SynthesizedExplicitImplementations ComputeInterfaceImplementations(BelteDiagnosticQueue diagnostics) {
        var forwardingMethods = ArrayBuilder<SynthesizedExplicitImplementationForwardingMethod>.GetInstance();
        var methodImpls = ArrayBuilder<(MethodSymbol Body, MethodSymbol Implemented)>.GetInstance();

        var interfacesAndTheirBases = interfacesAndTheirBaseInterfaces;

        foreach (var @interface in allInterfaces) {
            if (!interfacesAndTheirBases[@interface].Contains(@interface))
                continue;

            bool? hasBaseClassDeclaringInterface = null;

            foreach (var interfaceMember in @interface.GetMembers()) {
                var interfaceMemberKind = interfaceMember.kind;

                switch (interfaceMemberKind) {
                    case SymbolKind.Method:
                        if (!interfaceMember.IsImplementableInterfaceMember())
                            continue;

                        break;
                    default:
                        continue;
                }

                SymbolAndDiagnostics implementingMemberAndDiagnostics;

                if (isInterface) {
                    var explicitImpl = GetExplicitImplementationForInterfaceMember(interfaceMember);

                    switch (explicitImpl.Count) {
                        case 0:
                            continue;
                        case 1:
                            implementingMemberAndDiagnostics = new SymbolAndDiagnostics(
                                explicitImpl.Single(),
                                BelteDiagnosticQueue.Discarded
                            );

                            break;
                        default:
                            var error = Error.DuplicateExplicitImpl(location, interfaceMember);

                            implementingMemberAndDiagnostics = new SymbolAndDiagnostics(
                                null,
                                new BelteDiagnosticQueue([error])
                            );

                            break;
                    }
                } else {
                    implementingMemberAndDiagnostics = FindImplementationForInterfaceMemberInNonInterface(
                        interfaceMember
                    );
                }

                var implementingMember = implementingMemberAndDiagnostics.symbol;
                var synthesizedImplementation = SynthesizeInterfaceMemberImplementation(
                    implementingMemberAndDiagnostics,
                    interfaceMember
                );

                var wasImplementingMemberFound = implementingMember is not null;

                if (synthesizedImplementation.ForwardingMethod is
                    SynthesizedExplicitImplementationForwardingMethod forwardingMethod) {
                    forwardingMethods.Add(forwardingMethod);
                }

                if (synthesizedImplementation.MethodImpl is { } methodImpl)
                    methodImpls.Add(methodImpl);

                var reportedAnError = false;

                if (implementingMemberAndDiagnostics.diagnostics.Any()) {
                    diagnostics.PushRange(implementingMemberAndDiagnostics.diagnostics);
                    reportedAnError = implementingMemberAndDiagnostics.diagnostics.ToArray()
                        .Any(static d => d.info.severity == DiagnosticSeverity.Error);
                }

                if (!reportedAnError) {
                    if (!wasImplementingMemberFound ||
                        (!implementingMember.containingType.Equals(this, TypeCompareKind.ConsiderEverything) &&
                        implementingMember.GetExplicitInterfaceImplementations()
                            .Contains(interfaceMember, ExplicitInterfaceImplementationTargetMemberEqualityComparer.Instance))) {
                        hasBaseClassDeclaringInterface ??= HasBaseClassDeclaringInterface(@interface);

                        var matchResult = hasBaseClassDeclaringInterface.GetValueOrDefault();

                        if (matchResult != true &&
                            wasImplementingMemberFound && implementingMember.containingType.isInterface) {
                            HasBaseInterfaceDeclaringInterface(
                                implementingMember.containingType,
                                @interface,
                                ref matchResult
                            );
                        }

                        switch (matchResult) {
                            case false: {
                                    if (!interfaceMember.MustCallMethodsDirectly()) {
                                        diagnostics.Push(Error.UnimplementedInterfaceMember(
                                            GetImplementsLocationOrFallback(@interface),
                                            this,
                                            interfaceMember
                                        ));
                                    }
                                }

                                break;
                            case true:
                                break;
                        }
                    }
                }
            }
        }

        return SynthesizedExplicitImplementations.Create(
            forwardingMethods.ToImmutableAndFree(),
            methodImpls.ToImmutableAndFree()
        );
    }

    private void HasBaseInterfaceDeclaringInterface(
        NamedTypeSymbol baseInterface,
        NamedTypeSymbol @interface,
        ref bool matchResult) {
        if (DeclaresBaseInterface(baseInterface, @interface, ref matchResult))
            return;

        foreach (var interfaceType in allInterfaces) {
            if ((object)interfaceType == baseInterface)
                continue;

            if (interfaceType.Equals(baseInterface, TypeCompareKind.CLRSignatureCompareOptions) &&
                DeclaresBaseInterface(interfaceType, @interface, ref matchResult)) {
                return;
            }
        }
    }

    private TextLocation GetImplementsLocationOrFallback(NamedTypeSymbol implementedInterface) {
        return GetImplementsLocation(implementedInterface) ?? location;
    }

    internal TextLocation GetImplementsLocation(NamedTypeSymbol implementedInterface) {
        NamedTypeSymbol directInterface = null;

        foreach (var iface in Interfaces()) {
            if (Equals(iface, implementedInterface, TypeCompareKind.ConsiderEverything)) {
                directInterface = iface;
                break;
            } else if (directInterface is null && iface.ImplementsInterface(implementedInterface)) {
                directInterface = iface;
            }
        }

        return GetCorrespondingBaseListLocation(directInterface);
    }

    private protected abstract TextLocation GetCorrespondingBaseListLocation(NamedTypeSymbol @base);

    private bool HasBaseClassDeclaringInterface(NamedTypeSymbol @interface) {
        var result = false;

        for (var currType = baseType; currType is not null; currType = currType.baseType) {
            if (DeclaresBaseInterface(currType, @interface, ref result))
                break;
        }

        return result;
    }

    private static bool DeclaresBaseInterface(
        NamedTypeSymbol currType,
        NamedTypeSymbol @interface,
        ref bool result) {
        var set = currType.interfacesAndTheirBaseInterfaces[@interface];

        if (set.Count != 0) {
            if (set.Contains(@interface)) {
                result = true;
                return true;
            }
        }

        return false;
    }

    private (SynthesizedExplicitImplementationForwardingMethod ForwardingMethod, (MethodSymbol Body, MethodSymbol Implemented)? MethodImpl) SynthesizeInterfaceMemberImplementation(
        SymbolAndDiagnostics implementingMemberAndDiagnostics,
        Symbol interfaceMember) {
        foreach (var diagnostic in implementingMemberAndDiagnostics.diagnostics) {
            // if (diagnostic.info.severity == DiagnosticSeverity.Error && diagnostic.info.code is not ((int)DiagnosticCode.ERR_ImplicitImplementationOfNonPublicInterfaceMember or (int)DiagnosticCode.ERR_ImplicitImplementationOfInaccessibleInterfaceMember)) {
            return default;
            // }
        }

        var implementingMember = implementingMemberAndDiagnostics.symbol;

        if (implementingMember is null || implementingMember.kind != SymbolKind.Method)
            return default;

        var interfaceMethod = (MethodSymbol)interfaceMember;
        var implementingMethod = (MethodSymbol)implementingMember;

        if (implementingMethod.explicitInterfaceImplementations
            .Contains(interfaceMethod, ExplicitInterfaceImplementationTargetMemberEqualityComparer.Instance)) {
            return default;
        }

        if (!interfaceMethod.isStatic) {
            var implementingMethodOriginalDefinition = implementingMethod.originalDefinition;
            var needSynthesizedImplementation = true;

            if (MemberSignatureComparer.RuntimeImplicitImplementationComparer.Equals(implementingMethod, interfaceMethod) &&
                IsOverrideOfPossibleImplementationUnderRuntimeRules(implementingMethod, @interfaceMethod.containingType)) {
                if (ReferenceEquals(containingModule, implementingMethodOriginalDefinition.containingModule)) {
                    if (implementingMethodOriginalDefinition is SourceMemberMethodSymbol sourceImplementMethodOriginalDefinition) {
                        sourceImplementMethodOriginalDefinition.EnsureMetadataVirtual();
                        needSynthesizedImplementation = false;
                    }
                }
                // TODO interfaces
                // else if (implementingMethod.IsMetadataVirtual(MethodSymbol.IsMetadataVirtualOption.IgnoreInterfaceImplementationChanges)) {
                //     // If the signatures match and the implementation method is definitely virtual, then we're set.
                //     needSynthesizedImplementation = false;
                // }
            }

            if (!needSynthesizedImplementation)
                return default;
        } else {
            if (implementingMethod.containingType != (object)this) {
                if (implementingMethod.containingType.isInterface ||
                    implementingMethod.Equals(
                        baseType?.FindImplementationForInterfaceMemberInNonInterface(interfaceMethod).symbol,
                        TypeCompareKind.CLRSignatureCompareOptions)) {
                    return default;
                }
            } else if (MemberSignatureComparer.RuntimeExplicitImplementationSignatureComparer.Equals(
                    implementingMethod,
                    interfaceMethod)) {
                return (null, (implementingMethod, interfaceMethod));
            }
        }

        return (new SynthesizedExplicitImplementationForwardingMethod(interfaceMethod, implementingMethod, this), null);
    }

    private static bool IsOverrideOfPossibleImplementationUnderRuntimeRules(
        MethodSymbol implementingMethod,
        NamedTypeSymbol @interface) {
        var curr = implementingMethod;

        while (curr is not null) {
            if (IsPossibleImplementationUnderRuntimeRules(curr, @interface))
                return true;

            curr = curr.overriddenMethod;
        }

        return false;
    }

    private static bool IsPossibleImplementationUnderRuntimeRules(
        MethodSymbol implementingMethod,
        NamedTypeSymbol @interface) {
        var type = implementingMethod.containingType;

        if (type.interfacesAndTheirBaseInterfaces.ContainsKey(@interface))
            return true;

        var baseType = type.baseType;
        return baseType is null || !baseType.allInterfaces.Contains(@interface);
    }

    private protected abstract void CheckBase(BelteDiagnosticQueue diagnostics);

    private protected abstract void CheckInterfaces(BelteDiagnosticQueue diagnostics);

    private protected virtual void AfterMembersCompletedChecks(BelteDiagnosticQueue diagnostics) { }

    private protected void AfterMembersChecks(BelteDiagnosticQueue diagnostics) {
        if (isInterface)
            CheckInterfaceMembers(GetMembersAndInitializers().nonTypeMembers, diagnostics);

        CheckMemberNamesDistinctFromType(diagnostics);
        CheckMemberNameConflicts(diagnostics);
        CheckSpecialMemberErrors(diagnostics);
        CheckTemplateParameterNameConflicts(diagnostics);

        _ = knownCircularStruct;

        CheckForProtectedInStaticClass(diagnostics);
        CheckForUnmatchedOperators(diagnostics);
        CheckUnionIsNonEmpty(diagnostics);

        _ = baseType;

        // TODO We don't actually use a symbol adapter to get members to emit (i.e. GetInterfacesToEmit)
        // But we will still use this useless calculation as an excuse to visit everything for diagnostic collection
        // Eventually we will probably use a proper adapter where this calculation will actually be useful

        // var interfaces = GetInterfacesToEmit();
        _ = CalculateInterfacesToEmit();

        CheckStructLayoutEfficiency(diagnostics);
    }

    private void CheckInterfaceUnification(BelteDiagnosticQueue diagnostics) {
        if (!isTemplateType)
            return;

        var numInterfaces = interfacesAndTheirBaseInterfaces.Count;

        if (numInterfaces < 2)
            return;

        var interfaces = interfacesAndTheirBaseInterfaces.Keys.ToArray();

        for (var i1 = 0; i1 < numInterfaces; i1++) {
            for (var i2 = i1 + 1; i2 < numInterfaces; i2++) {
                var interface1 = interfaces[i1];
                var interface2 = interfaces[i2];

                if (interface1.isTemplateType && interface2.isTemplateType &&
                    Equals(
                        interface1.originalDefinition,
                        interface2.originalDefinition,
                        TypeCompareKind.ConsiderEverything) &&
                    interface1.CanUnifyWith(interface2)) {
                    if (GetImplementsLocationOrFallback(interface1).span.start >
                        GetImplementsLocationOrFallback(interface2).span.start) {
                        (interface2, interface1) = (interface1, interface2);
                    }

                    diagnostics.Push(Error.UnifyingInterfaceInstantiations(location, this, interface1, interface2));
                }
            }
        }
    }

    private static void CheckInterfaceMembers(ImmutableArray<Symbol> nonTypeMembers, BelteDiagnosticQueue diagnostics) {
        foreach (var member in nonTypeMembers)
            CheckInterfaceMember(member, diagnostics);
    }

    private static void CheckInterfaceMember(Symbol member, BelteDiagnosticQueue diagnostics) {
        switch (member.kind) {
            case SymbolKind.Field:
                break;
            case SymbolKind.Method:
                var meth = (MethodSymbol)member;

                switch (meth.methodKind) {
                    case MethodKind.Constructor:
                        diagnostics.Push(Error.InterfacesCantContainConstructors(member.location));
                        break;
                    case MethodKind.Conversion:
                        break;
                    case MethodKind.Operator:
                        break;
                    case MethodKind.Finalizer:
                        diagnostics.Push(Error.OnlyClassesCanContainFinalizers(member.location));
                        break;
                    case MethodKind.ExplicitInterfaceImplementation:
                    case MethodKind.Ordinary:
                    case MethodKind.LocalFunction:
                    case MethodKind.StaticConstructor:
                    case MethodKind.Destructor:
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(meth.methodKind);
                }

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(member.kind);
        }
    }

    private void CheckUnionIsNonEmpty(BelteDiagnosticQueue diagnostics) {
        if (!isUnionStruct)
            return;

        var hasNonStaticField = false;

        foreach (var m in GetMembers()) {
            if (m is FieldSymbol f && !f.isStatic) {
                hasNonStaticField = true;
                break;
            }
        }

        if (!hasNonStaticField)
            diagnostics.Push(Error.UnionMustHaveField(location));
    }

    private void CheckForUnmatchedOperators(BelteDiagnosticQueue diagnostics) {
        CheckForUnmatchedOperator(
            diagnostics,
            WellKnownMemberNames.EqualityOperatorName,
            WellKnownMemberNames.InequalityOperatorName
        );

        CheckForUnmatchedOperator(
            diagnostics,
            WellKnownMemberNames.LessThanOperatorName,
            WellKnownMemberNames.GreaterThanOperatorName
        );

        CheckForUnmatchedOperator(
            diagnostics,
            WellKnownMemberNames.LessThanOrEqualOperatorName,
            WellKnownMemberNames.GreaterThanOrEqualOperatorName
        );

        CheckForUnmatchedOperator(
            diagnostics,
            WellKnownMemberNames.SlashBackslashOperatorName,
            WellKnownMemberNames.BackslashSlashOperatorName
        );

        CheckForEqualityAndGetHashCode(diagnostics);
    }

    private void CheckForUnmatchedOperator(
        BelteDiagnosticQueue diagnostics,
        string operatorName1,
        string operatorName2) {
        var ops1 = GetOperators(operatorName1);
        var ops2 = GetOperators(operatorName2);
        CheckForUnmatchedOperator(diagnostics, ops1, ops2, operatorName2, ReportOperatorNeedsMatch);
        CheckForUnmatchedOperator(diagnostics, ops2, ops1, operatorName1, ReportOperatorNeedsMatch);

        static void ReportOperatorNeedsMatch(BelteDiagnosticQueue diagnostics, string operatorName2, MethodSymbol op1) {
            diagnostics.Push(Error.OperatorNeedsMatch(
                op1.location,
                op1,
                SyntaxFacts.GetText(SyntaxFacts.GetOperatorKind(operatorName2))
            ));
        }
    }

    private static void CheckForUnmatchedOperator(
        BelteDiagnosticQueue diagnostics,
        ImmutableArray<MethodSymbol> ops1,
        ImmutableArray<MethodSymbol> ops2,
        string operatorName2,
        Action<BelteDiagnosticQueue, string, MethodSymbol> reportMatchNotFoundError) {
        foreach (var op1 in ops1) {
            var foundMatch = false;

            foreach (var op2 in ops2) {
                foundMatch = DoOperatorsPair(op1, op2);

                if (foundMatch)
                    break;
            }

            if (!foundMatch)
                reportMatchNotFoundError(diagnostics, operatorName2, op1);
        }
    }

    internal static bool DoOperatorsPair(MethodSymbol op1, MethodSymbol op2) {
        if (op1.parameterCount != op2.parameterCount)
            return false;

        for (var p = 0; p < op1.parameterCount; ++p) {
            if (!op1.parameterTypesWithAnnotations[p]
                .Equals(op2.parameterTypesWithAnnotations[p], TypeCompareKind.AllIgnoreOptions)) {
                return false;
            }
        }

        if (!op1.returnType.Equals(op2.returnType, TypeCompareKind.AllIgnoreOptions))
            return false;

        return true;
    }

    private void CheckForEqualityAndGetHashCode(BelteDiagnosticQueue diagnostics) {
        if (this.IsInterfaceType())
            return;

        var hasOp = GetOperators(WellKnownMemberNames.EqualityOperatorName).Any() ||
            GetOperators(WellKnownMemberNames.InequalityOperatorName).Any();

        var overridesEquals = TypeOverridesObjectMethod("Equals");

        if (hasOp || overridesEquals) {
            var overridesGHC = TypeOverridesObjectMethod("GetHashCode");

            if (overridesEquals && !overridesGHC)
                diagnostics.Push(Warning.EqualsWithoutGetHashCode(location, this));

            if (hasOp && !overridesEquals)
                diagnostics.Push(Warning.EqualityOpWithoutEquals(location, this));

            if (hasOp && !overridesGHC)
                diagnostics.Push(Warning.EqualityOpWithoutGetHashCode(location, this));
        }
    }

    private void CheckMembersAgainstBaseType(BelteDiagnosticQueue diagnostics) {
        if (baseType?.IsErrorType() == true)
            return;

        switch (typeKind) {
            case TypeKind.Enum:
                return;
            case TypeKind.Class:
            case TypeKind.Struct:
            case TypeKind.Interface:
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(typeKind);
        }

        foreach (var member in GetMembersUnordered()) {
            switch (member.kind) {
                case SymbolKind.Method:
                    var method = (MethodSymbol)member;

                    if (MethodSymbol.CanOverrideOrHide(method.methodKind)) {
                        if (member.isOverride) {
                            CheckOverrideMember(method, method.overriddenOrHiddenMembers, diagnostics);
                        } else if (method is SourceMemberMethodSymbol sourceMethod) {
                            var isNew = sourceMethod.isNew;
                            CheckNonOverrideMember(method, isNew, method.overriddenOrHiddenMembers, diagnostics);
                        }
                    } else if (method.methodKind == MethodKind.Finalizer) {
                        // TODO Do we care about this error
                        // MethodSymbol overridden = method.GetFirstRuntimeOverriddenMethodIgnoringNewSlot(out _);

                        // if (overridden is not null && overridden.isMetadataFinal) {
                        //     diagnostics.Add(ErrorCode.ERR_CantOverrideSealed, method.GetFirstLocation(), method, overridden);
                        // }
                    }

                    break;
                case SymbolKind.Field:
                    var isNewField = member is SourceFieldSymbol sourceField && sourceField.isNew;
                    CheckNewModifier(member, isNewField, diagnostics);
                    break;
                case SymbolKind.NamedType:
                    // TODO 'new' types
                    // CheckNewModifier(member, ((SourceMemberContainerTypeSymbol)member).isNew, diagnostics);
                    break;
            }
        }
    }

    internal static bool IsOrContainsErrorType(TypeSymbol typeSymbol) {
        return typeSymbol.VisitType((currentTypeSymbol, unused1, unused2)
            => currentTypeSymbol.IsErrorType(), (object)null) is not null;
    }

    private void CheckOverrideMember(
        Symbol overridingMember,
        OverriddenOrHiddenMembersResult overriddenOrHiddenMembers,
        BelteDiagnosticQueue diagnostics) {
        var overridingMemberIsMethod = overridingMember.kind == SymbolKind.Method;
        var overridingMemberLocation = overridingMember.location;
        var overriddenMembers = overriddenOrHiddenMembers.overriddenMembers;

        if (overriddenMembers.Length == 0) {
            var hiddenMembers = overriddenOrHiddenMembers.hiddenMembers;

            if (hiddenMembers.Any()) {
                if (overridingMemberIsMethod) {
                    diagnostics.Push(
                        Error.CantOverrideNonMethod(overridingMemberLocation, overridingMember, hiddenMembers[0])
                    );
                }
            } else {
                var suppressError = false;

                if (overridingMemberIsMethod) {
                    var parameterTypes = ((MethodSymbol)overridingMember).parameterTypesWithAnnotations;

                    foreach (var parameterType in parameterTypes) {
                        if (IsOrContainsErrorType(parameterType.type)) {
                            suppressError = true;
                            break;
                        }
                    }
                }

                if (!suppressError)
                    diagnostics.Push(Error.OverrideNotExpected(overridingMemberLocation, overridingMember));
            }
        } else {
            var overridingType = overridingMember.containingType;

            if (overriddenMembers.Length > 1) {
                diagnostics.Push(Error.AmbiguousOverride(
                    overridingMemberLocation,
                    overriddenMembers[0].originalDefinition,
                    overriddenMembers[1].originalDefinition,
                    overridingType
                ));
            } else {
                CheckSingleOverriddenMember(overridingMember, overriddenMembers[0], diagnostics);
            }
        }

        // TODO Unncessary?
        // if (!this.ContainingAssembly.RuntimeSupportsCovariantReturnsOfClasses && overridingMember is MethodSymbol overridingMethod) {
        //     overridingMethod.RequiresExplicitOverride(out bool warnAmbiguous);
        //     if (warnAmbiguous) {
        //         var ambiguousMethod = overridingMethod.OverriddenMethod;
        //         diagnostics.Add(ErrorCode.WRN_MultipleRuntimeOverrideMatches, ambiguousMethod.GetFirstLocation(), ambiguousMethod, overridingMember);
        //         suppressAccessors = true;
        //     }
        // }

        return;

        void CheckSingleOverriddenMember(
            Symbol overridingMember,
            Symbol overriddenMember,
            BelteDiagnosticQueue diagnostics) {
            var overridingMemberLocation = overridingMember.location;
            var overridingMemberIsMethod = overridingMember.kind == SymbolKind.Method;
            var overridingType = overridingMember.containingType;

            if (!overriddenMember.isVirtual && !overriddenMember.isAbstract && !overriddenMember.isOverride) {
                diagnostics.Push(
                    Error.CantOverrideNonVirtual(overridingMemberLocation, overridingMember, overriddenMember)
                );
            } else if (overriddenMember.isSealed) {
                diagnostics.Push(
                    Error.CantOverrideSealed(overridingMemberLocation, overridingMember, overriddenMember)
                );
            } else if (!OverrideHasCorrectAccessibility(overriddenMember, overridingMember)) {
                var accessibility = SyntaxFacts.GetText(overriddenMember.declaredAccessibility);
                diagnostics.Push(Error.CantChangeAccessOnOverride(
                    overridingMemberLocation,
                    overridingMember,
                    accessibility,
                    overriddenMember
                ));
            } else {
                var leastOverriddenMember = overriddenMember.GetLeastOverriddenMember(overriddenMember.containingType);

                var overridingMethod = (MethodSymbol)overridingMember;
                var overriddenMethod = (MethodSymbol)overriddenMember;

                if (overridingMethod.isTemplateMethod) {
                    overriddenMethod = overriddenMethod.Construct(
                        TemplateMap.TemplateParametersAsTypeOrConstants(overridingMethod.templateParameters)
                    );
                }

                if (overridingMethod.isSealed && overridingType.isSealed)
                    diagnostics.Push(Warning.SealedInSealed(overridingMemberLocation, overridingMember));

                if (overridingMethod.refKind != overriddenMethod.refKind) {
                    diagnostics.Push(Error.CantChangeRefReturnOnOverride(
                        overridingMemberLocation,
                        overridingMember,
                        overriddenMember
                    ));
                } else if (!IsValidOverrideReturnType(
                    overridingMethod,
                    overridingMethod.returnTypeWithAnnotations,
                    overriddenMethod.returnTypeWithAnnotations,
                    diagnostics)) {
                    if (!IsOrContainsErrorType(overridingMethod.returnType)) {
                        diagnostics.Push(Error.CantChangeReturnTypeOnOverride(
                            overridingMemberLocation,
                            overridingMember,
                            overriddenMember,
                            overriddenMethod.returnType
                        ));
                    }
                } else {
                    CheckValidMethodOverride(
                        overridingMemberLocation,
                        overriddenMethod,
                        overridingMethod,
                        diagnostics
                    );
                }
            }
        }

        static void CheckValidMethodOverride(
            TextLocation overridingMemberLocation,
            MethodSymbol overriddenMethod,
            MethodSymbol overridingMethod,
            BelteDiagnosticQueue diagnostics) {
            if (RequiresValidScopedOverrideForRefSafety(overriddenMethod)) {
                // TODO Do we need this?
                // CheckValidScopedOverride(
                //     overriddenMethod,
                //     overridingMethod,
                //     diagnostics,
                //     static (diagnostics, overriddenMethod, overridingMethod, overridingParameter, _, location) => {
                //         diagnostics.Add(
                //                 ReportInvalidScopedOverrideAsError(overriddenMethod, overridingMethod) ?
                //                     ErrorCode.ERR_ScopedMismatchInParameterOfOverrideOrImplementation :
                //                     ErrorCode.WRN_ScopedMismatchInParameterOfOverrideOrImplementation,
                //                 location,
                //                 new FormattedSymbol(overridingParameter, SymbolDisplayFormat.ShortFormat));
                //     },
                //     overridingMemberLocation,
                //     allowVariance: true,
                //     invokedAsExtensionMethod: false);
            }

            CheckValidNullableMethodOverride(
                overridingMethod.declaringCompilation,
                overriddenMethod,
                overridingMethod,
                diagnostics,
                ReportBadReturn,
                ReportBadParameter,
                overridingMemberLocation
            );

            CheckRefReadonlyInMismatch(
                overriddenMethod, overridingMethod, diagnostics,
                static (diagnostics, _, _, overridingParameter, _, arg) => {
                    var (overriddenParameter, location) = arg;
                    diagnostics.Push(
                        Warning.OverridingDifferentRefness(location, overridingParameter, overriddenParameter)
                    );
                },
                overridingMemberLocation,
                invokedAsExtensionMethod: false
            );

            CheckConstMismatch(overridingMemberLocation, overriddenMethod, overridingMethod, diagnostics);
        }
    }

    private static void CheckConstMismatch(
        TextLocation overridingMemberLocation,
        MethodSymbol overriddenMethod,
        MethodSymbol overridingMethod,
        BelteDiagnosticQueue diagnostics) {
        if (overriddenMethod.isDeclaredConst != overridingMethod.isDeclaredConst) {
            if (overriddenMethod.isDeclaredConst) {
                diagnostics.Push(Error.CantChangeConstOnOverride(
                    overridingMemberLocation,
                    overridingMethod,
                    overriddenMethod
                ));
            } else {
                diagnostics.Push(Warning.DifferentConstOnOverride(
                    overridingMemberLocation,
                    overridingMethod,
                    overriddenMethod
                ));
            }
        }

        for (var i = 0; i < overridingMethod.parameterCount; i++) {
            var overriddenParameter = overriddenMethod.parameters[i];
            var overridingParameter = overridingMethod.parameters[i];

            if (overriddenParameter.isConst != overridingParameter.isConst) {
                if (overriddenParameter.isConst) {
                    diagnostics.Push(Error.CantChangeConstOnOverrideParameter(
                        overridingParameter.location,
                        overridingMethod,
                        overriddenMethod,
                        overridingParameter.name
                    ));
                } else {
                    diagnostics.Push(Warning.DifferentConstOnOverrideParameter(
                        overridingParameter.location,
                        overridingMethod,
                        overriddenMethod,
                        overridingParameter.name
                    ));
                }
            }
        }
    }

    internal static bool CheckValidNullableMethodOverride<TArg>(
        Compilation compilation,
        MethodSymbol baseMethod,
        MethodSymbol overrideMethod,
        BelteDiagnosticQueue diagnostics,
        ReportMismatchInReturnType<TArg> reportMismatchInReturnType,
        ReportMismatchInParameterType<TArg> reportMismatchInParameterType,
        TArg extraArgument,
        bool invokedAsExtensionMethod = false) {
        if (!PerformValidNullableOverrideCheck(compilation, baseMethod, overrideMethod)) {
            return false;
        }

        var hasErrors = false;

        // TODO Flow analysis like this would be nice to add
        // if ((baseMethod.FlowAnalysisAnnotations & FlowAnalysisAnnotations.DoesNotReturn) == FlowAnalysisAnnotations.DoesNotReturn &&
        //     (overrideMethod.FlowAnalysisAnnotations & FlowAnalysisAnnotations.DoesNotReturn) != FlowAnalysisAnnotations.DoesNotReturn) {
        //     diagnostics.Add(ErrorCode.WRN_DoesNotReturnMismatch, overrideMethod.GetFirstLocation(), new FormattedSymbol(overrideMethod, SymbolDisplayFormat.MinimallyQualifiedFormat));
        //     hasErrors = true;
        // }

        var baseParameters = baseMethod.parameters;
        var overrideParameters = overrideMethod.parameters;
        var overrideParameterOffset = invokedAsExtensionMethod ? 1 : 0;

        if (reportMismatchInReturnType != null) {
            var overrideReturnType = overrideMethod.returnTypeWithAnnotations;

            if (!IsValidNullableConversion(
                overrideMethod.refKind,
                overrideReturnType.type,
                baseMethod.returnTypeWithAnnotations.type)) {
                reportMismatchInReturnType(diagnostics, baseMethod, overrideMethod, false, extraArgument);
                return true;
            }

            // TODO NullableWalker
            // if (!NullableWalker.AreParameterAnnotationsCompatible(
            //         overrideMethod.RefKind == RefKind.Ref ? RefKind.Ref : RefKind.Out,
            //         baseMethod.ReturnTypeWithAnnotations,
            //         baseMethod.ReturnTypeFlowAnalysisAnnotations,
            //         overrideReturnType,
            //         overrideMethod.ReturnTypeFlowAnalysisAnnotations)) {
            //     reportMismatchInReturnType(diagnostics, baseMethod, overrideMethod, true, extraArgument);
            //     return true;
            // }
        }

        if (reportMismatchInParameterType is not null) {
            for (var i = 0; i < baseParameters.Length; i++) {
                var baseParameter = baseParameters[i];
                var baseParameterType = baseParameter.typeWithAnnotations;
                var parameterIndex = i + overrideParameterOffset;
                var overrideParameter = overrideParameters[parameterIndex];
                var overrideParameterType = overrideParameter.typeWithAnnotations;

                if (!IsValidNullableConversion(
                    overrideParameter.refKind,
                    baseParameterType.type,
                    overrideParameterType.type)) {
                    reportMismatchInParameterType(
                        diagnostics,
                        baseMethod,
                        overrideMethod,
                        overrideParameter,
                        false,
                        extraArgument
                    );

                    hasErrors = true;
                }
                // TODO NullableWalker
                // check top-level nullability including flow analysis annotations
                // else if (!NullableWalker.AreParameterAnnotationsCompatible(
                //         overrideParameter.RefKind,
                //         baseParameterType,
                //         baseParameter.FlowAnalysisAnnotations,
                //         overrideParameterType,
                //         overrideParameter.FlowAnalysisAnnotations)) {
                //     reportMismatchInParameterType(diagnostics, baseMethod, overrideMethod, overrideParameter, true, extraArgument);
                //     hasErrors = true;
                // }
            }
        }

        return hasErrors;

        static bool IsValidNullableConversion(
            RefKind refKind,
            TypeSymbol sourceType,
            TypeSymbol targetType) {
            switch (refKind) {
                case RefKind.Ref:
                    return sourceType.Equals(targetType, TypeCompareKind.AllIgnoreOptions);
                default:
                    break;
            }

            return true;
            // TODO Connect conversions to compilation
            // return conversions.ClassifyImplicitConversionFromType(sourceType, targetType, ref discardedUseSiteInfo).Kind != ConversionKind.NoConversion;
        }
    }
    private static bool PerformValidNullableOverrideCheck(
        Compilation compilation,
        Symbol overriddenMember,
        Symbol overridingMember) {
        return overriddenMember is not null &&
               overridingMember is not null &&
               compilation is not null;
    }

    internal static bool CheckValidScopedOverride<TArg>(
        MethodSymbol baseMethod,
        MethodSymbol overrideMethod,
        BelteDiagnosticQueue diagnostics,
        ReportMismatchInParameterType<TArg> reportMismatchInParameterType,
        TArg extraArgument,
        bool allowVariance,
        bool invokedAsExtensionMethod) {
        if (baseMethod is null || overrideMethod is null)
            return false;

        var hasErrors = false;
        var baseParameters = baseMethod.parameters;
        var overrideParameters = overrideMethod.parameters;
        var overrideParameterOffset = invokedAsExtensionMethod ? 1 : 0;

        for (var i = 0; i < baseParameters.Length; i++) {
            var baseParameter = baseParameters[i];
            var overrideParameter = overrideParameters[i + overrideParameterOffset];

            if (!IsValidScopedConversion(
                allowVariance,
                baseParameter.effectiveScope,
                baseParameter.hasUnscopedRefAttribute,
                overrideParameter.effectiveScope,
                overrideParameter.hasUnscopedRefAttribute)) {
                reportMismatchInParameterType(
                    diagnostics,
                    baseMethod,
                    overrideMethod,
                    overrideParameter,
                    topLevel: true,
                    extraArgument
                );

                hasErrors = true;
            }
        }
        return hasErrors;

        static bool IsValidScopedConversion(
            bool allowVariance,
            ScopedKind baseScope,
            bool baseHasUnscopedRefAttribute,
            ScopedKind overrideScope,
            bool overrideHasUnscopedRefAttribute) {
            if (baseScope == overrideScope) {
                if (baseHasUnscopedRefAttribute == overrideHasUnscopedRefAttribute)
                    return true;

                return allowVariance && !overrideHasUnscopedRefAttribute;
            }

            return allowVariance && baseScope == ScopedKind.None;
        }
    }

    internal static bool RequiresValidScopedOverrideForRefSafety(MethodSymbol method) {
        if (method is null)
            return false;

        var parameters = method.parameters;

        if (parameters.Any(static p =>
                p is { effectiveScope: ScopedKind.None, refKind: RefKind.Ref } &&
                p.type.IsRefLikeOrAllowsRefLikeType())) {
            return true;
        }

        int nRefParametersRequired;

        if (method.returnType.IsRefLikeOrAllowsRefLikeType() ||
            (method.refKind is RefKind.Ref or RefKind.RefConst or RefKind.RefFinal)) {
            nRefParametersRequired = 1;
        } else if (parameters.Any(p => (p.refKind is RefKind.Ref) && p.type.IsRefLikeOrAllowsRefLikeType())) {
            nRefParametersRequired = 2;
        } else {
            return false;
        }

        var nRefParameters = parameters.Count(p => p.refKind is RefKind.Ref or RefKind.RefConst or RefKind.RefFinal);

        if (nRefParameters >= nRefParametersRequired)
            return true;
        else if (parameters.Any(p => p.refKind == RefKind.None && p.type.IsRefLikeOrAllowsRefLikeType()))
            return true;

        return false;
    }

    private bool IsValidOverrideReturnType(
        Symbol overridingSymbol,
        TypeWithAnnotations overridingReturnType,
        TypeWithAnnotations overriddenReturnType,
        BelteDiagnosticQueue diagnostics) {
        return overridingReturnType.Equals(overriddenReturnType, TypeCompareKind.AllIgnoreOptions);
    }

    private static bool OverrideHasCorrectAccessibility(Symbol overridden, Symbol overriding) {
        return overridden.declaredAccessibility == overriding.declaredAccessibility;
    }

    private void CheckAbstractClassImplementations(BelteDiagnosticQueue diagnostics) {
        var baseType = this.baseType;

        if (isAbstract || baseType is null || !baseType.isAbstract)
            return;

        foreach (var abstractMember in abstractMembers) {
            if (abstractMember.kind == SymbolKind.Method)
                diagnostics.Push(Error.TypeDoesNotImplementAbstract(location, this, abstractMember));
        }
    }

    private static void CheckNonOverrideMember(
        Symbol hidingMember,
        bool hidingMemberIsNew,
        OverriddenOrHiddenMembersResult overriddenOrHiddenMembers,
        BelteDiagnosticQueue diagnostics) {
        var hidingMemberLocation = hidingMember.location;
        var hiddenMembers = overriddenOrHiddenMembers.hiddenMembers;

        if (hiddenMembers.Length == 0) {
            if (hidingMemberIsNew)
                diagnostics.Push(Warning.NewNotRequired(hidingMemberLocation, hidingMember));
        } else {
            var diagnosticAdded = false;

            foreach (var hiddenMember in hiddenMembers) {
                diagnosticAdded |= AddHidingAbstractDiagnostic(
                    hidingMember,
                    hidingMemberLocation,
                    hiddenMember,
                    diagnostics
                );

                if (!hidingMemberIsNew && hiddenMember.kind == hidingMember.kind &&
                    (hiddenMember.isAbstract || hiddenMember.isVirtual || hiddenMember.isOverride)) {
                    diagnostics.Push(Warning.NewOrOverrideExpected(hidingMemberLocation, hidingMember, hiddenMember));
                    diagnosticAdded = true;
                }

                if (diagnosticAdded)
                    break;
            }

            if (!hidingMemberIsNew && !diagnosticAdded && !hidingMember.IsOperator())
                diagnostics.Push(Warning.NewRequired(hidingMemberLocation, hidingMember, hiddenMembers[0]));

            if (hidingMember is MethodSymbol hidingMethod && hiddenMembers[0] is MethodSymbol hiddenMethod) {
                CheckRefReadonlyInMismatch(
                    hiddenMethod, hidingMethod, diagnostics,
                    static (diagnostics, _, _, hidingParameter, _, arg) => {
                        var (hiddenParameter, location) = arg;
                        diagnostics.Push(Warning.HidingDifferentRefness(location, hidingParameter, hiddenParameter));
                    },
                    hidingMemberLocation,
                    invokedAsExtensionMethod: false);
            }
        }
    }

    internal static void CheckRefReadonlyInMismatch<TArg>(
        MethodSymbol baseMethod,
        MethodSymbol overrideMethod,
        BelteDiagnosticQueue diagnostics,
        ReportMismatchInParameterType<(ParameterSymbol, TArg)> reportMismatchInParameterType,
        TArg extraArgument,
        bool invokedAsExtensionMethod) {
        if (baseMethod is null || overrideMethod is null)
            return;

        var baseParameters = baseMethod.parameters;
        var overrideParameters = overrideMethod.parameters;
        var overrideParameterOffset = invokedAsExtensionMethod ? 1 : 0;

        for (var i = 0; i < baseParameters.Length; i++) {
            var baseParameter = baseParameters[i];
            var overrideParameter = overrideParameters[i + overrideParameterOffset];

            if (baseParameter.refKind != overrideParameter.refKind) {
                reportMismatchInParameterType(
                    diagnostics,
                    baseMethod,
                    overrideMethod,
                    overrideParameter,
                    topLevel: true,
                    (baseParameter, extraArgument)
                );
            }
        }
    }

    private void CheckNewModifier(Symbol symbol, bool isNew, BelteDiagnosticQueue diagnostics) {
        if (symbol.isImplicitlyDeclared)
            return;

        if (baseType is null)
            return;

        var symbolArity = symbol.GetMemberArity();
        var symbolLocation = symbol.location;

        var currType = baseType;
        while (currType is not null) {
            foreach (var hiddenMember in currType.GetMembers(symbol.name)) {
                if (hiddenMember.kind == SymbolKind.Method &&
                    !((MethodSymbol)hiddenMember).CanBeHiddenByMemberKind(symbol.kind)) {
                    continue;
                }

                var isAccessible = AccessCheck.IsSymbolAccessible(hiddenMember, this);

                if (isAccessible && hiddenMember.GetMemberArity() == symbolArity) {
                    if (!isNew)
                        diagnostics.Push(Warning.NewRequired(symbolLocation, symbol, hiddenMember));

                    AddHidingAbstractDiagnostic(symbol, symbolLocation, hiddenMember, diagnostics);
                    return;
                }
            }

            currType = currType.baseType;
        }

        if (isNew)
            diagnostics.Push(Warning.NewNotRequired(symbolLocation, symbol));
    }

    private static bool AddHidingAbstractDiagnostic(
        Symbol hidingMember,
        TextLocation hidingMemberLocation,
        Symbol hiddenMember,
        BelteDiagnosticQueue diagnostics) {
        switch (hiddenMember.kind) {
            case SymbolKind.Method:
                break;
            default:
                return false;
        }

        if (!hiddenMember.isAbstract || !hidingMember.containingType.isAbstract)
            return false;

        switch (hidingMember.declaredAccessibility) {
            case Accessibility.Private:
                break;
            case Accessibility.Public:
            case Accessibility.Protected:
                diagnostics.Push(Error.HidingAbstractMember(hidingMemberLocation, hidingMember, hiddenMember));
                return true;
            default:
                throw ExceptionUtilities.UnexpectedValue(hidingMember.declaredAccessibility);
        }

        return false;
    }

    private bool TypeOverridesObjectMethod(string name) {
        foreach (var method in GetMembers(name).OfType<MethodSymbol>()) {
            if (method.isOverride && method.GetConstructedLeastOverriddenMethod(
                    this, requireSameReturnType: false)
                        .containingType.specialType == SpecialType.Object) {
                return true;
            }
        }

        return false;
    }

    private void CheckForProtectedInStaticClass(BelteDiagnosticQueue diagnostics) {
        if (!isStatic)
            return;

        foreach (var valuesByName in GetMembersByName().Values) {
            foreach (var member in valuesByName) {
                if (member.declaredAccessibility.HasProtected()) {
                    if (member.kind != SymbolKind.Method ||
                        ((MethodSymbol)member).methodKind is not MethodKind.Destructor and not MethodKind.Finalizer) {
                        diagnostics.Push(Error.ProtectedInStatic(member.location, member));
                    }
                }
            }
        }
    }

    private void CheckTemplateParameterNameConflicts(BelteDiagnosticQueue diagnostics) {
        foreach (var tp in templateParameters) {
            foreach (var dup in GetMembers(tp.name))
                diagnostics.Push(Error.DuplicateNameInClass(dup.location, this, tp.name));
        }
    }

    private void CheckSpecialMemberErrors(BelteDiagnosticQueue diagnostics) {
        foreach (var member in GetMembersUnordered())
            member.AfterAddingTypeMembersChecks(TypeConversions.GetInstance(), diagnostics);
    }

    private void CheckMemberNameConflicts(BelteDiagnosticQueue diagnostics) {
        var membersByName = GetMembersByName();

        var methodsBySignature = new Dictionary<SourceMemberMethodSymbol, SourceMemberMethodSymbol>(
            MemberSignatureComparer.DuplicateSourceComparer
        );

        var conversionsAsMethods = new Dictionary<SourceMemberMethodSymbol, SourceMemberMethodSymbol>(
            MemberSignatureComparer.DuplicateSourceComparer
        );

        var conversionsAsConversions = new HashSet<SourceUserDefinedConversionSymbol>(
            ConversionSignatureComparer.Comparer
        );

        foreach (var pair in membersByName) {
            var name = pair.Key;
            Symbol lastSym = GetTypeMembers(name).FirstOrDefault();
            methodsBySignature.Clear();

            foreach (var symbol in pair.Value) {
                if (symbol.kind == SymbolKind.NamedType)
                    continue;

                if (lastSym is not null) {
                    if (symbol.kind != SymbolKind.Method || lastSym.kind != SymbolKind.Method) {
                        if (symbol.kind != SymbolKind.Field || !symbol.isImplicitlyDeclared)
                            diagnostics.Push(Error.DuplicateNameInClass(symbol.location, this, symbol.name));

                        if (lastSym.kind == SymbolKind.Method)
                            lastSym = symbol;
                    }
                } else {
                    lastSym = symbol;
                }

                var conversion = symbol as SourceUserDefinedConversionSymbol;
                var method = symbol as SourceMemberMethodSymbol;

                if (conversion is { methodKind: MethodKind.Conversion }) {
                    if (!conversionsAsConversions.Add(conversion)) {
                        diagnostics.Push(Error.DuplicateConversion(conversion.location, this));
                    } else {
                        if (!conversionsAsMethods.ContainsKey(conversion))
                            conversionsAsMethods.Add(conversion, conversion);
                    }

                    if (methodsBySignature.TryGetValue(conversion, out var previousMethod))
                        ReportMethodSignatureCollision(diagnostics, conversion, previousMethod);
                } else if (method is not null) {
                    if (methodsBySignature.TryGetValue(method, out var previousMethod))
                        ReportMethodSignatureCollision(diagnostics, method, previousMethod);
                    else
                        methodsBySignature.Add(method, method);
                }
            }
        }
    }

    private void ReportMethodSignatureCollision(
        BelteDiagnosticQueue diagnostics,
        SourceMemberMethodSymbol method1,
        SourceMemberMethodSymbol method2) {
        switch (method1, method2) {
            case (SynthesizedEntryPoint { }, SynthesizedEntryPoint { }):
                return;
        }

        for (var i = 0; i < method1.parameterCount; i++) {
            var refKind1 = method1.parameters[i].refKind;
            var refKind2 = method2.parameters[i].refKind;

            if (refKind1 != refKind2) {
                var methodKind = method1.methodKind == MethodKind.Constructor
                    ? MessageID.IDS_SK_CONSTRUCTOR
                    : MessageID.IDS_SK_METHOD;

                diagnostics.Push(Error.OverloadRefKind(
                    method1.location,
                    this,
                    methodKind.Localize(),
                    refKind1.ToParameterDisplayString(),
                    refKind2.ToParameterDisplayString()
                ));

                return;
            }
        }

        if (method1.methodKind == MethodKind.Constructor)
            diagnostics.Push(Error.ConstructorAlreadyExists(method1.location, this));
        else
            diagnostics.Push(Error.MemberAlreadyExists(method1.location, this, method1.name));
    }

    private static void CheckForStructDefaultConstructors(
        ArrayBuilder<Symbol> members,
        BelteDiagnosticQueue diagnostics) {
        foreach (var s in members) {
            if (s is MethodSymbol m) {
                if (m.methodKind == MethodKind.Constructor && m.parameterCount == 0) {
                    if (m.declaredAccessibility != Accessibility.Public)
                        diagnostics.Push(Error.NonPublicParameterlessStructConstructor(m.location));
                }
            }
        }
    }

    private void CheckMemberNamesDistinctFromType(BelteDiagnosticQueue diagnostics) {
        foreach (var member in GetMembersAndInitializers().nonTypeMembers)
            CheckMemberNameDistinctFromType(member, diagnostics);
    }

    private protected Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> GetMembersByName() {
        if (_state.HasComplete(CompletionParts.Members))
            return _lazyMembersDictionary;

        return GetMembersByNameSlow();
    }

    private protected MembersAndInitializers GetMembersAndInitializers() {
        var membersAndInitializers = _lazyMembersAndInitializers;

        if (membersAndInitializers is not null)
            return membersAndInitializers;

        var diagnostics = BelteDiagnosticQueue.GetInstance();
        membersAndInitializers = BuildMembersAndInitializers(diagnostics);

        var alreadyKnown = Interlocked.CompareExchange(ref _lazyMembersAndInitializers, membersAndInitializers, null);

        if (alreadyKnown is not null)
            return alreadyKnown;

        AddDeclarationDiagnostics(diagnostics);
        diagnostics.Free();

        _lazyDeclaredMembersAndInitializers = null;
        return membersAndInitializers;
    }

    private void EnsureFieldDefinitionsNoted() {
        if (_fieldDefinitionsNoted)
            return;

        NoteFieldDefinitions();
    }

    private void NoteFieldDefinitions() {
        var membersAndInitializers = GetMembersAndInitializers();

        lock (membersAndInitializers) {
            if (!_fieldDefinitionsNoted) {
                // TODO Implement this to support unused fields warnings
                /*
                var containerEffectiveAccessibility = EffectiveAccessibility();

                foreach (var member in membersAndInitializers.nonTypeMembers) {
                    if (member is not FieldSymbol field || field.IsConstExpr)
                        continue;

                    var fieldDeclaredAccessibility = field.accessibility;

                    if (fieldDeclaredAccessibility == Accessibility.Private)
                        declaringCompilation.NoteFieldDefinition(field, isUnread: true);
                    else if (containerEffectiveAccessibility == Accessibility.Private)
                        declaringCompilation.NoteFieldDefinition(field, isUnread: false);
                }
                */
                _fieldDefinitionsNoted = true;
            }
        }
    }

    private Accessibility EffectiveAccessibility() {
        var result = declaredAccessibility;

        if (result == Accessibility.Private)
            return Accessibility.Private;

        for (var container = containingType; container is not null; container = container.containingType) {
            if (container.declaredAccessibility == Accessibility.Private)
                return Accessibility.Private;
        }

        return result;
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> GetMembersByNameSlow() {
        if (_lazyMembersDictionary is null) {
            var membersDictionary = MakeAllMembers();

            if (Interlocked.CompareExchange(ref _lazyMembersDictionary, membersDictionary, null) is null)
                _state.NotePartComplete(CompletionParts.Members);
        }

        _state.SpinWaitComplete(CompletionParts.Members);
        return _lazyMembersDictionary;
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> MakeAllMembers() {
        var membersAndInitializers = GetMembersAndInitializers();
        var membersByName = ToNameKeyedDictionary(membersAndInitializers.nonTypeMembers);
        AddNestedTypesToDictionary(membersByName, GetTypeMembersDictionary());
        return membersByName;
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> GetTypeMembersDictionary() {
        if (_lazyTypeMembers is null) {
            var diagnostics = BelteDiagnosticQueue.GetInstance();

            if (Interlocked.CompareExchange(ref _lazyTypeMembers, MakeTypeMembers(diagnostics), null) is null) {
                AddDeclarationDiagnostics(diagnostics);
                _state.NotePartComplete(CompletionParts.TypeMembers);
            }

            diagnostics.Free();
        }

        return _lazyTypeMembers;
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> MakeTypeMembers(
        BelteDiagnosticQueue diagnostics) {
        var symbols = ArrayBuilder<NamedTypeSymbol>.GetInstance();
        var conflicts = new Dictionary<(string name, int arity, SyntaxTree syntaxTree), SourceNamedTypeSymbol>();

        try {
            foreach (var childDeclaration in _declaration.children) {
                var t = new SourceNamedTypeSymbol(this, childDeclaration, diagnostics);
                CheckMemberNameDistinctFromType(t, diagnostics);

                var key = (t.name, t.arity, t.syntaxReference.syntaxTree);

                if (conflicts.TryGetValue(key, out var other))
                    diagnostics.Push(Error.DuplicateNameInClass(t.syntaxReference.location, this, t.name));
                else
                    conflicts.Add(key, t);

                symbols.Add(t);
            }

            if (isInterface) {
                foreach (var t in symbols)
                    diagnostics.Push(Error.DefaultInterfaceImplementation(t.location));
            }

            return symbols.Count > 0
                ? symbols.ToDictionary(s => s.name.AsMemory(), ReadOnlyMemoryOfCharComparer.Instance)
                : EmptyTypeMembers;
        } finally {
            symbols.Free();
        }
    }

    private void CheckMemberNameDistinctFromType(Symbol member, BelteDiagnosticQueue diagnostics) {
        switch (typeKind) {
            case TypeKind.Class:
            case TypeKind.Struct:
                CheckContainingTypeName(member, name, diagnostics);
                break;
            case TypeKind.Interface:
                if (member.isStatic)
                    CheckContainingTypeName(member, name, diagnostics);

                break;
        }

        static void CheckContainingTypeName(Symbol member, string typeName, BelteDiagnosticQueue diagnostics) {
            if (member.name == typeName)
                diagnostics.Push(Error.MemberNameSameAsType(member.location, typeName));
        }
    }

    private MembersAndInitializers BuildMembersAndInitializers(BelteDiagnosticQueue diagnostics) {
        var declaredMembersAndInitializers = GetDeclaredMembersAndInitializers();

        if (declaredMembersAndInitializers is null)
            return null;

        var membersAndInitializersBuilder = new MembersAndInitializersBuilder();
        AddSynthesizedMembers(membersAndInitializersBuilder, declaredMembersAndInitializers);

        if (Volatile.Read(ref _lazyMembersAndInitializers) is not null) {
            membersAndInitializersBuilder.Free();
            return null;
        }

        return membersAndInitializersBuilder.ToReadOnlyAndFree(declaredMembersAndInitializers);

        DeclaredMembersAndInitializers GetDeclaredMembersAndInitializers() {
            var declaredMembersAndInitializers = _lazyDeclaredMembersAndInitializers;
            if (declaredMembersAndInitializers != DeclaredMembersAndInitializers.UninitializedSentinel)
                return declaredMembersAndInitializers;

            if (Volatile.Read(ref _lazyMembersAndInitializers) is not null)
                return null;

            declaredMembersAndInitializers = BuildDeclaredMembersAndInitializers();

            var alreadyKnown = Interlocked.CompareExchange(
                ref _lazyDeclaredMembersAndInitializers,
                declaredMembersAndInitializers,
                DeclaredMembersAndInitializers.UninitializedSentinel
            );

            if (alreadyKnown != DeclaredMembersAndInitializers.UninitializedSentinel)
                return alreadyKnown;

            AddDeclarationDiagnostics(diagnostics);
            return declaredMembersAndInitializers;
        }

        DeclaredMembersAndInitializers BuildDeclaredMembersAndInitializers() {
            var builder = new DeclaredMembersAndInitializersBuilder();
            AddDeclaredNonTypeMembers(builder, diagnostics);

            switch (typeKind) {
                case TypeKind.Struct:
                    // TODO, but pretty sure we just do nothing here, same for enum
                    // CheckForStructBadInitializers(builder, diagnostics);
                    CheckForStructDefaultConstructors(builder.nonTypeMembers, diagnostics);
                    break;
                default:
                    break;
            }

            if (Volatile.Read(ref _lazyDeclaredMembersAndInitializers) !=
                DeclaredMembersAndInitializers.UninitializedSentinel) {
                builder.Free();
                return null;
            }

            return builder.ToReadOnlyAndFree(declaringCompilation);
        }
    }

    private void AddDeclaredNonTypeMembers(
        DeclaredMembersAndInitializersBuilder builder,
        BelteDiagnosticQueue diagnostics) {
        if (_lazyMembersAndInitializers is not null)
            return;

        var syntax = syntaxReference.node;

        switch (syntax.kind) {
            case SyntaxKind.EnumDeclaration:
                var enumDeclaration = (EnumDeclarationSyntax)syntax;
                AddEnumMembers(builder, enumDeclaration, diagnostics);
                AddNonTypeMembers(builder, enumDeclaration.members, diagnostics);
                break;
            case SyntaxKind.CompilationUnit:
                AddNonTypeMembers(builder, ((CompilationUnitSyntax)syntax).elements, diagnostics);
                break;
            case SyntaxKind.NamespaceDeclaration:
            case SyntaxKind.FileScopedNamespaceDeclaration:
                AddNonTypeMembers(builder, ((BaseNamespaceDeclarationSyntax)syntax).elements, diagnostics);
                break;
            case SyntaxKind.InterfaceDeclaration:
                AddNonTypeMembers(builder, ((InterfaceDeclarationSyntax)syntax).members, diagnostics);
                break;
            case SyntaxKind.ClassDeclaration:
            case SyntaxKind.FileScopedClassDeclaration:
            case SyntaxKind.StructDeclaration:
            case SyntaxKind.UnionDeclaration:
                var typeDeclaration = (TypeDeclarationSyntax)syntax;
                NoteTypeParameters(typeDeclaration, builder);
                AddNonTypeMembers(builder, typeDeclaration.members, diagnostics);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(syntax.kind);
        }

        void NoteTypeParameters(TypeDeclarationSyntax syntax, DeclaredMembersAndInitializersBuilder builder) {
            var parameterList = syntax.templateParameterList;

            if (parameterList is null)
                return;

            if (builder.declarationWithParameters is null) {
                builder.declarationWithParameters = syntax;

                // TODO Do we want to err here
                // if (isStatic)
                //     diagnostics.Push(Error.ConstructorInStaticClass(syntax.identifier.location));
            }
        }
    }

    private void AddEnumMembers(
        DeclaredMembersAndInitializersBuilder result,
        EnumDeclarationSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        SourceEnumConstantSymbol otherSymbol = null;

        var otherSymbolOffset = 0;

        foreach (var member in syntax.members) {
            if (member is not EnumMemberDeclarationSyntax enumMember)
                break;

            SourceEnumConstantSymbol symbol;
            var valueOpt = enumMember.equalsValue;

            if (valueOpt is not null) {
                symbol = SourceEnumConstantSymbol.CreateExplicitValuedConstant(this, enumMember, diagnostics);
            } else {
                symbol = SourceEnumConstantSymbol.CreateImplicitValuedConstant(
                    this,
                    enumMember,
                    otherSymbol,
                    otherSymbolOffset,
                    enumFlagsAttribute,
                    diagnostics
                );
            }

            result.nonTypeMembers.Add(symbol);

            if (valueOpt is not null || otherSymbol is null) {
                otherSymbol = symbol;
                otherSymbolOffset = 1;
            } else {
                otherSymbolOffset++;
            }
        }
    }

    private void AddNonTypeMembers<TNode>(
        DeclaredMembersAndInitializersBuilder builder,
        SyntaxList<TNode> members,
        BelteDiagnosticQueue diagnostics)
        where TNode : NamespaceElementSyntax {
        if (members.Count == 0)
            return;

        var firstMember = members[0];
        var bodyBinder = GetBinder(firstMember);

        ArrayBuilder<FieldInitializer>? instanceInitializers = null;
        ArrayBuilder<FieldInitializer>? staticInitializers = null;

        foreach (var e in members) {
            var m = (NamespaceElementSyntax)e;

            if (_lazyMembersAndInitializers is not null)
                return;

            var reportMisplacedGlobalCode = !m.containsDiagnostics;

            switch (m.kind) {
                case SyntaxKind.UnionDeclaration: {
                        var unionSyntax = (UnionDeclarationSyntax)m;

                        if (unionSyntax.identifier is not null)
                            break;

                        if (unionSyntax.members.Count == 0) {
                            diagnostics.Push(Error.UnionMustHaveField(unionSyntax.keyword.location));
                            break;
                        }

                        var unionMembers = ArrayBuilder<SourceMemberFieldSymbol>.GetInstance();

                        foreach (var u in unionSyntax.members) {
                            unionMembers.Add(AddFieldMember(
                                (FieldDeclarationSyntax)u,
                                reportMisplacedGlobalCode && !u.containsDiagnostics
                            ));
                        }

                        SetAnonymousUnionType(unionMembers.ToImmutableAndFree());
                    }

                    break;
                case SyntaxKind.FieldDeclaration:
                    AddFieldMember((FieldDeclarationSyntax)m, reportMisplacedGlobalCode);
                    break;
                case SyntaxKind.ExternBlockDeclaration: {
                        var externSyntax = (ExternBlockDeclarationSyntax)m;

                        if (isImplicitClass && reportMisplacedGlobalCode)
                            diagnostics.Push(Error.NamespaceUnexpected(externSyntax.keyword.location));

                        AddScopeInheritorInfo(externSyntax, diagnostics);
                        AddNonTypeMembers(builder, externSyntax.members, diagnostics);
                    }

                    break;
                case SyntaxKind.MethodDeclaration: {
                        var methodSyntax = (MethodDeclarationSyntax)m;

                        if (isImplicitClass && reportMisplacedGlobalCode)
                            diagnostics.Push(Error.NamespaceUnexpected(methodSyntax.identifier.location));

                        var method = SourceOrdinaryMethodSymbol.CreateMethodSymbol(
                            this,
                            bodyBinder,
                            methodSyntax,
                            diagnostics
                        );

                        builder.nonTypeMembers.Add(method);

                        if (method.isReversible)
                            builder.nonTypeMembers.Add(method.reverseMethod);

                        if (method.hasReversalState)
                            builder.nonTypeMembers.Add(method.stateMethod);
                    }

                    break;
                case SyntaxKind.ConstructorDeclaration: {
                        var constructorSyntax = (ConstructorDeclarationSyntax)m;

                        if (isImplicitClass && reportMisplacedGlobalCode)
                            diagnostics.Push(Error.NamespaceUnexpected(constructorSyntax.constructorKeyword.location));

                        var constructor = SourceConstructorSymbol.CreateConstructorSymbol(
                            this,
                            constructorSyntax,
                            diagnostics
                        );

                        builder.nonTypeMembers.Add(constructor);
                    }

                    break;
                case SyntaxKind.DestructorDeclaration: {
                        var destructorSyntax = (DestructorDeclarationSyntax)m;

                        if (isImplicitClass && reportMisplacedGlobalCode)
                            diagnostics.Push(Error.NamespaceUnexpected(destructorSyntax.destructorKeyword.location));

                        var destructor = new SourceDestructorSymbol(this, destructorSyntax, diagnostics);
                        builder.nonTypeMembers.Add(destructor);
                    }

                    break;
                case SyntaxKind.FinalizerDeclaration: {
                        var finalizerSyntax = (FinalizerDeclarationSyntax)m;

                        if (isImplicitClass && reportMisplacedGlobalCode)
                            diagnostics.Push(Error.NamespaceUnexpected(finalizerSyntax.finalizerKeyword.location));

                        var finalizer = new SourceFinalizerSymbol(this, finalizerSyntax, diagnostics);
                        builder.nonTypeMembers.Add(finalizer);
                    }

                    break;
                case SyntaxKind.OperatorDeclaration: {
                        var operatorSyntax = (OperatorDeclarationSyntax)m;

                        if (isImplicitClass && reportMisplacedGlobalCode)
                            diagnostics.Push(Error.NamespaceUnexpected(operatorSyntax.operatorKeyword.location));

                        var method = SourceUserDefinedOperatorSymbol.CreateUserDefinedOperatorSymbol(
                            this,
                            bodyBinder,
                            operatorSyntax,
                            diagnostics
                        );

                        builder.nonTypeMembers.Add(method);
                    }

                    break;
                case SyntaxKind.LiteralOperatorDeclaration: {
                        var operatorSyntax = (LiteralOperatorDeclarationSyntax)m;

                        if (isImplicitClass && reportMisplacedGlobalCode)
                            diagnostics.Push(Error.NamespaceUnexpected(operatorSyntax.literalKeyword.location));

                        var method = SourceUserDefinedLiteralOperatorSymbol.CreateUserDefinedLiteralOperatorSymbol(
                            this,
                            operatorSyntax,
                            diagnostics
                        );

                        builder.nonTypeMembers.Add(method);
                    }

                    break;
                case SyntaxKind.ConversionDeclaration: {
                        var conversionSyntax = (ConversionDeclarationSyntax)m;

                        if (isImplicitClass && reportMisplacedGlobalCode)
                            diagnostics.Push(Error.NamespaceUnexpected(conversionSyntax.operatorKeyword.location));

                        var method = SourceUserDefinedConversionSymbol.CreateUserDefinedConversionSymbol(
                            this,
                            bodyBinder,
                            conversionSyntax,
                            diagnostics
                        );

                        builder.nonTypeMembers.Add(method);
                    }

                    break;
                case SyntaxKind.GlobalStatement:
                    var globalStatement = ((GlobalStatementSyntax)m).statement;
                    // AddInitializer(ref initializers, null, globalStatement);

                    if (reportMisplacedGlobalCode &&
                        !SyntaxFacts.IsSimpleProgramTopLevelStatement((GlobalStatementSyntax)m)) {
                        // TODO Report misplaced global code? (Think the MethodCompiler handles this actually...)
                        // diagnostics.Push(Error.NamespaceUnexpected(globalStatement.location));
                    }

                    break;
                default:
                    break;
            }
        }

        AddInitializers(builder.instanceInitializers, instanceInitializers);
        AddInitializers(builder.staticInitializers, staticInitializers);

        SourceMemberFieldSymbol AddFieldMember(FieldDeclarationSyntax fieldSyntax, bool reportMisplacedGlobalCode) {
            if (isImplicitClass && reportMisplacedGlobalCode)
                diagnostics.Push(Error.NamespaceUnexpected(fieldSyntax.declaration.identifier.location));

            var modifiers = SourceMemberFieldSymbol.MakeModifiers(
                this,
                fieldSyntax.declaration.identifier,
                fieldSyntax.modifiers,
                diagnostics,
                out var modifierErrors
            );

            var declaration = fieldSyntax.declaration;
            var fieldSymbol = declaration.argumentList is null
                ? new SourceMemberFieldSymbolFromDeclarator(
                    this,
                    declaration,
                    modifiers,
                    modifierErrors,
                    diagnostics)
                : new SourceFixedFieldSymbol(this, declaration, modifiers, modifierErrors, diagnostics);

            builder.nonTypeMembers.Add(fieldSymbol);

            if (declaration.initializer is not null) {
                if (fieldSymbol.isStatic)
                    AddInitializer(ref staticInitializers, fieldSymbol, declaration.initializer);
                else
                    AddInitializer(ref instanceInitializers, fieldSymbol, declaration.initializer);
            }

            return fieldSymbol;
        }

        void SetAnonymousUnionType(ImmutableArray<SourceMemberFieldSymbol> fields) {
            if (_lazyAnonymousUnionTypes is null)
                Interlocked.CompareExchange(ref _lazyAnonymousUnionTypes, [], null);

            AnonymousUnionType result;

            lock (_lazyAnonymousUnionTypes) {
                if (!_lazyAnonymousUnionTypes.TryGetValue(fields[0], out result)) {
                    result = new AnonymousUnionType(this, fields);

                    foreach (var field in fields)
                        _lazyAnonymousUnionTypes.Add(field, result);
                }
            }

            if (_lazyAnonymousUnionFields is null)
                Interlocked.CompareExchange(ref _lazyAnonymousUnionFields, [], null);

            lock (_lazyAnonymousUnionFields) {
                if (_lazyAnonymousUnionFields.TryGetValue(result, out _))
                    return;

                var field = new SynthesizedFieldSymbol(
                    this,
                    result,
                    GeneratedNames.MakeAnonymousUnionFieldName(result.name),
                    true,
                    false,
                    false,
                    false,
                    false
                );

                _lazyAnonymousUnionFields.Add(result, field);
            }
        }

        void AddScopeInheritorInfo(ExternBlockDeclarationSyntax syntax, BelteDiagnosticQueue diagnostics) {
            var modifiers = ModifierHelpers.CreateModifiers(syntax.modifiers, diagnostics, out _);
            modifiers |= DeclarationModifiers.Extern | DeclarationModifiers.Static;

            var scopeInfo = new ScopeInheritorInfo(modifiers, syntax.attributeLists);

            if (_lazyScopeInheritorInfo is null)
                Interlocked.CompareExchange(ref _lazyScopeInheritorInfo, [], null);

            lock (_lazyScopeInheritorInfo) {
                if (_lazyScopeInheritorInfo.TryGetValue(syntax, out _))
                    return;

                _lazyScopeInheritorInfo.Add(syntax, scopeInfo);
            }
        }
    }

    internal DeclarationModifiers GetInheritedModifiersForMember(SyntaxNode syntax) {
        if (_lazyScopeInheritorInfo is null)
            return DeclarationModifiers.None;

        foreach (var (key, value) in _lazyScopeInheritorInfo) {
            if (key.Contains(syntax))
                return value.modifiers;
        }

        return DeclarationModifiers.None;
    }

    internal SyntaxList<AttributeListSyntax> GetInheritedAttributeListsForMember(SyntaxNode syntax) {
        if (_lazyScopeInheritorInfo is null)
            return null;

        foreach (var (key, value) in _lazyScopeInheritorInfo) {
            if (key.Contains(syntax))
                return value.attributeLists;
        }

        return null;
    }

    private static void AddInitializer(
        ref ArrayBuilder<FieldInitializer>? initializers,
        FieldSymbol field,
        BelteSyntaxNode node) {
        initializers ??= ArrayBuilder<FieldInitializer>.GetInstance();
        initializers.Add(new FieldInitializer(field, node));
    }

    private static void AddInitializers(
        ArrayBuilder<ArrayBuilder<FieldInitializer>> allInitializers,
        ArrayBuilder<FieldInitializer>? siblingsOpt) {
        if (siblingsOpt is not null)
            allInitializers.Add(siblingsOpt);
    }

    private void AddSynthesizedMembers(
        MembersAndInitializersBuilder builder,
        DeclaredMembersAndInitializers declaredMembersAndInitializers) {
        if (typeKind is TypeKind.Class)
            AddSynthesizedSimpleProgramEntryPointIfNecessary(builder, declaredMembersAndInitializers);

        switch (typeKind) {
            case TypeKind.Struct:
            case TypeKind.Enum:
            case TypeKind.Class:
            case TypeKind.Interface:
                AddSynthesizedConstructorsIfNecessary(builder, declaredMembersAndInitializers);
                break;
            default:
                break;
        }

        AddSynthesizedTupleMembersIfNecessary(builder, declaredMembersAndInitializers);
    }

    private void AddSynthesizedTupleMembersIfNecessary(
        MembersAndInitializersBuilder builder,
        DeclaredMembersAndInitializers declaredMembersAndInitializers) {
        if (!isTupleType)
            return;

        var synthesizedMembers = MakeSynthesizedTupleMembers(declaredMembersAndInitializers.nonTypeMembers);

        if (synthesizedMembers is null)
            return;

        foreach (var synthesizedMember in synthesizedMembers)
            builder.AddNonTypeMember(synthesizedMember, declaredMembersAndInitializers);

        synthesizedMembers.Free();
    }

    private void AddSynthesizedSimpleProgramEntryPointIfNecessary(
        MembersAndInitializersBuilder builder,
        DeclaredMembersAndInitializers declaredMembersAndInitializers) {
        var simpleProgramEntryPoints = GetSimpleProgramEntryPoints();

        foreach (var member in simpleProgramEntryPoints)
            builder.AddNonTypeMember(member, declaredMembersAndInitializers);
    }

    private void AddSynthesizedConstructorsIfNecessary(
        MembersAndInitializersBuilder builder,
        DeclaredMembersAndInitializers declaredMembersAndInitializers) {
        var hasInstanceConstructor = false;
        var hasParameterlessConstructor = false;
        var hasStaticConstructor = false;

        var membersSoFar = builder.GetNonTypeMembers(declaredMembersAndInitializers);

        foreach (var member in membersSoFar) {
            if (member.kind == SymbolKind.Method) {
                var method = (MethodSymbol)member;

                switch (method.methodKind) {
                    case MethodKind.Constructor:
                        hasInstanceConstructor = true;
                        hasParameterlessConstructor = hasParameterlessConstructor || method.parameters.Length == 0;
                        break;
                    case MethodKind.StaticConstructor:
                        hasStaticConstructor = true;
                        break;
                }
            }

            if (hasInstanceConstructor && hasParameterlessConstructor)
                break;
        }

        if (!hasStaticConstructor && HasNonConstExprInitializer(declaredMembersAndInitializers.staticInitializers))
            builder.AddNonTypeMember(new SynthesizedStaticConstructor(this), declaredMembersAndInitializers);

        // TODO Do we want to have structs a parameterless constructor always?
        // if ((!hasParameterlessConstructor && IsStructType()) || (!hasConstructor && !isStatic))
        if (!hasInstanceConstructor && !isStatic && !isInterface)
            builder.AddNonTypeMember(new SynthesizedInstanceConstructorSymbol(this), declaredMembersAndInitializers);

        static bool HasNonConstExprInitializer(ImmutableArray<ImmutableArray<FieldInitializer>> initializers) {
            return initializers.Any(
                static siblings => siblings.Any(static initializer => !initializer.field.isConstExpr)
            );
        }
    }

    private static int? MakeExplicitAlignment(PackedArgumentSyntax packedArgument, BelteDiagnosticQueue diagnostics) {
        if (packedArgument.alignment is null)
            return 1;

        var alignmentValue = packedArgument.alignment.value;
        var alignmentType = SpecialTypeExtensions.SpecialTypeFromLiteralValue(alignmentValue);

        if (!LiteralUtilities.TrySpecialCastCore(alignmentValue, alignmentType, SpecialType.Int, out var result)) {
            diagnostics.Push(
                Error.CannotConvertConstantValue(
                    packedArgument.alignment.location,
                    result,
                    CorLibrary.GetSpecialType(SpecialType.Int)
                )
            );
        } else {
            var alignment = (long)result;

            if (alignment is 1 or 2 or 4 or 8 or 16 or 32 or 64 or 128)
                return (int)alignment;

            diagnostics.Push(Error.InvalidPackedAlignment(packedArgument.alignment.location));
        }

        return null;
    }

    private DeclarationModifiers MakeModifiers(BelteDiagnosticQueue diagnostics) {
        var defaultAccess = containingSymbol is null or NamespaceSymbol
            ? DeclarationModifiers.None
            : DeclarationModifiers.Private;

        var allowedModifiers = DeclarationModifiers.AccessibilityMask;

        if (containingSymbol.kind == SymbolKind.Namespace) {
            // defaultAccess = DeclarationModifiers.Internal;
        } else {
            allowedModifiers |= DeclarationModifiers.New;

            if (((NamedTypeSymbol)containingSymbol).isInterface)
                defaultAccess = DeclarationModifiers.Public;
            else
                defaultAccess = DeclarationModifiers.Private;
        }

        switch (typeKind) {
            case TypeKind.Class:
                allowedModifiers |= DeclarationModifiers.Sealed
                                 | DeclarationModifiers.Abstract
                                 | DeclarationModifiers.LowLevel
                                 | DeclarationModifiers.Static;
                break;
            case TypeKind.Interface:
                allowedModifiers |= DeclarationModifiers.LowLevel;
                break;
            case TypeKind.Struct:
                allowedModifiers |= DeclarationModifiers.LowLevel;
                break;
        }

        var mods = MakeAndCheckTypeModifiers(
            defaultAccess,
            allowedModifiers,
            diagnostics,
            out var hasErrors);

        if (!hasErrors &&
            (mods & DeclarationModifiers.Abstract) != 0 &&
            (mods & (DeclarationModifiers.Sealed | DeclarationModifiers.Static)) != 0) {
            diagnostics.Push(
                Error.ConflictingModifiers(location, "abstract", isSealed ? "sealed" : "static")
            );
        }

        if (!hasErrors &&
            (mods & (DeclarationModifiers.Sealed | DeclarationModifiers.Static)) ==
            (DeclarationModifiers.Sealed | DeclarationModifiers.Static)) {
            diagnostics.Push(Error.ConflictingModifiers(location, "sealed", "static"));
        }

        switch (typeKind) {
            case TypeKind.Interface:
                mods |= DeclarationModifiers.Abstract;
                break;
            case TypeKind.Struct:
            case TypeKind.Enum:
                mods |= DeclarationModifiers.Sealed;
                break;
        }

        return mods;
    }

    private DeclarationModifiers MakeAndCheckTypeModifiers(
        DeclarationModifiers defaultAccess,
        DeclarationModifiers allowedModifiers,
        BelteDiagnosticQueue diagnostics,
        out bool hasErrors) {
        var modifiers = _declaration.declarations[0].modifiers;
        var partCount = _declaration.declarations.Length;

        modifiers = ModifierHelpers.CheckModifiers(
            true,
            false,
            modifiers,
            allowedModifiers,
            location,
            diagnostics,
            out hasErrors
        );

        if (!hasErrors)
            hasErrors = ModifierHelpers.CheckAccessibility(modifiers, diagnostics, location);

        if ((modifiers & DeclarationModifiers.AccessibilityMask) == 0)
            modifiers |= defaultAccess;

        switch (containingSymbol.kind) {
            case SymbolKind.Namespace:
                for (var i = 1; i < partCount; i++) {
                    diagnostics.Push(Error.DuplicateNameInNamespace(
                        _declaration.declarations[i].nameLocation,
                        name,
                        containingNamespace
                    ));

                    hasErrors = true;
                }

                break;
            case SymbolKind.NamedType:
                for (var i = 1; i < partCount; i++) {
                    if (containingType.locations.Length == 1) {
                        diagnostics.Push(Error.DuplicateNameInClass(
                            _declaration.declarations[i].nameLocation,
                            containingSymbol,
                            name
                        ));
                    }

                    hasErrors = true;
                }

                break;
        }

        return modifiers;
    }

    private SpecialType MakeSpecialType() {
        if (declaringCompilation.keepLookingForCorTypes) {
            string emittedName = null;

            if (containingSymbol is not null)
                emittedName = containingSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedNameFormat);

            emittedName = MetadataHelpers.BuildQualifiedName(emittedName, metadataName);

            return SpecialTypes.GetTypeFromMetadataName(emittedName);
        }

        return SpecialType.None;
    }

    internal static Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> ToNameKeyedDictionary(
        ImmutableArray<Symbol> symbols) {
        if (symbols is [var symbol]) {
            return new Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>>(
                1,
                ReadOnlyMemoryOfCharComparer.Instance) {
                {  symbol.name.AsMemory(), ImmutableArray.Create(symbol) },
            };
        }

        if (symbols.Length == 0)
            return new Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>>(ReadOnlyMemoryOfCharComparer.Instance);

        var accumulator = NameToObjectPool.Allocate();

        foreach (var item in symbols)
            ImmutableArrayExtensions.AddToMultiValueDictionaryBuilder(accumulator, item.name.AsMemory(), item);

        var dictionary = new Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>>(
            accumulator.Count,
            ReadOnlyMemoryOfCharComparer.Instance
        );

        foreach (var pair in accumulator) {
            dictionary.Add(pair.Key, pair.Value is ArrayBuilder<Symbol> arrayBuilder
                ? arrayBuilder.ToImmutableAndFree()
                : [(Symbol)pair.Value]);
        }

        accumulator.Free();
        return dictionary;
    }

    private static void AddNestedTypesToDictionary(
        Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> membersByName,
        Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> typesByName) {
        foreach ((var name, var types) in typesByName) {
            var typesAsSymbols = StaticCast<Symbol>.From(types);

            if (membersByName.TryGetValue(name, out var membersForName))
                membersByName[name] = membersForName.Concat(typesAsSymbols);
            else
                membersByName.Add(name, typesAsSymbols);
        }
    }

    internal bool TryCalculateSyntaxOffsetOfPositionInInitializer(
        int position,
        SyntaxTree tree,
        bool isStatic,
        int ctorInitializerLength,
        out int syntaxOffset) {
        var membersAndInitializers = GetMembersAndInitializers();
        var allInitializers = isStatic
            ? membersAndInitializers.staticInitializers
            : membersAndInitializers.instanceInitializers;

        if (!FindInitializer(allInitializers, position, tree, out var initializer, out var precedingLength)) {
            syntaxOffset = 0;
            return false;
        }

        var initializersLength = GetInitializersLength(allInitializers);
        var distanceFromInitializerStart = position - initializer.syntax.span.start;

        var distanceFromCtorBody =
            initializersLength + ctorInitializerLength -
            (precedingLength + distanceFromInitializerStart);

        syntaxOffset = -distanceFromCtorBody;
        return true;

        static bool FindInitializer(
            ImmutableArray<ImmutableArray<FieldInitializer>> initializers,
            int position,
            SyntaxTree tree,
            out FieldInitializer found,
            out int precedingLength) {
            precedingLength = 0;

            foreach (var group in initializers) {
                if (!group.IsEmpty &&
                    group[0].syntax.syntaxTree == tree &&
                    position < group.Last().syntax.span.end) {
                    var initializerIndex = IndexOfInitializerContainingPosition(group, position);

                    if (initializerIndex < 0)
                        break;

                    precedingLength += GetPrecedingInitializersLength(group, initializerIndex);
                    found = group[initializerIndex];
                    return true;
                }

                precedingLength += GetGroupLength(group);
            }

            found = default;
            return false;
        }

        static int GetGroupLength(ImmutableArray<FieldInitializer> initializers) {
            var length = 0;

            foreach (var initializer in initializers)
                length += GetInitializerLength(initializer);

            return length;
        }

        static int GetPrecedingInitializersLength(ImmutableArray<FieldInitializer> initializers, int index) {
            var length = 0;

            for (var i = 0; i < index; i++)
                length += GetInitializerLength(initializers[i]);

            return length;
        }

        static int GetInitializersLength(ImmutableArray<ImmutableArray<FieldInitializer>> initializers) {
            var length = 0;

            foreach (var group in initializers)
                length += GetGroupLength(group);

            return length;
        }

        static int GetInitializerLength(FieldInitializer initializer) {
            if (initializer.field is null || !initializer.field.isMetadataConstant)
                return initializer.syntax.span.length;

            return 0;
        }
    }

    private static int IndexOfInitializerContainingPosition(
        ImmutableArray<FieldInitializer> initializers,
        int position) {
        var index = initializers.BinarySearch(
            position,
            (initializer, pos) => initializer.syntax.span.start.CompareTo(pos)
        );

        if (index >= 0)
            return index;

        var precedingInitializerIndex = ~index - 1;

        if (precedingInitializerIndex >= 0 && initializers[precedingInitializerIndex].syntax.span.Contains(position))
            return precedingInitializerIndex;

        return -1;
    }

    internal int CalculateSyntaxOffsetInSynthesizedConstructor(int position, SyntaxTree tree, bool isStatic) {
        if (TryCalculateSyntaxOffsetOfPositionInInitializer(
            position,
            tree,
            isStatic,
            0,
            out var syntaxOffset)) {
            return syntaxOffset;
        }

        if (_declaration.declarations.Length >= 1 &&
            position == _declaration.declarations[0].location.span.start) {
            return 0;
        }

        throw ExceptionUtilities.Unreachable();
    }

    private bool CheckStructCircularity(BelteDiagnosticQueue diagnostics) {
        CheckFiniteFlatteningGraph(diagnostics);
        return HasStructCircularity(diagnostics);
    }

    private bool HasStructCircularity(BelteDiagnosticQueue diagnostics) {
        foreach (var valuesByName in GetMembersByName().Values) {
            foreach (var member in valuesByName) {
                if (member.kind != SymbolKind.Field)
                    continue;

                var field = (FieldSymbol)member;

                if (field.isStatic)
                    continue;

                var type = field.NonPointerType()?.StrippedType();

                if ((type is not null) &&
                    (type.typeKind == TypeKind.Struct) &&
                    BaseTypeAnalysis.StructDependsOn((NamedTypeSymbol)type, this)) {
                    diagnostics.Push(Error.StructLayoutCycle(field.location, field, type));
                    return true;
                }
            }
        }

        return false;
    }

    private void CheckFiniteFlatteningGraph(BelteDiagnosticQueue diagnostics) {
        if (AllTemplateArgumentsCount() == 0)
            return;

        var instanceMap = new Dictionary<NamedTypeSymbol, NamedTypeSymbol>(ReferenceEqualityComparer.Instance) {
            { this, this }
        };

        foreach (var m in GetMembersUnordered()) {
            if (m is not FieldSymbol f || !f.isStatic || f.type.typeKind != TypeKind.Struct)
                continue;

            var type = (NamedTypeSymbol)f.type;

            if (InfiniteFlatteningGraph(this, type, instanceMap)) {
                diagnostics.Push(Error.StructLayoutCycle(f.location, f, type));
                return;
            }
        }
    }

    private static bool InfiniteFlatteningGraph(
        SourceMemberContainerTypeSymbol top,
        NamedTypeSymbol t,
        Dictionary<NamedTypeSymbol, NamedTypeSymbol> instanceMap) {
        if (!t.ContainsTemplateParameter())
            return false;

        var tOriginal = t.originalDefinition;

        if (instanceMap.TryGetValue(tOriginal, out var oldInstance)) {
            return (!Equals(oldInstance, t)) && ReferenceEquals(tOriginal, top);
        } else {
            instanceMap.Add(tOriginal, t);

            try {
                foreach (var m in t.GetMembersUnordered()) {
                    if (m is not FieldSymbol f || !f.isStatic || f.type.typeKind != TypeKind.Struct)
                        continue;

                    var type = (NamedTypeSymbol)f.type;

                    if (InfiniteFlatteningGraph(top, type, instanceMap))
                        return true;
                }

                return false;
            } finally {
                instanceMap.Remove(tOriginal);
            }
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasFlag(DeclarationModifiers flag) => (_modifiers & flag) != 0;
}
