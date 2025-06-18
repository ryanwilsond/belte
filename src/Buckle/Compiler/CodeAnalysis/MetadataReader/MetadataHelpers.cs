using System;
using System.Collections.Immutable;
using System.Text;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal static class MetadataHelpers {
    private static readonly ImmutableArray<ReadOnlyMemory<char>> SplitQualifiedNameSystemMemory
        = [SystemString.AsMemory()];

    internal const string DotDelimiterString = ".";
    internal const string SystemString = "System";
    internal const char GenericTypeNameManglingChar = '`';
    internal const char DotDelimiter = '.';
    internal const char CommaDelimiter = ',';
    internal const char MangledNameRegionStartChar = '<';
    internal const char MangledNameRegionEndChar = '>';

    internal const int MaxStringLengthForParamSize = 22;

    internal static bool IsValidPublicKey(ImmutableArray<byte> bytes) => CryptoBlobParser.IsValidPublicKey(bytes);

    internal static ImmutableArray<ReadOnlyMemory<char>> SplitQualifiedName(ReadOnlyMemory<char> name)
        => SplitQualifiedNameWorker(name, SplitQualifiedNameSystemMemory, static memory => memory);

    internal static string ComposeSuffixedMetadataName(string name, ImmutableArray<TemplateParameterSymbol> templates) {
        if (templates.Length == 0)
            return name;

        var builder = new StringBuilder(name);
        builder.Append(MangledNameRegionStartChar);

        for (var i = 0; i < templates.Length; i++) {
            if (i > 0)
                builder.Append(CommaDelimiter);

            builder.Append(templates[i].underlyingType.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat));
        }

        builder.Append(MangledNameRegionEndChar);
        return builder.ToString();
    }

    internal static string BuildQualifiedName(string qualifier, string name) {
        if (string.IsNullOrEmpty(qualifier))
            return name;

        if (qualifier.EndsWith("::"))
            return string.Concat(qualifier, name);

        return string.Concat(qualifier, DotDelimiterString, name);
    }

    internal static ReadOnlyMemory<char> SplitQualifiedName(string pstrName, out ReadOnlyMemory<char> qualifier) {
        var angleBracketDepth = 0;
        var delimiter = -1;

        for (var i = 0; i < pstrName.Length; i++) {
            switch (pstrName[i]) {
                case MangledNameRegionStartChar:
                    angleBracketDepth++;
                    break;
                case MangledNameRegionEndChar:
                    angleBracketDepth--;
                    break;
                case DotDelimiter:
                    if (angleBracketDepth == 0 && (i == 0 || delimiter < i - 1))
                        delimiter = i;

                    break;
            }
        }

        if (delimiter < 0) {
            qualifier = string.Empty.AsMemory();
            return pstrName.AsMemory();
        }

        if (delimiter == 6 && pstrName.StartsWith(SystemString, StringComparison.Ordinal))
            qualifier = SystemString.AsMemory();
        else
            qualifier = pstrName.AsMemory()[..delimiter];

        return pstrName.AsMemory()[(delimiter + 1)..];
    }

    internal static ImmutableArray<T> SplitQualifiedNameWorker<T>(
        ReadOnlyMemory<char> nameMemory,
        ImmutableArray<T> splitSystemString,
        Func<ReadOnlyMemory<char>, T> convert) {
        if (nameMemory.Length == 0)
            return [];

        var dots = 0;
        var nameSpan = nameMemory.Span;

        foreach (var ch in nameSpan) {
            if (ch == DotDelimiter)
                dots++;
        }

        if (dots == 0)
            return nameMemory.Span.SequenceEqual(SystemString.AsSpan()) ? splitSystemString : [convert(nameMemory)];

        var result = ArrayBuilder<T>.GetInstance(dots + 1);

        var start = 0;

        for (var i = 0; dots > 0; i++) {
            if (nameSpan[i] == DotDelimiter) {
                var len = i - start;

                if (len == 6 && start == 0 && nameSpan.StartsWith(SystemString.AsSpan(), StringComparison.Ordinal))
                    result.Add(convert(SystemString.AsMemory()));
                else
                    result.Add(convert(nameMemory.Slice(start, len)));

                dots--;
                start = i + 1;
            }
        }

        result.Add(convert(nameMemory[start..]));

        return result.ToImmutableAndFree();
    }

    internal static ReadOnlyMemory<char> InferTypeArityAndUnmangleMetadataName(
        ReadOnlyMemory<char> emittedTypeName,
        out short arity) {
        arity = InferTypeArityFromMetadataName(emittedTypeName.Span, out var suffixStartsAt);

        if (arity == 0)
            return emittedTypeName;

        return emittedTypeName[..suffixStartsAt];
    }

    internal static bool SplitNameEqualsFullyQualifiedName(
        string namespaceName,
        string typeName,
        string fullyQualified) {
        return fullyQualified.Length == namespaceName.Length + typeName.Length + 1 &&
               fullyQualified[namespaceName.Length] == DotDelimiter &&
               fullyQualified.StartsWith(namespaceName, StringComparison.Ordinal) &&
               fullyQualified.EndsWith(typeName, StringComparison.Ordinal);
    }

    private static short InferTypeArityFromMetadataName(ReadOnlySpan<char> emittedTypeName, out int suffixStartsAt) {
        var emittedTypeNameLength = emittedTypeName.Length;

        int indexOfManglingChar;
        for (indexOfManglingChar = emittedTypeNameLength; indexOfManglingChar >= 1; indexOfManglingChar--) {
            if (emittedTypeName[indexOfManglingChar - 1] == GenericTypeNameManglingChar) {
                break;
            }
        }

        if (indexOfManglingChar < 2 ||
           (emittedTypeNameLength - indexOfManglingChar) == 0 ||
           emittedTypeNameLength - indexOfManglingChar > MaxStringLengthForParamSize) {
            suffixStartsAt = -1;
            return 0;
        }

        if (TryScanArity(emittedTypeName[indexOfManglingChar..]) is not short arity) {
            suffixStartsAt = -1;
            return 0;
        }

        suffixStartsAt = indexOfManglingChar - 1;
        return arity;

        static short? TryScanArity(ReadOnlySpan<char> aritySpan) {
            if (aritySpan is { Length: >= 1 and <= 5 } and not ['0', ..]) {
                var intArity = 0;

                foreach (var digit in aritySpan) {
                    if (digit is < '0' or > '9')
                        return null;

                    intArity = intArity * 10 + (digit - '0');
                }

                if (intArity <= short.MaxValue)
                    return (short)intArity;
            }

            return null;
        }
    }
}
