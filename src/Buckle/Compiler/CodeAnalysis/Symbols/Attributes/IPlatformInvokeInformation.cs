
namespace Buckle.CodeAnalysis.Symbols;

internal interface IPlatformInvokeInformation {
    string moduleName { get; }

    string entryPointName { get; }

    MethodImportAttributes flags { get; }
}
