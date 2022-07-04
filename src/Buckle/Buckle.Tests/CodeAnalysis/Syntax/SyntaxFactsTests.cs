using System;
using System.Collections.Generic;
using Xunit;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.Tests.CodeAnalysis.Syntax;

public class SyntaxFactTests {
    [Theory]
    [MemberData(nameof(GetSyntaxTypeData))]
    internal void SyntaxFact_GetText_RoundTrips(SyntaxType type) {
        var text = SyntaxFacts.GetText(type);
        if (text == null)
            return;

        var tokens = SyntaxTree.ParseTokens(text);
        var token = Assert.Single(tokens);
        Assert.Equal(type, token.type);
        Assert.Equal(text, token.text);
    }

    public static IEnumerable<object[]> GetSyntaxTypeData() {
        var types = (SyntaxType[])Enum.GetValues(typeof(SyntaxType));
        foreach (var type in types)
            yield return new object[] { type };
    }
}
