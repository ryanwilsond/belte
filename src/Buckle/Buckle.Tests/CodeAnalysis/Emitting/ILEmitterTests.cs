using System;
using Xunit;
using static Buckle.Tests.Assertions;

namespace Buckle.Tests.CodeAnalysis.Emitting;

public sealed class ILEmitterTests {
    [Theory]
    [InlineData(
        /* Belte Code */
        @"
void Main() { }
        ",
        /* IL Code */
        @"
TODO
        "
    )]
    public void Emitter_Emits_CorrectText(string text, string expectedText) {
        AssertText(text, expectedText.Trim() + Environment.NewLine, BuildMode.Dotnet);
    }
}
