using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis;

internal sealed partial class SingleTypeDeclaration : SingleNamespaceOrTypeDeclaration {
    private readonly TypeDeclarationFlags _flags;
    private readonly ushort _arity;

    internal SingleTypeDeclaration(
        DeclarationKind kind,
        string name,
        int arity,
        DeclarationModifiers modifiers,
        TypeDeclarationFlags declFlags,
        SyntaxReference syntaxReference,
        TextLocation nameLocation,
        StrongBox<ImmutableSegmentedHashSet<string>> memberNames,
        ImmutableArray<SingleTypeDeclaration> children,
        ImmutableArray<BelteDiagnostic> diagnostics)
        : base(name, syntaxReference, nameLocation, diagnostics) {
        this.kind = kind;
        _arity = (ushort)arity;
        this.modifiers = modifiers;
        this.memberNames = memberNames;
        this.children = children;
        _flags = declFlags;
    }

    internal override DeclarationKind kind { get; }

    internal new ImmutableArray<SingleTypeDeclaration> children { get; }

    internal int arity => _arity;

    internal DeclarationModifiers modifiers { get; }

    internal StrongBox<ImmutableSegmentedHashSet<string>> memberNames { get; }

    internal bool hasBaseDeclarations => (_flags & TypeDeclarationFlags.HasBaseDeclarations) != 0;

    internal bool hasAnyNonTypeMembers => (_flags & TypeDeclarationFlags.HasAnyNonTypeMembers) != 0;

    internal bool hasReturnWithExpression => (_flags & TypeDeclarationFlags.HasReturnWithExpression) != 0;

    internal bool isSimpleProgram => (_flags & TypeDeclarationFlags.IsSimpleProgram) != 0;

    internal bool hasRequiredMembers => (_flags & TypeDeclarationFlags.HasRequiredMembers) != 0;

    internal TypeDeclarationIdentity identity => new TypeDeclarationIdentity(this);

    private protected override ImmutableArray<SingleNamespaceOrTypeDeclaration>
        GetNamespaceOrTypeDeclarationChildren() {
        return StaticCast<SingleNamespaceOrTypeDeclaration>.From(children);
    }
}
