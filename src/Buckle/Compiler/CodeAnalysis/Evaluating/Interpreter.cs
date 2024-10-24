using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Shared;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Interprets the program by compiling and evaluating chunk by chunk, instead of compiling and then evaluating in
/// separate stages.
/// </summary>
internal sealed class Interpreter {
    /// <summary>
    /// Pseudo interprets the given unparsed <see cref="SyntaxTree" />.
    /// Does not run line-by-line like a traditional interpreter, but rather parses the entire file at once and runs
    /// each member one by one.
    /// </summary>
    /// <param name="syntaxTree">
    /// The unparsed <see cref="SyntaxTree" /> containing the <see cref="SourceText" />.
    /// </param>
    /// <param name="options">Compilation options to pass onto the created compilation scripts.</param>
    /// <param name="variables">And predefined variables.</param>
    /// <param name="abort">Flag that tells the interpreter to abort and safely exit as soon as possible.</param>
    /// <returns>The result of the last evaluated member.</returns>
    internal static EvaluationResult Interpret(
        SyntaxTree syntaxTree,
        CompilationOptions options,
        Dictionary<IDataContainerSymbol, EvaluatorObject> variables,
        ValueWrapper<bool> abort) {
        // This pseudo interpreter parses all of the source files at once, so there is a short delay before running the
        // code. This is not perfect, as the goal is to be a "true" interpreter, but without doing this at once the
        // parser would have to be written to support partial parsing. This would be a large undertaking, but maybe
        // could be done in the future.
        var parsedSyntaxTree = SyntaxTree.Parse(syntaxTree.text);
        // This represents how much of the text has been evaluated. For diagnostics to have the correct location, each
        // compilation needs to have a copy of the text starting at this index.
        var textOffset = 0;

        EvaluationResult result = null;
        Compilation previous = null;

        foreach (var member in parsedSyntaxTree.GetCompilationUnitRoot().members) {
            textOffset += member.position;

            var newSyntaxTree = SyntaxTree.Create(
                syntaxTree.text.GetSubText(new TextSpan(textOffset, syntaxTree.text.length - textOffset)),
                SyntaxFactory.CompilationUnit(
                    new SyntaxList<MemberDeclarationSyntax>(member),
                    parsedSyntaxTree.endOfFile
                )
            );

            previous = Compilation.CreateScript(options, previous, newSyntaxTree);
            result = previous.Evaluate(variables, abort);

            // ? If any diagnostics are found, we quit early. Is this what we want though?
            if (result.diagnostics.AnyErrors())
                break;
        }

        return result;
    }
}
