using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal class MissingAssemblySymbol : AssemblySymbol {
    internal MissingAssemblySymbol(AssemblyIdentity identity) {
        this.identity = identity;
        modules = [new MissingModuleSymbol(this, 0)];
    }

    internal sealed override bool isMissing => true;

    internal override bool isLinked => false;

    internal override AssemblyIdentity identity { get; }

    internal override ImmutableArray<byte> publicKey => identity.publicKey;

    internal override ImmutableArray<ModuleSymbol> modules { get; }

    public override int GetHashCode() {
        return identity.GetHashCode();
    }

    internal override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        return Equals(obj as MissingAssemblySymbol);
    }

    public bool Equals(MissingAssemblySymbol other) {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return identity.Equals(other.identity);
    }

    internal override ImmutableArray<TextLocation> locations => [];

    internal override TextLocation location => null;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal sealed override NamespaceSymbol globalNamespace => modules[0].globalNamespace;

    internal override ICollection<string> typeNames => SpecializedCollections.EmptyCollection<string>();

    internal override ICollection<string> namespaceNames => SpecializedCollections.EmptyCollection<string>();

    internal override NamedTypeSymbol LookupDeclaredOrForwardedTopLevelMetadataType(
        ref MetadataTypeName emittedName,
        ConsList<AssemblySymbol> visitedAssemblies) {
        return new MissingMetadataTypeSymbol.TopLevel(modules[0], ref emittedName);
    }

    internal override NamedTypeSymbol TryLookupForwardedMetadataTypeWithCycleDetection(
        ref MetadataTypeName emittedName,
        ConsList<AssemblySymbol> visitedAssemblies) {
        return null;
    }

    internal override NamedTypeSymbol? LookupDeclaredTopLevelMetadataType(ref MetadataTypeName emittedName) {
        return null;
    }

    internal override bool AreInternalsVisibleToThisAssembly(AssemblySymbol other) {
        return false;
    }

    internal override IEnumerable<ImmutableArray<byte>> GetInternalsVisibleToPublicKeys(string simpleName) {
        return SpecializedCollections.EmptyEnumerable<ImmutableArray<byte>>();
    }

    internal override IEnumerable<string> GetInternalsVisibleToAssemblyNames() {
        return SpecializedCollections.EmptyEnumerable<string>();
    }

    internal override AssemblyMetadata GetMetadata() {
        return null;
    }

    internal sealed override IEnumerable<NamedTypeSymbol> GetAllTopLevelForwardedTypes() {
        return SpecializedCollections.EmptyEnumerable<NamedTypeSymbol>();
    }

    internal override void SetLinkedReferencedAssemblies(ImmutableArray<AssemblySymbol> assemblies) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<AssemblySymbol> GetLinkedReferencedAssemblies() {
        return [];
    }

    internal override void SetNoPiaResolutionAssemblies(ImmutableArray<AssemblySymbol> assemblies) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override ImmutableArray<AssemblySymbol> GetNoPiaResolutionAssemblies() {
        return [];
    }

    internal override bool GetGuidString(out string guidString) {
        guidString = null;
        return false;
    }
}
