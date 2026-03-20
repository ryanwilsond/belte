using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class RefSafetyAnalysis {
    private readonly struct MixableDestination {
        internal MixableDestination(ParameterSymbol parameter, BoundExpression argument) {
            this.argument = argument;
            this.parameter = parameter;
            escapeLevel = GetParameterValEscapeLevel(parameter)!.Value;
        }

        internal MixableDestination(BoundExpression argument, EscapeLevel escapeLevel) {
            this.argument = argument;
            parameter = null;
            this.escapeLevel = escapeLevel;
        }

        internal BoundExpression argument { get; }

        internal ParameterSymbol parameter { get; }

        internal EscapeLevel escapeLevel { get; }

        internal bool IsAssignableFrom(EscapeLevel level) => escapeLevel switch {
            EscapeLevel.CallingMethod => level == EscapeLevel.CallingMethod,
            EscapeLevel.ReturnOnly => true,
            _ => throw ExceptionUtilities.UnexpectedValue(escapeLevel)
        };

        public override string? ToString() => (parameter, argument, escapeLevel).ToString();
    }
}
