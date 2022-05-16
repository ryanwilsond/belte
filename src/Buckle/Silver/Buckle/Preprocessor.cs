using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Buckle.CodeAnalysis.Text;

namespace Buckle;

public abstract class PreprocessLine {
    public TextLine text;

    public PreprocessLine() { }
}

public sealed class PreprocessIf : PreprocessLine {
    // includes elif, else, and end
}

public sealed class PreprocessPragma : PreprocessLine { }

public sealed class PreprocessDefine : PreprocessLine { }

public sealed class PreprocessUndefine : PreprocessLine { }

public sealed class PreprocessRun : PreprocessLine { }

public sealed class PreprocessSand : PreprocessLine { }

public sealed class PreprocessFile {
    public ImmutableArray<PreprocessLine> lines = new ImmutableArray<PreprocessLine>();

    private PreprocessFile() { }

    public static PreprocessFile Parse(ImmutableArray<TextLine> lines) {
        // ! just so it compiles
        return new PreprocessFile();
    }
}

public class Preprocessor {
    private Dictionary<string, string> symbols = new Dictionary<string, string>();

    public string PreprocessText(string fileName, string text) {
        var sourceText = SourceText.From(text, fileName);
        var lines = sourceText.lines;

        var preprocessFile = PreprocessFile.Parse(lines);

        foreach (var line in preprocessFile.lines) {

        }


        // ! just so it compiles
        return text;
    }
}
