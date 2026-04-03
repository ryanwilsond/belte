using Buckle.CodeAnalysis.Evaluating;
using Buckle.Utilities;
using StackBehaviour = System.Reflection.Emit.StackBehaviour;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal static partial class OpCodeExtensions {
    // TODO Hardcode a map for the delta instead of computing
    internal static int StackOffset(this OpCode opCode) {
        var refOpCode = RefILBuilder.ConvertToRef(opCode);
        var push = GetPushCount(refOpCode.StackBehaviourPush);
        var pop = GetPopCount(refOpCode.StackBehaviourPop);
        return push - pop;
    }

    private static int GetPopCount(StackBehaviour pop) {
        return pop switch {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 => 1,
            StackBehaviour.Pop1_pop1 => 2,
            StackBehaviour.Popi => 1,
            StackBehaviour.Popi_pop1 => 2,
            StackBehaviour.Popi_popi => 2,
            StackBehaviour.Popi_popi8 => 2,
            StackBehaviour.Popi_popi_popi => 3,
            StackBehaviour.Popi_popr4 => 2,
            StackBehaviour.Popi_popr8 => 2,
            // StackBehaviour.Popi_popref => 2,
            StackBehaviour.Popref => 1,
            StackBehaviour.Popref_pop1 => 2,
            StackBehaviour.Popref_popi => 2,
            StackBehaviour.Popref_popi_popi => 3,
            StackBehaviour.Popref_popi_popi8 => 3,
            StackBehaviour.Popref_popi_popr4 => 3,
            StackBehaviour.Popref_popi_popr8 => 3,
            StackBehaviour.Popref_popi_popref => 3,
            // StackBehaviour.Varpop => -1,
            _ => throw ExceptionUtilities.UnexpectedValue(pop)
        };
    }

    private static int GetPushCount(StackBehaviour push) {
        return push switch {
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1 => 1,
            StackBehaviour.Push1_push1 => 2,
            StackBehaviour.Pushi => 1,
            StackBehaviour.Pushi8 => 1,
            StackBehaviour.Pushr4 => 1,
            StackBehaviour.Pushr8 => 1,
            StackBehaviour.Pushref => 1,
            // StackBehaviour.Varpush => -1,
            _ => throw ExceptionUtilities.UnexpectedValue(push)
        };
    }

    internal static OperandKind ToOperandKind(this OpCode opCode) {
        return opCode switch {
            OpCode.Box => OperandKind.TypeTok,
            OpCode.Call => OperandKind.Method,
            OpCode.Calli => OperandKind.Callsitedescr,
            OpCode.Callvirt => OperandKind.Method,
            OpCode.Castclass => OperandKind.Class,
            OpCode.Constrained => OperandKind.TypeTok,
            OpCode.Cpobj => OperandKind.TypeTok,
            OpCode.Initobj => OperandKind.TypeTok,
            OpCode.Isinst => OperandKind.Class,
            OpCode.Ldarg => OperandKind.UInt16,
            OpCode.Ldarg_S => OperandKind.UInt8,
            OpCode.Ldarga => OperandKind.UInt16,
            OpCode.Ldarga_S => OperandKind.UInt8,
            OpCode.Ldc_I4 => OperandKind.Int32,
            OpCode.Ldc_I4_S => OperandKind.Int8,
            OpCode.Ldc_I8 => OperandKind.Int64,
            OpCode.Ldc_R4 => OperandKind.Float32,
            OpCode.Ldc_R8 => OperandKind.Float64,
            OpCode.Ldelem => OperandKind.TypeTok,
            OpCode.Ldelema => OperandKind.Class,
            OpCode.Ldfld => OperandKind.Field,
            OpCode.Ldflda => OperandKind.Field,
            OpCode.Ldftn => OperandKind.Method,
            OpCode.Ldloc => OperandKind.UInt16,
            OpCode.Ldloca => OperandKind.UInt16,
            OpCode.Ldloca_S => OperandKind.UInt8,
            OpCode.Ldobj => OperandKind.TypeTok,
            OpCode.Ldsfld => OperandKind.Field,
            OpCode.Ldsflda => OperandKind.Field,
            OpCode.Ldstr => OperandKind.String,
            OpCode.Ldtoken => OperandKind.Token,
            OpCode.Ldvirtftn => OperandKind.Method,
            OpCode.Mkrefany => OperandKind.Class,
            OpCode.Newarr => OperandKind.TypeTok,
            OpCode.Newobj => OperandKind.Ctor,
            OpCode.Refanyval => OperandKind.TypeTok,
            OpCode.Sizeof => OperandKind.TypeTok,
            OpCode.Stelem => OperandKind.TypeTok,
            OpCode.Stfld => OperandKind.Field,
            OpCode.Stloc => OperandKind.UInt16,
            OpCode.Stloc_S => OperandKind.UInt8,
            OpCode.Stobj => OperandKind.TypeTok,
            OpCode.Stsfld => OperandKind.Field,
            OpCode.Unbox => OperandKind.ValueType,
            OpCode.Unbox_Any => OperandKind.TypeTok,
            _ => OperandKind.None,
        };
    }
}
