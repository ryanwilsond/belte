using System;
using System.Reflection;

namespace Buckle.CodeAnalysis.Symbols;

internal class CommonAssemblyWellKnownAttributeData<TNamedTypeSymbol> : WellKnownAttributeData {
    internal Version assemblyVersionAttributeSetting { get; set; }

    internal string assemblyCultureAttributeSetting { get; set; }

    internal string assemblyKeyFileAttributeSetting { get; set; }

    internal string assemblyKeyContainerAttributeSetting { get; set; }

    internal string assemblySignatureKeyAttributeSetting { get; set; }

    internal AssemblyFlags assemblyFlagsAttributeSetting { get; set; }
}
