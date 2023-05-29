using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
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
    /// The unparsed <see cref="SyntaxTree" /> containing the <see cref="Text.SourceText" />.
    /// </param>
    /// <param name="options">Compilation options to pass onto the created compilation scripts.</param>
    /// <param name="variables">And predefined variables.</param>
    /// <param name="abort">Flag that tells the interpreter to abort and safely exit as soon as possible.</param>
    /// <returns>The result of the last evaluated member.</returns>
    internal static EvaluationResult Interpret(
        SyntaxTree syntaxTree, CompilationOptions options,
        Dictionary<IVariableSymbol, IEvaluatorObject> variables, ValueWrapper<bool> abort) {
        var parsedSyntaxTree = SyntaxTree.Parse(syntaxTree.text);
        Compilation previous = null;
        EvaluationResult result = null;

        foreach (var member in parsedSyntaxTree.GetCompilationUnitRoot().members) {
            var newSyntaxTree = SyntaxTree.Create(
                SyntaxFactory.CompilationUnit(
                    new SyntaxList<MemberSyntax>(member),
                    parsedSyntaxTree.endOfFile
                )
            );

            previous = Compilation.CreateScript(options, previous, newSyntaxTree);
            result = previous.Evaluate(variables, abort);
        }

        return result;
    }
}
