using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Diagnostics;
using Xunit;

namespace Buckle.Tests.CodeAnalysis;

public sealed class CSharpEmitterTests {
    [Theory]
    [InlineData(
        /* Belte Code */
@"void Main() { }",
        /* C# Code */
@"using System;
using System.Collections.Generic;

namespace CSharpEmitterTests;

public static class Program {

    public static void Main() {
        return;
    }

}
"
    )]
    public void Emitter_EmitsCorrectly(string text, string expectedText) {
        AssertText(text, expectedText);
    }

    private void AssertText(string text, string expectedText) {
        var syntaxTree = SyntaxTree.Parse(text);
        var compilation = Compilation.Create(syntaxTree);
        var result = compilation.EmitToString(BuildMode.CSharpTranspile, "CSharpEmitterTests", false);

        Assert.Empty(compilation.diagnostics.FilterOut(DiagnosticType.Warning).ToArray());
        Assert.Equal(expectedText, result);
    }
}
