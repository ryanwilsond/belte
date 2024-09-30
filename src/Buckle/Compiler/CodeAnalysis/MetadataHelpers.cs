using System.Collections.Immutable;
using System.Text;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal static class MetadataHelpers {
    internal const string DotDelimiterString = ".";
    internal const char CommaDelimiter = ',';
    internal const char MangledNameRegionStartChar = '<';
    internal const char MangledNameRegionEndChar = '>';

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

        return string.Concat(qualifier, DotDelimiterString, name);
    }
}
