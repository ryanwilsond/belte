using System.Reflection;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal static class ModuleExtensions {
    internal static bool ShouldImportField(FieldAttributes flags, MetadataImportOptions importOptions) {
        switch (flags & FieldAttributes.FieldAccessMask) {
            case FieldAttributes.Private:
            case FieldAttributes.PrivateScope:
                return importOptions == MetadataImportOptions.All;
            case FieldAttributes.Assembly:
                return importOptions >= MetadataImportOptions.Internal;
            default:
                return true;
        }
    }
}
