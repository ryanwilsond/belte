using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using StackBehaviour = System.Reflection.Emit.StackBehaviour;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal static partial class OpCodeExtensions {
    // TODO Hardcode a map for the delta instead of computing
    internal static int StackOffset(this OpCode opCode, MethodSymbol methodSymbolOpt) {
        var refOpCode = RefILBuilder.ConvertToRef(opCode);
        var push = GetPushCount(refOpCode.StackBehaviourPush);
        var pop = GetPopCount(refOpCode.StackBehaviourPop);

        if (push == -1) {
            if (opCode is OpCode.Call or OpCode.Calli or OpCode.Callvirt) {
                if (methodSymbolOpt is not null)
                    push = methodSymbolOpt.returnsVoid == true ? 0 : 1;
            } else {
                throw ExceptionUtilities.UnexpectedValue(opCode);
            }
        }

        if (pop == -1) {
            if (opCode is OpCode.Call or OpCode.Calli or OpCode.Callvirt) {
                if (methodSymbolOpt is not null) {
                    pop = methodSymbolOpt.parameterCount +
                        ((methodSymbolOpt.isStatic || methodSymbolOpt.methodKind == MethodKind.LocalFunction) ? 0 : 1);
                }
            } else {
                throw ExceptionUtilities.UnexpectedValue(opCode);
            }
        }

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
            StackBehaviour.Varpop => -1,
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
            StackBehaviour.Varpush => -1,
            _ => throw ExceptionUtilities.UnexpectedValue(push)
        };
    }

    internal static OperandKind ToOperandKind(this OpCode opCode) {
        return opCode switch {
            OpCode.Box => OperandKind.TypeToken,
            OpCode.Call => OperandKind.Method,
            OpCode.Calli => OperandKind.FunctionPointer,
            OpCode.Callvirt => OperandKind.Method,
            OpCode.Castclass => OperandKind.Class,
            OpCode.Constrained => OperandKind.TypeToken,
            OpCode.Cpobj => OperandKind.TypeToken,
            OpCode.Initobj => OperandKind.TypeToken,
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
            OpCode.Ldelem => OperandKind.TypeToken,
            OpCode.Ldelema => OperandKind.Class,
            OpCode.Ldfld => OperandKind.Field,
            OpCode.Ldflda => OperandKind.Field,
            OpCode.Ldftn => OperandKind.Method,
            OpCode.Ldloc => OperandKind.UInt16,
            OpCode.Ldloca => OperandKind.UInt16,
            OpCode.Ldloca_S => OperandKind.UInt8,
            OpCode.Ldobj => OperandKind.TypeToken,
            OpCode.Ldsfld => OperandKind.Field,
            OpCode.Ldsflda => OperandKind.Field,
            OpCode.Ldstr => OperandKind.String,
            OpCode.Ldtoken => OperandKind.Token,
            OpCode.Ldvirtftn => OperandKind.Method,
            OpCode.Mkrefany => OperandKind.Class,
            OpCode.Newarr => OperandKind.TypeToken,
            OpCode.Newobj => OperandKind.Constructor,
            OpCode.Refanyval => OperandKind.TypeToken,
            OpCode.Starg => OperandKind.UInt16,
            OpCode.Starg_S => OperandKind.UInt8,
            OpCode.Sizeof => OperandKind.TypeToken,
            OpCode.Stelem => OperandKind.TypeToken,
            OpCode.Stfld => OperandKind.Field,
            OpCode.Stloc => OperandKind.UInt16,
            OpCode.Stloc_S => OperandKind.UInt8,
            OpCode.Stobj => OperandKind.TypeToken,
            OpCode.Stsfld => OperandKind.Field,
            OpCode.Unbox => OperandKind.ValueType,
            OpCode.Unbox_Any => OperandKind.TypeToken,
            _ => OperandKind.None,
        };
    }
}
