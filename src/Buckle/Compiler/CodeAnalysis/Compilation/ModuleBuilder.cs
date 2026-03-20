
namespace Buckle.CodeAnalysis;

internal abstract class ModuleBuilder {
    internal bool hasGeneratedGlobalsClass;

    internal abstract void EmitGlobalsClass();
}
