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
    internal static EvaluationResult Interpret(
        SyntaxTree syntaxTree, Dictionary<IVariableSymbol, IEvaluatorObject> variables, ValueWrapper<bool> abort) {
        // TODO
        return null;
    }
}
