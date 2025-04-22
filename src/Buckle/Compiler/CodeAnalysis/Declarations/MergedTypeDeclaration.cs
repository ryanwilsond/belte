using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
internal sealed class MergedTypeDeclaration : MergedNamespaceOrTypeDeclaration {
    private ImmutableArray<MergedTypeDeclaration> _lazyChildren;
    private ICollection<string> _lazyMemberNames;

    internal MergedTypeDeclaration(ImmutableArray<SingleTypeDeclaration> declarations)
        : base(declarations[0].name) {
        this.declarations = declarations;
    }

    internal ImmutableArray<SingleTypeDeclaration> declarations { get; }

    internal ImmutableArray<SyntaxReference> syntaxReferences => declarations.SelectAsArray(r => r.syntaxReference);

    internal override DeclarationKind kind => declarations[0].kind;

    internal int arity => declarations[0].arity;

    internal LexicalSortKey GetLexicalSortKey(Compilation compilation) {
        var sortKey = new LexicalSortKey(
            declarations[0].syntaxReference.syntaxTree,
            declarations[0].nameLocation,
            compilation
        );

        for (var i = 1; i < declarations.Length; i++) {
            sortKey = LexicalSortKey.First(sortKey, new LexicalSortKey(
                declarations[i].syntaxReference.syntaxTree,
                declarations[i].nameLocation,
                compilation
            ));
        }

        return sortKey;
    }

    internal OneOrMany<TextLocation> nameLocations {
        get {
            if (declarations.Length == 1)
                return OneOrMany.Create(declarations[0].nameLocation);

            var builder = ArrayBuilder<TextLocation>.GetInstance(declarations.Length);

            foreach (var decl in declarations)
                builder.AddIfNotNull(decl.nameLocation);

            return builder.ToOneOrManyAndFree();
        }
    }

    internal new ImmutableArray<MergedTypeDeclaration> children {
        get {
            if (_lazyChildren.IsDefault)
                ImmutableInterlocked.InterlockedInitialize(ref _lazyChildren, MakeChildren());

            return _lazyChildren;
        }
    }

    internal ICollection<string> memberNames {
        get {
            if (_lazyMemberNames is null) {
                var names = UnionCollection<string>.Create(declarations, d => d.memberNames.Value);
                Interlocked.CompareExchange(ref _lazyMemberNames, names, null);
            }

            return _lazyMemberNames;
        }
    }

    private ImmutableArray<MergedTypeDeclaration> MakeChildren() {
        ArrayBuilder<SingleTypeDeclaration> nestedTypes = null;

        foreach (var decl in declarations) {
            foreach (var child in decl.children) {
                var asType = child as SingleTypeDeclaration;

                if (asType is not null) {
                    nestedTypes ??= ArrayBuilder<SingleTypeDeclaration>.GetInstance();
                    nestedTypes.Add(asType);
                }
            }
        }

        var children = ArrayBuilder<MergedTypeDeclaration>.GetInstance();

        if (nestedTypes != null) {
            var typesGrouped = nestedTypes.ToDictionary(t => t.identity);
            nestedTypes.Free();

            foreach (var typeGroup in typesGrouped.Values)
                children.Add(new MergedTypeDeclaration(typeGroup));
        }

        return children.ToImmutableAndFree();
    }

    private protected override ImmutableArray<Declaration> GetDeclarationChildren() {
        return StaticCast<Declaration>.From(children);
    }

    internal string GetDebuggerDisplay() {
        return $"{nameof(MergedTypeDeclaration)} {name}";
    }
}
