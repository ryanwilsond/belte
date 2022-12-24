using System;
using System.Collections.Generic;
using Xunit;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.Tests.CodeAnalysis.Syntax;

public class SyntaxFactTests {
    [Theory]
    [MemberData(nameof(GetSyntaxTypeData))]
    internal void SyntaxFact_GetText_RoundTrips(SyntaxKind kind) {
        var text = SyntaxFacts.GetText(kind);
        if (text == null)
            return;

        var tokens = SyntaxTree.ParseTokens(text);
        var token = Assert.Single(tokens);
        Assert.Equal(kind, token.kind);
        Assert.Equal(text, token.text);
    }

    public static IEnumerable<object[]> GetSyntaxTypeData() {
        var types = (SyntaxKind[])Enum.GetValues(typeof(SyntaxKind));
        foreach (var type in types)
            yield return new object[] { type };
    }
}
