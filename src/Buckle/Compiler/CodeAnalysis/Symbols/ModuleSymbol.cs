using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class ModuleSymbol : Symbol {
    internal ModuleSymbol() { }

    public sealed override SymbolKind kind => SymbolKind.Module;

    internal abstract NamespaceSymbol globalNamespace { get; }

    internal override AssemblySymbol containingAssembly => (AssemblySymbol)containingSymbol;

    internal abstract int ordinal { get; }

    internal abstract bool bit32Required { get; }

    internal abstract bool isMissing { get; }

    internal sealed override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal sealed override bool isStatic => false;

    internal sealed override bool isVirtual => false;

    internal sealed override bool isOverride => false;

    internal sealed override bool isAbstract => false;

    internal sealed override bool isSealed => false;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal abstract override ImmutableArray<TextLocation> locations { get; }

    internal abstract ImmutableArray<AssemblyIdentity> referencedAssemblies { get; }

    internal abstract ImmutableArray<AssemblySymbol> referencedAssemblySymbols { get; }

    internal AssemblySymbol GetReferencedAssemblySymbol(int referencedAssemblyIndex) {
        var referencedAssemblies = referencedAssemblySymbols;

        if (referencedAssemblyIndex < referencedAssemblies.Length)
            return referencedAssemblies[referencedAssemblyIndex];

        var assembly = containingAssembly;

        // TODO confirm condition
        // if ((object)assembly != assembly.CorLibrary) {
        if (assembly.declaringCompilation.assemblyName != "CorLibrary")
            throw new ArgumentOutOfRangeException(nameof(referencedAssemblyIndex));

        return null;
    }

    internal abstract bool hasUnifiedReferences { get; }

    internal abstract ICollection<string> typeNames { get; }

    internal abstract ICollection<string> namespaceNames { get; }

    internal abstract bool hasAssemblyCompilationRelaxationsAttribute { get; }

    internal abstract bool hasAssemblyRuntimeCompatibilityAttribute { get; }

    internal abstract bool useUpdatedEscapeRules { get; }

    internal abstract CharSet? defaultMarshallingCharSet { get; }

    internal abstract bool areLocalsZeroed { get; }

    internal abstract void SetReferences(
        ModuleReferences<AssemblySymbol> moduleReferences,
        SourceAssemblySymbol originatingSourceAssemblyDebugOnly = null);

    internal abstract NamedTypeSymbol LookupTopLevelMetadataType(ref MetadataTypeName emittedName);

    internal virtual ImmutableArray<byte> GetHash(AssemblyHashAlgorithm algorithmId) {
        throw ExceptionUtilities.Unreachable();
    }

    internal NamespaceSymbol GetModuleNamespace(NamespaceSymbol namespaceSymbol) {
        if (namespaceSymbol is null)
            throw new ArgumentNullException(nameof(namespaceSymbol));

        // TODO Condition
        if (namespaceSymbol.extent.kind == NamespaceKind.Assembly &&
            namespaceSymbol.containingAssembly == containingAssembly) {
            return namespaceSymbol;
        }

        if (namespaceSymbol.isGlobalNamespace || namespaceSymbol.containingNamespace is null) {
            return globalNamespace;
        } else {
            var cns = GetModuleNamespace(namespaceSymbol.containingNamespace);

            if (cns is not null)
                return cns.GetNestedNamespace(namespaceSymbol.name);

            return null;
        }
    }

    internal abstract ModuleMetadata GetMetadata();

    internal override TResult Accept<TArgument, TResult>(SymbolVisitor<TArgument, TResult> visitor, TArgument argument) {
        return visitor.VisitModule(this, argument);
    }
}
