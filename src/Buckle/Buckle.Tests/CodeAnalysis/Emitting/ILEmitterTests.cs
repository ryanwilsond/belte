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
<Program>$ {
    System.Void <Program>$::Main() {
        IL_0000: ret
    }
}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
int Main() {
    return 0;
}
        ",
        /* IL Code */
        @"
<Program>$ {
    System.Int32 <Program>$::Main() {
        IL_0000: ldc.i4.0
        IL_0001: ret
    }
}
        "
    )]
    [InlineData(
        /* Belte Code */
        @"
int Main() {
    return null;
}
        ",
        /* IL Code */
        @"
<Program>$ {
    System.Int32 <Program>$::Main() {
        IL_0000: ldc.i4.0
        IL_0001: ret
    }
}
        "
    )]
    public void Emitter_Emits_CorrectText(string text, string expectedText) {
        AssertText(text, expectedText.Trim() + Environment.NewLine, BuildMode.Dotnet);
    }
}
