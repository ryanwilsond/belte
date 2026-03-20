using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class RefSafetyAnalysis {
    private readonly struct EscapeValue {
        internal EscapeValue(
            ParameterSymbol parameter,
            BoundExpression argument,
            EscapeLevel escapeLevel,
            bool isRefEscape) {
            this.argument = argument;
            this.parameter = parameter;
            this.escapeLevel = escapeLevel;
            this.isRefEscape = isRefEscape;
        }

        internal ParameterSymbol parameter { get; }

        internal BoundExpression argument { get; }

        internal EscapeLevel escapeLevel { get; }

        internal bool isRefEscape { get; }

        public void Deconstruct(
            out ParameterSymbol parameter,
            out BoundExpression argument,
            out EscapeLevel escapeLevel,
            out bool isRefEscape) {
            parameter = this.parameter;
            argument = this.argument;
            escapeLevel = this.escapeLevel;
            isRefEscape = this.isRefEscape;
        }

        public override string? ToString() => parameter is { } p
            ? p.ToString()
            : argument.ToString();
    }
}
