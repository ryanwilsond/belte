using System;
using System.Globalization;
using System.Reflection;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal static class ModuleExtensions {
    private const string VTableGapMethodNamePrefix = "_VtblGap";

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

    internal static int GetVTableGapSize(string emittedMethodName) {
        const string Prefix = VTableGapMethodNamePrefix;

        if (emittedMethodName.StartsWith(Prefix, StringComparison.Ordinal)) {
            int index;

            for (index = Prefix.Length; index < emittedMethodName.Length; index++) {
                if (!char.IsDigit(emittedMethodName, index))
                    break;
            }

            if (index == Prefix.Length ||
                index >= emittedMethodName.Length - 1 ||
                emittedMethodName[index] != '_' ||
                !char.IsDigit(emittedMethodName, index + 1)) {
                return 1;
            }

            if (int.TryParse(
                    emittedMethodName.Substring(index + 1),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var countOfSlots)
                && countOfSlots > 0) {
                return countOfSlots;
            }

            return 1;
        }

        return 0;
    }
}
