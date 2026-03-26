using Buckle.CodeAnalysis.Lowering;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static class GeneratedNames {
    internal static string MakeLambdaDisplayLocalName(int uniqueId) {
        return "BLT$" + "<>8__locals" + uniqueId;
    }

    internal static string MakeDisplayClassName(int methodId, int closureId) {
        var result = PooledStringBuilder.GetInstance();
        var builder = result.Builder;
        builder.Append('<');
        builder.Append('>');
        builder.Append("Closure");

        if (methodId >= 0 || closureId >= 0) {
            builder.Append("__");

            if (methodId >= 0)
                builder.Append(methodId);

            if (closureId >= 0) {
                if (methodId >= 0)
                    builder.Append('_');

                builder.Append(closureId);
            }
        }

        return result.ToStringAndFree();
    }

    internal static string MakeFixedFieldImplementationName(string fieldName) {
        return "<" + fieldName + ">e__FixedBuffer";
    }

    internal static string MakeClosureName(
        string topLevelMethodName,
        string localFunctionName,
        int topLevelMethodOrdinal,
        ClosureKind closureKind,
        int methodOrdinal) {
        var result = PooledStringBuilder.GetInstance();
        var builder = result.Builder;
        builder.Append('<');

        if (topLevelMethodName is not null)
            builder.Append(topLevelMethodName);

        builder.Append('>');

        switch (closureKind) {
            case ClosureKind.Static:
                builder.Append("ss");
                break;
            case ClosureKind.Singleton:
                builder.Append('s');
                break;
            case ClosureKind.ThisOnly:
                builder.Append('t');
                break;
            case ClosureKind.General:
                builder.Append('g');
                break;
        }

        if (localFunctionName is not null || topLevelMethodOrdinal >= 0 || methodOrdinal >= 0) {
            // '__' represents the suffix separator
            builder.Append("__");
            builder.Append(localFunctionName);
            // '|' represents the local function suffix terminator
            builder.Append('|');

            if (topLevelMethodOrdinal >= 0)
                builder.Append(topLevelMethodOrdinal);

            if (methodOrdinal >= 0) {
                if (methodOrdinal >= 0) {
                    // '_' represents the ID separator
                    builder.Append('_');
                }

                builder.Append(methodOrdinal);
            }
        }

        return result.ToStringAndFree();
    }

    internal static string MakeBaseMethodWrapperName(int uniqueId) {
        return "<>n__" + uniqueId;
    }

    internal static string MakeSynthedParameterName(int ordinal, TypeWithAnnotations paramType) {
        return paramType.type.name + "_p" + ordinal;
    }

    internal static string MakeSynthedLocalName(TypeWithAnnotations type, int ordinal) {
        return type.type.name + "_l" + ordinal;
    }
}
