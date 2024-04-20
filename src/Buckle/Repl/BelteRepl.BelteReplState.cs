using System.Collections.Generic;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Repl.Themes;

namespace Repl;

public sealed partial class BelteRepl {
    /// <summary>
    /// Repl specific state, maintained throughout instance, recreated every instance.
    /// </summary>
    internal sealed class BelteReplState {
        /// <summary>
        /// Show the lexed syntax tokens after a submission.
        /// </summary>
        internal bool showTokens = false;

        /// <summary>
        /// Show the parse tree after a submission.
        /// </summary>
        internal bool showTree = false;

        /// <summary>
        /// Show the lowered code after a submission.
        /// </summary>
        internal bool showProgram = false;

        /// <summary>
        /// Show the IL code after a submission.
        /// </summary>
        internal bool showIL = false;

        /// <summary>
        /// Show compiler produced warnings.
        /// </summary>
        internal bool showWarnings = false;

        /// <summary>
        /// If to ignore statements with side effects (Print, PrintLine, etc.).
        /// </summary>
        internal bool loadingSubmissions = false;

        /// <summary>
        /// What color theme to use (can change).
        /// </summary>
        internal ColorTheme colorTheme = new DarkTheme();

        /// <summary>
        /// Current <see cref="Page" /> the user is viewing.
        /// </summary>
        internal Page currentPage = Page.Repl;

        /// <summary>
        /// Previous <see cref="Compilation" /> (used to build of previous).
        /// </summary>
        internal Compilation previous;

        /// <summary>
        /// Current tree representation of the most recent submission.
        /// </summary>
        internal SyntaxTree tree;

        /// <summary>
        /// Current defined variables.
        /// Not tracked after Repl instance is over, instead previous submissions are reevaluated.
        /// </summary>
        internal Dictionary<IVariableSymbol, IEvaluatorObject> variables;
    }
}
