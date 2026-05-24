using Mono.Cecil.Cil;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class CecilILBuilder {
    private class TryFrame {
        internal Instruction outerTryStart;
        internal Instruction outerTryEnd;

        internal Instruction innerTryStart;
        internal Instruction innerTryEnd;

        internal Instruction handlerStart;
        internal Instruction handlerEnd;

        internal Instruction finallyStart;
        internal Instruction finallyEnd;

        internal Instruction leaveTarget;

        internal bool hasCatch;
        internal bool hasFinally;
    }
}
