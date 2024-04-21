using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Xunit;

namespace Buckle.Tests.CodeAnalysis.Syntax;

/// <summary>
/// Tests on the <see cref="SyntaxFacts" /> class.
/// </summary>
public sealed class SyntaxFactTests {
    [Theory]
    [MemberData(nameof(GetSyntaxTypeData))]
    internal void SyntaxFact_GetText_RoundTrips(SyntaxKind kind) {
        var text = SyntaxFacts.GetText(kind);
        if (text is null)
            return;

        var tokens = SyntaxTreeExtensions.ParseTokens(text);
        Assert.Equal(1, tokens.Count);
        var token = tokens[0];
        Assert.Equal(kind, token.kind);
        Assert.Equal(text, token.text);
    }

    public static IEnumerable<object[]> GetSyntaxTypeData() {
        var types = Enum.GetValues(typeof(SyntaxKind))
            .Cast<SyntaxKind>()
            .Where(k => k is not SyntaxKind.GreaterThanGreaterThanToken
                         and not SyntaxKind.GreaterThanGreaterThanGreaterThanToken)
            .ToArray();

        foreach (var type in types)
            yield return new object[] { type };
    }
}
