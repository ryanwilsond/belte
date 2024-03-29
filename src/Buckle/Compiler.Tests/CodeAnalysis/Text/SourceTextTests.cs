using Buckle.CodeAnalysis.Text;
using Xunit;

namespace Buckle.Tests.CodeAnalysis.Text;

/// <summary>
/// Tests on the <see cref="SourceText" /> class.
/// </summary>
public sealed class SourceTextTests {
    [Theory]
    [InlineData(".", 1)]
    [InlineData(".\r\n", 2)]
    [InlineData(".\r\n\r\n", 3)]
    public void SourceText_IncludesLastLine(string text, int expectedLineCount) {
        var sourceText = SourceText.From(text);
        Assert.Equal(expectedLineCount, sourceText.lineCount);
    }
}
