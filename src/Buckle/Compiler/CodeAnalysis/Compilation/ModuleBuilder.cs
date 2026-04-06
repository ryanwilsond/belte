using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal abstract class ModuleBuilder {
    internal bool hasGeneratedGlobalsClass;

    internal abstract void EmitGlobalsClass();

    internal abstract NamedTypeSymbol GetFixedImplementationType(SourceFixedFieldSymbol field);
}
