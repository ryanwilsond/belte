using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class RefSafetyAnalysis {
    private readonly struct EscapeArgument {
        internal EscapeArgument(ParameterSymbol? parameter, BoundExpression argument, RefKind refKind) {
            this.argument = argument;
            this.parameter = parameter;
            this.refKind = refKind;
        }

        internal ParameterSymbol parameter { get; }

        internal BoundExpression argument { get; }

        internal RefKind refKind { get; }

        public void Deconstruct(out ParameterSymbol parameter, out BoundExpression argument, out RefKind refKind) {
            parameter = this.parameter;
            argument = this.argument;
            refKind = this.refKind;
        }

        public override string? ToString() => parameter is { } p
            ? p.ToString()
            : argument.ToString();
    }
}
