using System.Runtime.InteropServices;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class DllImportData : IPlatformInvokeInformation {
    private readonly MethodImportAttributes _flags;

    internal DllImportData(string moduleName, string entryPointName, MethodImportAttributes flags) {
        this.moduleName = moduleName;
        this.entryPointName = entryPointName;
        _flags = flags;
    }

    public string moduleName { get; }

    public string entryPointName { get; }

    MethodImportAttributes IPlatformInvokeInformation.flags => _flags;

    internal bool exactSpelling => (_flags & MethodImportAttributes.ExactSpelling) != 0;

    internal CharSet characterSet {
        get {
            switch (_flags & MethodImportAttributes.CharSetMask) {
                case MethodImportAttributes.CharSetAnsi:
                    return CharSet.Ansi;
                case MethodImportAttributes.CharSetUnicode:
                    return CharSet.Unicode;
                case MethodImportAttributes.CharSetAuto:
                    return (CharSet)4;
                case 0:
                    return (CharSet)1;
            }

            throw ExceptionUtilities.UnexpectedValue(_flags);
        }
    }

    internal bool setLastError => (_flags & MethodImportAttributes.SetLastError) != 0;

    internal CallingConvention callingConvention {
        get {
            switch (_flags & MethodImportAttributes.CallingConventionMask) {
                default:
                    return CallingConvention.Winapi;
                case MethodImportAttributes.CallingConventionCDecl:
                    return CallingConvention.Cdecl;
                case MethodImportAttributes.CallingConventionStdCall:
                    return CallingConvention.StdCall;
                case MethodImportAttributes.CallingConventionThisCall:
                    return CallingConvention.ThisCall;
                case MethodImportAttributes.CallingConventionFastCall:
                    return CallingConvention.FastCall;
            }
        }
    }

    internal bool? bestFitMapping {
        get {
            switch (_flags & MethodImportAttributes.BestFitMappingMask) {
                case MethodImportAttributes.BestFitMappingEnable:
                    return true;
                case MethodImportAttributes.BestFitMappingDisable:
                    return false;
                default:
                    return null;
            }
        }
    }

    internal bool? throwOnUnmappableCharacter {
        get {
            switch (_flags & MethodImportAttributes.ThrowOnUnmappableCharMask) {
                case MethodImportAttributes.ThrowOnUnmappableCharEnable:
                    return true;
                case MethodImportAttributes.ThrowOnUnmappableCharDisable:
                    return false;
                default:
                    return null;
            }
        }
    }

    internal static MethodImportAttributes MakeFlags(
        bool exactSpelling,
        CharSet charSet,
        bool setLastError,
        CallingConvention callingConvention,
        bool? useBestFit,
        bool? throwOnUnmappable) {
        MethodImportAttributes result = 0;

        if (exactSpelling)
            result |= MethodImportAttributes.ExactSpelling;

        switch (charSet) {
            case CharSet.Ansi:
                result |= MethodImportAttributes.CharSetAnsi;
                break;
            case CharSet.Unicode:
                result |= MethodImportAttributes.CharSetUnicode;
                break;
            case (CharSet)4:
                result |= MethodImportAttributes.CharSetAuto;
                break;
        }

        if (setLastError)
            result |= MethodImportAttributes.SetLastError;

        switch (callingConvention) {
            default:
                result |= MethodImportAttributes.CallingConventionWinApi;
                break;
            case CallingConvention.Cdecl:
                result |= MethodImportAttributes.CallingConventionCDecl;
                break;
            case CallingConvention.StdCall:
                result |= MethodImportAttributes.CallingConventionStdCall;
                break;
            case CallingConvention.ThisCall:
                result |= MethodImportAttributes.CallingConventionThisCall;
                break;
            case CallingConvention.FastCall:
                result |= MethodImportAttributes.CallingConventionFastCall;
                break;
        }

        if (throwOnUnmappable.HasValue) {
            if (throwOnUnmappable.Value)
                result |= MethodImportAttributes.ThrowOnUnmappableCharEnable;
            else
                result |= MethodImportAttributes.ThrowOnUnmappableCharDisable;
        }

        if (useBestFit.HasValue) {
            if (useBestFit.Value)
                result |= MethodImportAttributes.BestFitMappingEnable;
            else
                result |= MethodImportAttributes.BestFitMappingDisable;
        }

        return result;
    }
}
