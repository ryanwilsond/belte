using System;
using System.Collections.Generic;
using System.Reflection;

namespace Buckle.CodeAnalysis.Symbols;

internal class CommonAssemblyWellKnownAttributeData<TNamedTypeSymbol> : WellKnownAttributeData {
    internal Version assemblyVersionAttributeSetting { get; set; }

    internal string assemblyCultureAttributeSetting { get; set; }

    internal string assemblyKeyFileAttributeSetting { get; set; }

    internal string assemblyKeyContainerAttributeSetting { get; set; }

    internal string assemblySignatureKeyAttributeSetting { get; set; }

    internal AssemblyFlags assemblyFlagsAttributeSetting { get; set; }

    internal string guidAttribute { get; set; }

    internal HashSet<TNamedTypeSymbol> forwardedTypes { get; set; }
}
