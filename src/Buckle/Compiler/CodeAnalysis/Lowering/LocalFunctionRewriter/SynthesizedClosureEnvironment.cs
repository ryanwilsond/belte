using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class SynthesizedClosureEnvironment : SynthesizedContainer {
    internal readonly MethodSymbol topLevelMethod;
    internal readonly SyntaxNode scopeSyntax;

    internal readonly MethodSymbol originalContainingMethod;
    internal readonly FieldSymbol singletonCache;

    private ArrayBuilder<Symbol> _membersBuilder = ArrayBuilder<Symbol>.GetInstance();
    private ImmutableArray<Symbol> _members;

    internal readonly int closureOrdinal;
    internal readonly int closureId;

    internal SynthesizedClosureEnvironment(
        MethodSymbol topLevelMethod,
        MethodSymbol containingMethod,
        bool isStruct,
        SyntaxNode scopeSyntax,
        int methodOrdinal,
        int closureOrdinal)
        : base(MakeName(scopeSyntax, methodOrdinal, closureOrdinal), containingMethod) {
        typeKind = isStruct ? TypeKind.Struct : TypeKind.Class;
        this.topLevelMethod = topLevelMethod;
        originalContainingMethod = containingMethod;
        this.closureOrdinal = closureOrdinal;
        this.scopeSyntax = scopeSyntax;
        // constructor = isStruct ? null : new SynthesizedClosureEnvironmentConstructor(this);
        constructor = new SynthesizedClosureEnvironmentConstructor(this);
    }

    public override TypeKind typeKind { get; }

    internal override MethodSymbol constructor { get; }

    internal override Symbol containingSymbol => topLevelMethod.containingSymbol;

    internal void AddHoistedField(LambdaCapturedVariable captured) => _membersBuilder.Add(captured);

    private static string MakeName(SyntaxNode scopeSyntaxOpt, int methodId, int closureId) {
        if (scopeSyntaxOpt is null)
            return GeneratedNames.MakeDisplayClassName(methodId, 0);

        return GeneratedNames.MakeDisplayClassName(methodId, closureId);
    }

    internal override ImmutableArray<Symbol> GetMembers() {
        if (_members.IsDefault) {
            var builder = _membersBuilder;
            builder.AddRange(base.GetMembers());
            _members = builder.ToImmutableAndFree();
            _membersBuilder = null;
        }

        return _members;
    }

    internal override IEnumerable<FieldSymbol> GetFieldsToEmit()
        => singletonCache is not null
        ? SpecializedCollections.SingletonEnumerable(singletonCache)
        : SpecializedCollections.EmptyEnumerable<FieldSymbol>();
}
