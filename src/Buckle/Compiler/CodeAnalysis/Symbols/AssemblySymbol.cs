using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class AssemblySymbol : Symbol {
    public override string name => identity.name;

    public sealed override SymbolKind kind => SymbolKind.Assembly;

    internal abstract AssemblyIdentity identity { get; }

    internal abstract NamespaceSymbol globalNamespace { get; }

    internal sealed override Symbol containingSymbol => null;

    internal sealed override AssemblySymbol containingAssembly => null;

    internal abstract bool isMissing { get; }

    internal sealed override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal sealed override bool isSealed => false;

    internal sealed override bool isStatic => false;

    internal sealed override bool isVirtual => false;

    internal sealed override bool isAbstract => false;

    internal sealed override bool isOverride => false;

    internal abstract override ImmutableArray<SyntaxReference> declaringSyntaxReferences { get; }

    internal abstract override ImmutableArray<TextLocation> locations { get; }

    internal override SyntaxReference syntaxReference => null;

    internal override TextLocation location => null;

    internal abstract ICollection<string> typeNames { get; }

    internal abstract ICollection<string> namespaceNames { get; }

    internal abstract bool isLinked { get; }

    internal abstract ImmutableArray<byte> publicKey { get; }

    internal abstract ImmutableArray<ModuleSymbol> modules { get; }

    internal abstract NamedTypeSymbol LookupDeclaredOrForwardedTopLevelMetadataType(
        ref MetadataTypeName emittedName,
        ConsList<AssemblySymbol> visitedAssemblies);

    internal abstract bool GetGuidString(out string guidString);

    internal abstract AssemblyMetadata GetMetadata();

    internal abstract ImmutableArray<AssemblySymbol> GetNoPiaResolutionAssemblies();

    internal abstract void SetNoPiaResolutionAssemblies(ImmutableArray<AssemblySymbol> assemblies);

    internal abstract ImmutableArray<AssemblySymbol> GetLinkedReferencedAssemblies();

    internal abstract void SetLinkedReferencedAssemblies(ImmutableArray<AssemblySymbol> assemblies);

    internal abstract IEnumerable<ImmutableArray<byte>> GetInternalsVisibleToPublicKeys(string simpleName);

    internal abstract IEnumerable<string> GetInternalsVisibleToAssemblyNames();

    internal abstract bool AreInternalsVisibleToThisAssembly(AssemblySymbol other);

    internal abstract IEnumerable<NamedTypeSymbol> GetAllTopLevelForwardedTypes();

    // internal virtual NamedTypeSymbol TryLookupForwardedMetadataTypeWithCycleDetection(
    //     ref MetadataTypeName emittedName,
    //     ConsList<AssemblySymbol> visitedAssemblies) {
    //     return null;
    // }
    internal abstract NamedTypeSymbol TryLookupForwardedMetadataTypeWithCycleDetection(ref MetadataTypeName emittedName, ConsList<AssemblySymbol>? visitedAssemblies);

    internal ErrorTypeSymbol CreateMultipleForwardingErrorTypeSymbol(
        ref MetadataTypeName emittedName,
        ModuleSymbol forwardingModule,
        AssemblySymbol destination1,
        AssemblySymbol destination2) {
        // TODO Error
        // var diagnosticInfo = new CSDiagnosticInfo(ErrorCode.ERR_TypeForwardedToMultipleAssemblies, forwardingModule, this, emittedName.FullName, destination1, destination2);
        return new MissingMetadataTypeSymbol.TopLevel(forwardingModule, ref emittedName, null);
    }

    internal ErrorTypeSymbol CreateCycleInTypeForwarderErrorTypeSymbol(ref MetadataTypeName emittedName) {
        // TODO error
        // DiagnosticInfo diagnosticInfo = new CSDiagnosticInfo(ErrorCode.ERR_CycleInTypeForwarder, emittedName.FullName, this.Name);
        return new MissingMetadataTypeSymbol.TopLevel(modules[0], ref emittedName, null);
    }

    internal override TResult Accept<TArgument, TResult>(
        SymbolVisitor<TArgument, TResult> visitor,
        TArgument argument) {
        return visitor.VisitAssembly(this, argument);
    }
}
