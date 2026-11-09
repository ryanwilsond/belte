using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;

namespace Buckle.CodeAnalysis.Symbols;

#pragma warning disable CS0660

internal abstract partial class TypeSymbol {
    private class InterfaceInfo {
        internal ImmutableArray<NamedTypeSymbol> allInterfaces;
        internal MultiDictionary<NamedTypeSymbol, NamedTypeSymbol> interfacesAndTheirBaseInterfaces;
        internal static readonly MultiDictionary<NamedTypeSymbol, NamedTypeSymbol> EmptyInterfacesAndTheirBaseInterfaces =
            new MultiDictionary<NamedTypeSymbol, NamedTypeSymbol>(0, SymbolEqualityComparer.CLRSignature);

        private ConcurrentDictionary<Symbol, SymbolAndDiagnostics> _implementationForInterfaceMemberMap;

        public ConcurrentDictionary<Symbol, SymbolAndDiagnostics> implementationForInterfaceMemberMap {
            get {
                var map = _implementationForInterfaceMemberMap;

                if (map is not null)
                    return map;

                map = new ConcurrentDictionary<Symbol, SymbolAndDiagnostics>(
                    concurrencyLevel: 1,
                    capacity: 1,
                    comparer: SymbolEqualityComparer.ConsiderEverything
                );

                return Interlocked.CompareExchange(ref _implementationForInterfaceMemberMap, map, null) ?? map;
            }
        }

        internal MultiDictionary<Symbol, Symbol> explicitInterfaceImplementationMap;
        internal ImmutableDictionary<MethodSymbol, MethodSymbol> synthesizedMethodImplMap;

        internal bool IsDefaultValue() {
            return allInterfaces.IsDefault &&
                interfacesAndTheirBaseInterfaces is null &&
                _implementationForInterfaceMemberMap is null &&
                explicitInterfaceImplementationMap is null &&
                synthesizedMethodImplMap is null;
        }
    }
}
