using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class AssemblySymbol : Symbol {
    private static readonly ObjectPool<ArrayBuilder<AssemblySymbol>> SymbolPool
        = new ObjectPool<ArrayBuilder<AssemblySymbol>>(() => []);

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

    internal sealed override bool isExtern => false;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

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

    internal abstract NamedTypeSymbol LookupDeclaredTopLevelMetadataType(ref MetadataTypeName emittedName);

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
        throw Utilities.ExceptionUtilities.Unreachable();
        // var diagnosticInfo = new CSDiagnosticInfo(ErrorCode.ERR_TypeForwardedToMultipleAssemblies, forwardingModule, this, emittedName.FullName, destination1, destination2);
        // return new MissingMetadataTypeSymbol.TopLevel(forwardingModule, ref emittedName, null);
    }

    internal ErrorTypeSymbol CreateCycleInTypeForwarderErrorTypeSymbol(ref MetadataTypeName emittedName) {
        // TODO error
        throw Utilities.ExceptionUtilities.Unreachable();
        // DiagnosticInfo diagnosticInfo = new CSDiagnosticInfo(ErrorCode.ERR_CycleInTypeForwarder, emittedName.FullName, this.Name);
        // return new MissingMetadataTypeSymbol.TopLevel(modules[0], ref emittedName, null);
    }

    private static readonly char[] NestedTypeNameSeparators = ['+'];

    internal NamedTypeSymbol GetTypeByMetadataName(
        string metadataName,
        bool includeReferences,
        bool isWellKnownType,
        out (AssemblySymbol, AssemblySymbol) conflicts,
        bool useCLSCompliantNameArityEncoding = false,
        BelteDiagnosticQueue warnings = null) {
        NamedTypeSymbol type;
        MetadataTypeName mdName;

        if (metadataName.IndexOf('+') >= 0) {
            var parts = metadataName.Split(NestedTypeNameSeparators);
            mdName = MetadataTypeName.FromFullName(parts[0], useCLSCompliantNameArityEncoding);

            type = GetTopLevelTypeByMetadataName(
                ref mdName,
                assemblyOpt: null,
                includeReferences: includeReferences,
                isWellKnownType: isWellKnownType,
                conflicts: out conflicts,
                warnings: warnings
            );

            if (type is null)
                return null;

            Debug.Assert(!type.IsErrorType());

            for (var i = 1; i < parts.Length; i++) {
                mdName = MetadataTypeName.FromTypeName(parts[i]);
                type = type.LookupMetadataType(ref mdName);

                if (type is null)
                    return null;

                Debug.Assert(!type.IsErrorType());

                if (isWellKnownType && !IsValidWellKnownType(type))
                    return null;
            }
        } else {
            mdName = MetadataTypeName.FromFullName(metadataName, useCLSCompliantNameArityEncoding);

            type = GetTopLevelTypeByMetadataName(
                ref mdName,
                assemblyOpt: null,
                includeReferences: includeReferences,
                isWellKnownType: isWellKnownType,
                conflicts: out conflicts,
                warnings: warnings
            );
        }

        Debug.Assert(type?.IsErrorType() != true);

        return type;
    }

    private bool IsValidWellKnownType(NamedTypeSymbol? result) {
        if (result is null || result.typeKind == TypeKind.Error)
            return false;

        Debug.Assert((object)result.containingType is null || IsValidWellKnownType(result.containingType),
            "Checking the containing type is the caller's responsibility.");

        return result.declaredAccessibility == Accessibility.Public || IsSymbolAccessible(result, this);
    }

    internal NamedTypeSymbol GetTopLevelTypeByMetadataName(
        ref MetadataTypeName metadataName,
        AssemblyIdentity? assemblyOpt,
        bool includeReferences,
        bool isWellKnownType,
        out (AssemblySymbol, AssemblySymbol) conflicts,
        BelteDiagnosticQueue warnings = null) {
        Debug.Assert(warnings is null || isWellKnownType);

        conflicts = default;
        NamedTypeSymbol result;

        result = GetTopLevelTypeByMetadataName(this, ref metadataName, assemblyOpt);
        Debug.Assert(result?.IsErrorType() != true);

        if (isWellKnownType && !IsValidWellKnownType(result))
            result = null;

        if (result is not null || !includeReferences)
            return result;

        Debug.Assert(this is SourceAssemblySymbol,
            "Never include references for a non-source assembly, because they don't know about aliases."
        );

        var assemblies = SymbolPool.Allocate();

        if (assemblyOpt is not null)
            assemblies.AddRange(declaringCompilation.referenceManager.referencedAssemblies);
        else
            declaringCompilation.GetUnaliasedReferencedAssemblies(assemblies);

        foreach (var assembly in assemblies) {
            Debug.Assert(!(this is SourceAssemblySymbol && assembly.isMissing));

            var candidate = GetTopLevelTypeByMetadataName(assembly, ref metadataName, assemblyOpt);
            Debug.Assert(candidate?.IsErrorType() != true);

            if (!IsValidCandidate(candidate, isWellKnownType))
                continue;

            Debug.Assert(!TypeSymbol.Equals(candidate, result, TypeCompareKind.ConsiderEverything));

            if (result is not null) {
                if (warnings is null) {
                    conflicts = (result.containingAssembly, candidate.containingAssembly);
                    result = null;
                } else {
                    // TODO Warning
                    // The predefined type '{0}' is defined in multiple assemblies in the global alias; using definition from '{1}'
                    // warnings.Add(ErrorCode.WRN_MultiplePredefTypes, NoLocation.Singleton, result, result.ContainingAssembly);
                    throw ExceptionUtilities.Unreachable();
                }

                break;
            }

            result = candidate;
        }

        assemblies.Clear();

        SymbolPool.Free(assemblies);

        Debug.Assert(result?.IsErrorType() != true);
        return result;

        bool IsValidCandidate(NamedTypeSymbol candidate, bool isWellKnownType) {
            return candidate is not null
                && (!isWellKnownType || IsValidWellKnownType(candidate))
                && !candidate.IsHiddenByCodeAnalysisEmbeddedAttribute();
        }
    }

    private static NamedTypeSymbol GetTopLevelTypeByMetadataName(
        AssemblySymbol assembly,
        ref MetadataTypeName metadataName,
        AssemblyIdentity assemblyOpt) {
        if (assemblyOpt is not null && !assemblyOpt.Equals(assembly.identity))
            return null;

        var result = assembly.LookupDeclaredTopLevelMetadataType(ref metadataName);
        Debug.Assert(result?.IsErrorType() != true);
        Debug.Assert(result is null || ReferenceEquals(result.containingAssembly, assembly));

        return result;
    }

    internal NamespaceSymbol GetAssemblyNamespace(NamespaceSymbol namespaceSymbol) {
        if (namespaceSymbol.isGlobalNamespace)
            return globalNamespace;

        var container = namespaceSymbol.containingNamespace;

        if (container is null)
            return globalNamespace;

        if (namespaceSymbol.namespaceKind == NamespaceKind.Assembly && namespaceSymbol.containingAssembly == this)
            return namespaceSymbol;

        var assemblyContainer = GetAssemblyNamespace(container);

        if ((object)assemblyContainer == container)
            return namespaceSymbol;

        if (assemblyContainer is null)
            return null;

        return assemblyContainer.GetNestedNamespace(namespaceSymbol.name);
    }

    internal override void Accept(SymbolVisitor visitor) {
        visitor.VisitAssembly(this);
    }

    internal override TResult Accept<TArgument, TResult>(
        SymbolVisitor<TArgument, TResult> visitor,
        TArgument argument) {
        return visitor.VisitAssembly(this, argument);
    }
}
