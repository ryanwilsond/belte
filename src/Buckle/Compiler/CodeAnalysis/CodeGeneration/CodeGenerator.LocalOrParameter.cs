
namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class CodeGenerator {
    internal readonly struct LocalOrParameter {
        internal readonly VariableDefinition local;
        internal readonly int parameterIndex;

        private LocalOrParameter(VariableDefinition local, int parameterIndex) {
            this.local = local;
            this.parameterIndex = parameterIndex;
        }

        public static implicit operator LocalOrParameter(VariableDefinition local) {
            return new LocalOrParameter(local, -1);
        }

        public static implicit operator LocalOrParameter(int parameterIndex) {
            return new LocalOrParameter(null, parameterIndex);
        }
    }
}
