using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal class MissingModuleSymbol : ModuleSymbol {
    internal MissingModuleSymbol(AssemblySymbol assembly, int ordinal) {
        this.ordinal = ordinal;
        containingAssembly = assembly;
        globalNamespace = new MissingNamespaceSymbol(this);
    }

    public override string name => "<Missing Module>";

    internal override int ordinal { get; }

    internal override bool bit32Required => false;

    internal sealed override bool isMissing => true;

    internal override AssemblySymbol containingAssembly { get; }

    internal override Symbol containingSymbol => containingAssembly;

    internal override NamespaceSymbol globalNamespace { get; }

    internal override ImmutableArray<TextLocation> locations => [];

    internal override TextLocation location => null;

    internal override ICollection<string> namespaceNames => SpecializedCollections.EmptyCollection<string>();

    internal override ICollection<string> typeNames => SpecializedCollections.EmptyCollection<string>();

    internal override ImmutableArray<AssemblyIdentity> referencedAssemblies => [];

    internal override ImmutableArray<AssemblySymbol> referencedAssemblySymbols => [];

    internal override bool hasUnifiedReferences => false;

    internal override bool hasAssemblyCompilationRelaxationsAttribute => false;

    internal override bool hasAssemblyRuntimeCompatibilityAttribute => false;

    internal override CharSet? defaultMarshallingCharSet => null;

    internal sealed override bool areLocalsZeroed => throw ExceptionUtilities.Unreachable();

    internal sealed override bool useUpdatedEscapeRules => false;

    internal override ModuleMetadata GetMetadata() => null;

    internal override NamedTypeSymbol LookupTopLevelMetadataType(ref MetadataTypeName emittedName) {
        return null;
    }

    internal override void SetReferences(
        ModuleReferences<AssemblySymbol> moduleReferences,
        SourceAssemblySymbol originatingSourceAssemblyDebugOnly) {
        throw ExceptionUtilities.Unreachable();
    }

    public override int GetHashCode() {
        return containingAssembly.GetHashCode();
    }

    internal override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if (ReferenceEquals(this, obj))
            return true;

        return obj is MissingModuleSymbol other &&
            containingAssembly.Equals(other.containingAssembly, compareKind);
    }
}
