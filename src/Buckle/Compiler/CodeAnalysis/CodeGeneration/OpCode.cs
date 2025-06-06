
namespace Buckle.CodeAnalysis.CodeGeneration;

internal enum OpCode : byte {
    Nop,
    Br,
    Clt,
    Clt_Un,
    Cgt,
    Cgt_Un,
    Blt,
    Bge,
    Blt_Un,
    Bge_Un,
    Ble_Un,
    Bgt,
    Ble,
    Bgt_Un,
    Readonly,
    Isinst,
    Brtrue,
    Brfalse,
    Ldelema,
    Ldc_I4,
    Conv_Ovf_I,
}
