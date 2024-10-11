using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMemberContainerTypeSymbol : NamedTypeSymbol {
    private static readonly Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> EmptyTypeMembers =
        new Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>>(EmptyReadOnlyMemoryOfCharComparer.Instance);

    private readonly DeclarationModifiers _modifiers;
    private readonly TextLocation _nameLocation;
    private protected SymbolCompletionState _state;
    private protected readonly TypeDeclarationSyntax _declaration;

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>>? _lazyTypeMembers;
    private DeclaredMembersAndInitializers _lazyDeclaredMembersAndInitializers = DeclaredMembersAndInitializers.UninitializedSentinel;
    private MembersAndInitializers _lazyMembersAndInitializers;
    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>>? _lazyMembersDictionary;
    private ImmutableArray<Symbol> _lazyMembers;

    internal SourceMemberContainerTypeSymbol(
        NamespaceOrTypeSymbol containingSymbol,
        TypeDeclarationSyntax declaration,
        BelteDiagnosticQueue diagnostics) {
        this.containingSymbol = containingSymbol;
        _declaration = declaration;
        _nameLocation = declaration.identifier.location;
        typeKind = declaration.kind == SyntaxKind.ClassDeclaration ? TypeKind.Class : TypeKind.Struct;
        name = declaration.identifier.text;
        arity = declaration.templateParameterList.parameters.Count;

        var modifiers = MakeModifiers(diagnostics);
        var access = (int)(modifiers & DeclarationModifiers.AccessibilityMask);

        if ((access & (access - 1)) != 0) {
            access &= ~(access - 1);
            modifiers &= ~DeclarationModifiers.AccessibilityMask;
            modifiers |= (DeclarationModifiers)access;
        }

        _modifiers = modifiers;
        specialType = MakeSpecialType();

        var containingType = this.containingType;

        if (containingType?.isSealed == true && accessibility.HasFlag(Accessibility.Protected)) {
            var protectedModifierIndex = declaration.modifiers.IndexOf(SyntaxKind.ProtectedKeyword);
            var protectedModifier = declaration.modifiers[protectedModifierIndex];
            diagnostics.Push(Warning.ProtectedMemberInSealedType(protectedModifier.location, containingSymbol, this));
        }

        _state.NotePartComplete(CompletionParts.TemplateArguments);
    }

    public override string name { get; }

    internal override int arity { get; }

    internal sealed override bool mangleName => arity > 0;

    internal sealed override bool isStatic => HasFlag(DeclarationModifiers.Static);

    internal sealed override bool isAbstract => HasFlag(DeclarationModifiers.Abstract);

    internal sealed override bool isSealed => HasFlag(DeclarationModifiers.Sealed);

    internal bool isLowLevel => HasFlag(DeclarationModifiers.LowLevel);

    internal sealed override Accessibility accessibility => ModifierHelpers.EffectiveAccessibility(_modifiers);

    internal override Symbol containingSymbol { get; }

    internal override TypeKind typeKind { get; }

    internal override SpecialType specialType { get; }

    internal override SyntaxReference syntaxReference => new SyntaxReference(_declaration);

    internal sealed override ImmutableArray<Symbol> GetMembers() {
        if (!_lazyMembers.IsDefault)
            return _lazyMembers;

        var members = GetMembersByName().Flatten();

        if (members.Length > 1) {
            members = members.Sort(LexicalOrderSymbolComparer.Instance);
            ImmutableInterlocked.InterlockedExchange(ref _lazyMembers, members);
        }

        return members;
    }

    internal sealed override ImmutableArray<Symbol> GetMembers(string name) {

    }

    internal override void ForceComplete(TextLocation location) {
        // TODO
    }

    internal sealed override bool HasComplete(CompletionParts part) {
        return _state.HasComplete(part);
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

        var diagnostics = BelteDiagnosticQueue.Instance;
        membersAndInitializers = BuildMembersAndInitializers(diagnostics);

        var alreadyKnown = Interlocked.CompareExchange(ref _lazyMembersAndInitializers, membersAndInitializers, null);

        if (alreadyKnown is not null)
            return alreadyKnown;

        AddDeclarationDiagnostics(diagnostics);
        _lazyDeclaredMembersAndInitializers = null;
        return membersAndInitializers;
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
            var diagnostics = BelteDiagnosticQueue.Instance;

            if (Interlocked.CompareExchange(ref _lazyTypeMembers, MakeTypeMembers(diagnostics), null) is null) {
                AddDeclarationDiagnostics(diagnostics);
                _state.NotePartComplete(CompletionParts.TypeMembers);
            }
        }

        return _lazyTypeMembers;
    }

    private Dictionary<ReadOnlyMemory<char>, ImmutableArray<NamedTypeSymbol>> MakeTypeMembers(
        BelteDiagnosticQueue diagnostics) {
        var symbols = ArrayBuilder<NamedTypeSymbol>.GetInstance();
        var conflicts = new Dictionary<(string name, int arity, SyntaxTree syntaxTree), SourceNamedTypeSymbol>();

        try {
            foreach (var childDeclaration in _declaration.members.Where(m => m is TypeDeclarationSyntax)) {
                var t = new SourceNamedTypeSymbol(this, childDeclaration as TypeDeclarationSyntax, diagnostics);
                CheckMemberNameDistinctFromType(t, diagnostics);

                var key = (t.name, t.arity, t.syntaxReference.syntaxTree);

                if (conflicts.TryGetValue(key, out var other)) {
                    diagnostics.Push(
                        Error.TypeAlreadyDeclared(t.syntaxReference.location, t.name, t.typeKind == TypeKind.Class)
                    );
                } else {
                    conflicts.Add(key, t);
                }

                symbols.Add(t);
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
                if (member.name == name)
                    diagnostics.Push(Error.MemberNameSameAsType(member.syntaxReference.location, name));

                break;
        }
    }

    private MembersAndInitializers BuildMembersAndInitializers(BelteDiagnosticQueue diagnostics) {
        var declaredMembersAndInitializers = GetDeclaredMembersAndInitializers();

        if (declaredMembersAndInitializers is null)
            return null;

        var membersAndInitializersBuilder = new MembersAndInitializersBuilder();
        AddSynthesizedMembers(membersAndInitializersBuilder, declaredMembersAndInitializers);

        if (Volatile.Read(ref _lazyMembersAndInitializers) != null) {
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
                    CheckForStructBadInitializers(builder, diagnostics);
                    CheckForStructDefaultConstructors(builder.nonTypeMembers, isEnum: false, diagnostics: diagnostics);
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

    private void AddSynthesizedMembers(
        MembersAndInitializersBuilder builder,
        DeclaredMembersAndInitializers declaredMembersAndInitializers) {
        switch (typeKind) {
            case TypeKind.Struct:
            case TypeKind.Class:
                AddSynthesizedConstructorsIfNecessary(builder, declaredMembersAndInitializers);
                break;
            default:
                break;
        }
    }

    private void AddSynthesizedConstructorsIfNecessary(
        MembersAndInitializersBuilder builder,
        DeclaredMembersAndInitializers declaredMembersAndInitializers) {
        var hasConstructor = false;
        var hasParameterlessConstructor = false;

        var membersSoFar = builder.GetNonTypeMembers(declaredMembersAndInitializers);

        foreach (var member in membersSoFar) {
            if (member.kind == SymbolKind.Method) {
                var method = (MethodSymbol)member;

                switch (method.methodKind) {
                    case MethodKind.Constructor:
                        hasConstructor = true;
                        hasParameterlessConstructor = hasParameterlessConstructor || method.parameters.Length == 0;
                        break;
                }
            }

            if (hasConstructor && hasParameterlessConstructor) {
                break;
            }
        }

        if ((!hasParameterlessConstructor && IsStructType()) ||
            (!hasConstructor && !isStatic)) {
            builder.AddNonTypeMember(new SynthesizedInstanceConstructor(this), declaredMembersAndInitializers);
        }
    }

    private DeclarationModifiers MakeModifiers(BelteDiagnosticQueue diagnostics) {
        var defaultAccess = DeclarationModifiers.Private;
        var allowedModifiers = DeclarationModifiers.AccessibilityMask;

        switch (typeKind) {
            case TypeKind.Class:
                allowedModifiers |= DeclarationModifiers.Sealed | DeclarationModifiers.Abstract
                    | DeclarationModifiers.LowLevel | DeclarationModifiers.Static;
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
                Error.ConflictingModifiers(_nameLocation, "abstract", isSealed ? "sealed" : "static")
            );
        }

        if (!hasErrors &&
            (mods & (DeclarationModifiers.Sealed | DeclarationModifiers.Static)) ==
            (DeclarationModifiers.Sealed | DeclarationModifiers.Static)) {
            diagnostics.Push(Error.ConflictingModifiers(_nameLocation, "sealed", "static"));
        }

        if (typeKind == TypeKind.Struct)
            mods |= DeclarationModifiers.Sealed;

        return mods;
    }

    private DeclarationModifiers MakeAndCheckTypeModifiers(
        DeclarationModifiers defaultAccess,
        DeclarationModifiers allowedModifiers,
        BelteDiagnosticQueue diagnostics,
        out bool hasErrors) {
        var modifiers = ModifierHelpers.CreateModifiers(
            _declaration.modifiers,
            diagnostics,
            out var hasDuplicateErrors
        );

        modifiers = ModifierHelpers.CheckModifiers(
            true,
            modifiers,
            allowedModifiers,
            _nameLocation,
            diagnostics,
            out hasErrors
        );

        hasErrors |= hasDuplicateErrors;

        if (!hasErrors)
            hasErrors = ModifierHelpers.CheckAccessibility(modifiers, diagnostics, _nameLocation);

        if ((modifiers & DeclarationModifiers.AccessibilityMask) == 0)
            modifiers |= defaultAccess;

        return modifiers;
    }

    private SpecialType MakeSpecialType() {
        if (declaringCompilation.keepLookingForCorTypes) {
            string emittedName = null;

            if (containingSymbol is not null)
                emittedName = containingSymbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat);

            emittedName = MetadataHelpers.BuildQualifiedName(emittedName, metadataName);

            return SpecialTypes.GetTypeFromMetadataName(emittedName);
        }

        return SpecialType.None;
    }

    private static Dictionary<ReadOnlyMemory<char>, ImmutableArray<Symbol>> ToNameKeyedDictionary(
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasFlag(DeclarationModifiers flag) => (_modifiers & flag) != 0;
}
