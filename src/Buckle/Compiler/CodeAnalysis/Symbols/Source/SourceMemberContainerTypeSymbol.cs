using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceMemberContainerTypeSymbol : NamedTypeSymbol {
    private readonly DeclarationModifiers _modifiers;
    private readonly TextLocation _nameLocation;
    private protected SymbolCompletionState _state;
    private protected readonly TypeDeclarationSyntax _declaration;

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
            access = access & ~(access - 1);
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

    internal override bool mangleName => arity > 0;

    internal override bool isStatic => HasFlag(DeclarationModifiers.Static);

    internal override bool isAbstract => HasFlag(DeclarationModifiers.Abstract);

    internal override bool isSealed => HasFlag(DeclarationModifiers.Sealed);

    internal bool isLowLevel => HasFlag(DeclarationModifiers.LowLevel);

    internal override Accessibility accessibility => ModifierHelpers.EffectiveAccessibility(_modifiers);

    internal override Symbol containingSymbol { get; }

    internal override TypeKind typeKind { get; }

    internal override SpecialType specialType { get; }

    internal override SyntaxReference syntaxReference => new SyntaxReference(_declaration);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasFlag(DeclarationModifiers flag) => (_modifiers & flag) != 0;
}
