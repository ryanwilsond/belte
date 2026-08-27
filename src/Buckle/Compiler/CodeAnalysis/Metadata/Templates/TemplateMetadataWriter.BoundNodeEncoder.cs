using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    private sealed class BoundNodeEncoder : BoundTreeWalkerWithStackGuard {
        private readonly BinaryWriter _writer;
        private readonly TemplateMetadataWriter _metadataWriter;
        private readonly HashSet<string> _seenLocals = [];

        private BoundNodeEncoder(BinaryWriter writer, TemplateMetadataWriter metadataWriter) {
            _writer = writer;
            _metadataWriter = metadataWriter;
        }

        internal static void Encode(
            BoundBlockStatement node,
            BinaryWriter writer,
            TemplateMetadataWriter metadataWriter) {
            var encoder = new BoundNodeEncoder(writer, metadataWriter);
            encoder.Visit(node);
        }

        internal override BoundNode VisitBlockStatement(BoundBlockStatement node) {
            _writer.Write((byte)BoundKind.BlockStatement);
            _writer.Write((ushort)node.locals.Length);

            foreach (var local in node.locals)
                WriteLocal(local);

            _writer.Write((ushort)node.statements.Length);

            return base.VisitBlockStatement(node);
        }

        private void WriteLocal(DataContainerSymbol local) {
            /*

    Size

    4       Name Size
    ...     Name
    1       Type Kind
    ...     Type Info
    1       Flags (Ref Kind, IsPinned)

            */
            Debug.Assert((uint)local.metadataName.Length == Encoding.UTF8.GetBytes(local.metadataName).Length);
            _writer.Write((uint)local.metadataName.Length);
            _writer.Write(Encoding.UTF8.GetBytes(local.metadataName));
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(local.type));
            _writer.Write(CreateLocalFlags(local));

            _seenLocals.Add(local.metadataName);
        }

        private static byte CreateLocalFlags(DataContainerSymbol local) {
            var flags = LocalFlags.None;

            if (local.isRef)
                flags |= LocalFlags.ByRef;

            if (local.isPinned)
                flags |= LocalFlags.IsPinned;

            return (byte)flags;
        }

        internal override BoundNode VisitLiteralExpression(BoundLiteralExpression node) {
            _writer.Write((byte)BoundKind.LiteralExpression);
            _writer.Write((byte)node.constantValue.specialType);
            _writer.Write(node.constantValue.value is null);

            if (node.constantValue.value is not null)
                WriteConstantValueValue(_writer, node.constantValue);

            return null;
        }

        internal override BoundNode VisitThisExpression(BoundThisExpression node) {
            _writer.Write((byte)BoundKind.ThisExpression);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            return base.VisitThisExpression(node);
        }

        internal override BoundNode VisitDefaultExpression(BoundDefaultExpression node) {
            _writer.Write((byte)BoundKind.DefaultExpression);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            return base.VisitDefaultExpression(node);
        }

        internal override BoundNode VisitBaseExpression(BoundBaseExpression node) {
            _writer.Write((byte)BoundKind.BaseExpression);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            return base.VisitBaseExpression(node);
        }

        internal override BoundNode VisitCastExpression(BoundCastExpression node) {
            _writer.Write((byte)BoundKind.CastExpression);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            _writer.Write((byte)node.conversion.kind);
            return base.VisitCastExpression(node);
        }

        internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
            _writer.Write((byte)BoundKind.DataContainerExpression);
            // TODO We should probably use slots instead of names...
            Debug.Assert((uint)node.dataContainer.metadataName.Length == Encoding.UTF8.GetBytes(node.dataContainer.metadataName).Length);
            _writer.Write((uint)node.dataContainer.metadataName.Length);
            _writer.Write(Encoding.UTF8.GetBytes(node.dataContainer.metadataName));

            Debug.Assert(_seenLocals.Contains(node.dataContainer.metadataName));

            return base.VisitDataContainerExpression(node);
        }

        internal override BoundNode VisitStackSlotExpression(BoundStackSlotExpression node) {
            return Visit(node.original);
        }

        internal override BoundNode VisitParameterExpression(BoundParameterExpression node) {
            _writer.Write((byte)BoundKind.ParameterExpression);
            _writer.Write((ushort)node.parameter.ordinal);
            return base.VisitParameterExpression(node);
        }

        internal override BoundNode VisitFieldAccessExpression(BoundFieldAccessExpression node) {
            _writer.Write((byte)BoundKind.FieldAccessExpression);
            Debug.Assert((uint)node.field.metadataName.Length == Encoding.UTF8.GetBytes(node.field.metadataName).Length);
            _writer.Write((uint)node.field.metadataName.Length);
            _writer.Write(Encoding.UTF8.GetBytes(node.field.metadataName));

            if (node.receiver is null) {
                _writer.Write(false);
                _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.field.containingType));
            } else {
                _writer.Write(true);
                Visit(node.receiver);
            }

            return null;
        }

        internal override BoundNode VisitFieldSlotExpression(BoundFieldSlotExpression node) {
            return Visit(node.original);
        }

        internal override BoundNode VisitAssignmentOperator(BoundAssignmentOperator node) {
            _writer.Write((byte)BoundKind.AssignmentOperator);
            _writer.Write(node.isRef ? (byte)1 : (byte)0);
            return base.VisitAssignmentOperator(node);
        }

        internal override BoundNode VisitUnaryOperator(BoundUnaryOperator node) {
            _writer.Write((byte)BoundKind.UnaryOperator);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            _writer.Write((uint)node.operatorKind);
            return base.VisitUnaryOperator(node);
        }

        internal override BoundNode VisitBinaryOperator(BoundBinaryOperator node) {
            _writer.Write((byte)BoundKind.BinaryOperator);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            _writer.Write((uint)node.operatorKind);
            return base.VisitBinaryOperator(node);
        }

        internal override BoundNode VisitAsOperator(BoundAsOperator node) {
            _writer.Write((byte)BoundKind.AsOperator);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            Visit(node.left);
            return null;
        }

        internal override BoundNode VisitIsOperator(BoundIsOperator node) {
            _writer.Write((byte)BoundKind.IsOperator);
            var flags = node.right.IsLiteralNull() ? (byte)1 : (byte)0;

            if (node.isNot)
                flags |= 2;

            _writer.Write(flags);

            if (!node.right.IsLiteralNull())
                _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.right.type));

            Visit(node.left);
            return null;
        }

        internal override BoundNode VisitAddressOfOperator(BoundAddressOfOperator node) {
            _writer.Write((byte)BoundKind.AddressOfOperator);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            return base.VisitAddressOfOperator(node);
        }

        internal override BoundNode VisitPointerIndirectionOperator(BoundPointerIndirectionOperator node) {
            _writer.Write((byte)BoundKind.PointerIndirectionOperator);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            return base.VisitPointerIndirectionOperator(node);
        }

        internal override BoundNode VisitFunctionPointerLoad(BoundFunctionPointerLoad node) {
            _writer.Write((byte)BoundKind.FunctionPointerLoad);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.constrainedToType));
            _writer.Write(_metadataWriter.CreateMethodIndex(node.targetMethod));
            return base.VisitFunctionPointerLoad(node);
        }

        internal override BoundNode VisitFunctionLoad(BoundFunctionLoad node) {
            _writer.Write((byte)BoundKind.FunctionLoad);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            _writer.Write(_metadataWriter.CreateMethodIndex(node.targetMethod));
            return base.VisitFunctionLoad(node);
        }

        internal override BoundNode VisitConditionalOperator(BoundConditionalOperator node) {
            _writer.Write((byte)BoundKind.ConditionalOperator);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            return base.VisitConditionalOperator(node);
        }

        internal override BoundNode VisitNullAssertOperator(BoundNullAssertOperator node) {
            _writer.Write((byte)BoundKind.NullAssertOperator);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            _writer.Write(node.throwIfNull ? (byte)1 : (byte)0);
            return base.VisitNullAssertOperator(node);
        }

        internal override BoundNode VisitCallExpression(BoundCallExpression node) {
            _writer.Write((byte)BoundKind.CallExpression);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            _writer.Write(_metadataWriter.CreateMethodIndex(node.method));

            _writer.Write(node.receiver is not null);
            Visit(node.receiver);

            _writer.Write((ushort)node.arguments.Length);
            VisitList(node.arguments);

            if (node.argumentRefKinds == default) {
                _writer.Write((ushort)0);
            } else {
                _writer.Write((ushort)node.argumentRefKinds.Length);

                foreach (var refKind in node.argumentRefKinds)
                    _writer.Write((byte)refKind);
            }

            return null;
        }

        internal override BoundNode VisitObjectCreationExpression(BoundObjectCreationExpression node) {
            _writer.Write((byte)BoundKind.ObjectCreationExpression);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            _writer.Write(_metadataWriter.CreateMethodIndex(node.constructor));

            _writer.Write((ushort)node.arguments.Length);
            VisitList(node.arguments);

            if (node.argumentRefKinds == default) {
                _writer.Write((ushort)0);
            } else {
                _writer.Write((ushort)node.argumentRefKinds.Length);

                foreach (var refKind in node.argumentRefKinds)
                    _writer.Write((byte)refKind);
            }

            return null;
        }

        internal override BoundNode VisitArrayCreationExpression(BoundArrayCreationExpression node) {
            _writer.Write((byte)BoundKind.ArrayCreationExpression);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            _writer.Write((ushort)node.sizes.Length);
            VisitList(node.sizes);
            _writer.Write(node.initializer is not null);
            Visit(node.initializer);
            return null;
        }

        internal override BoundNode VisitArrayAccessExpression(BoundArrayAccessExpression node) {
            _writer.Write((byte)BoundKind.ArrayAccessExpression);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            return base.VisitArrayAccessExpression(node);
        }

        internal override BoundNode VisitIndexerAccessExpression(BoundIndexerAccessExpression node) {
            _writer.Write((byte)BoundKind.IndexerAccessExpression);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            return base.VisitIndexerAccessExpression(node);
        }

        internal override BoundNode VisitTypeOfExpression(BoundTypeOfExpression node) {
            _writer.Write((byte)BoundKind.TypeOfExpression);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.sourceType.type));
            return null;
        }

        internal override BoundNode VisitSizeOfOperator(BoundSizeOfOperator node) {
            _writer.Write((byte)BoundKind.SizeOfOperator);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.sourceType.type));
            return null;
        }

        internal override BoundNode VisitTypeExpression(BoundTypeExpression node) {
            _writer.Write((byte)BoundKind.TypeExpression);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            return null;
        }

        internal override BoundNode VisitMethodGroup(BoundMethodGroup node) {
            throw ExceptionUtilities.UnexpectedValue(node.kind);
        }

        internal override BoundNode VisitThrowExpression(BoundThrowExpression node) {
            _writer.Write((byte)BoundKind.ThrowExpression);
            return base.VisitThrowExpression(node);
        }

        internal override BoundNode VisitFunctionPointerCallExpression(BoundFunctionPointerCallExpression node) {
            // TODO Need to work this one out
            throw ExceptionUtilities.Unreachable();
            // return base.VisitFunctionPointerCallExpression(node);
        }

        internal override BoundNode VisitConvertedStackAllocExpression(BoundConvertedStackAllocExpression node) {
            _writer.Write((byte)BoundKind.ConvertedStackAllocExpression);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.elementType));
            return base.VisitConvertedStackAllocExpression(node);
        }

        internal override BoundNode VisitArrayLength(BoundArrayLength node) {
            _writer.Write((byte)BoundKind.ArrayLength);
            _writer.Write(_metadataWriter.CreateTypeKindAndInfo(node.type));
            return base.VisitArrayLength(node);
        }

        internal override BoundNode VisitNopStatement(BoundNopStatement node) {
            return null;
        }

        internal override BoundNode VisitGotoStatement(BoundGotoStatement node) {
            _writer.Write((byte)BoundKind.GotoStatement);
            // TODO Should probably use ID's instead of names
            Debug.Assert((uint)node.label.metadataName.Length == Encoding.UTF8.GetBytes(node.label.metadataName).Length);
            _writer.Write((uint)node.label.metadataName.Length);
            _writer.Write(Encoding.UTF8.GetBytes(node.label.metadataName));
            return null;
        }

        internal override BoundNode VisitLabelStatement(BoundLabelStatement node) {
            _writer.Write((byte)BoundKind.LabelStatement);
            Debug.Assert((uint)node.label.metadataName.Length == Encoding.UTF8.GetBytes(node.label.metadataName).Length);
            _writer.Write((uint)node.label.metadataName.Length);
            _writer.Write(Encoding.UTF8.GetBytes(node.label.metadataName));
            return null;
        }

        internal override BoundNode VisitConditionalGotoStatement(BoundConditionalGotoStatement node) {
            _writer.Write((byte)BoundKind.ConditionalGotoStatement);
            Debug.Assert((uint)node.label.metadataName.Length == Encoding.UTF8.GetBytes(node.label.metadataName).Length);
            _writer.Write((uint)node.label.metadataName.Length);
            _writer.Write(Encoding.UTF8.GetBytes(node.label.metadataName));
            _writer.Write(node.jumpIfTrue ? (byte)1 : (byte)0);
            return base.VisitConditionalGotoStatement(node);
        }

        internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement node) {
            _writer.Write((byte)BoundKind.LocalDeclarationStatement);
            Debug.Assert((uint)node.declaration.dataContainer.metadataName.Length == Encoding.UTF8.GetBytes(node.declaration.dataContainer.metadataName).Length);
            _writer.Write((uint)node.declaration.dataContainer.metadataName.Length);
            _writer.Write(Encoding.UTF8.GetBytes(node.declaration.dataContainer.metadataName));
            Visit(node.declaration.initializer);
            return null;
        }

        internal override BoundNode VisitReturnStatement(BoundReturnStatement node) {
            _writer.Write((byte)BoundKind.ReturnStatement);
            _writer.Write((byte)node.refKind);
            return base.VisitReturnStatement(node);
        }

        internal override BoundNode VisitUnreachableStatement(BoundUnreachableStatement node) {
            _writer.Write((byte)BoundKind.UnreachableStatement);
            return null;
        }

        internal override BoundNode VisitExpressionStatement(BoundExpressionStatement node) {
            _writer.Write((byte)BoundKind.ExpressionStatement);
            return base.VisitExpressionStatement(node);
        }

        internal override BoundNode VisitSequencePoint(BoundSequencePoint node) {
            return null;
        }

        internal override BoundNode VisitSequencePointWithLocation(BoundSequencePointWithLocation node) {
            return null;
        }

        internal override BoundNode VisitInlineILStatement(BoundInlineILStatement node) {
            _writer.Write((byte)BoundKind.InlineILStatement);
            _writer.Write((ushort)node.instructions.Length);

            foreach (var instruction in node.instructions) {
                _writer.Write((byte)instruction.Item1);
                _writer.Write(
                    instruction.Item2 is not null && instruction.Item3 is not null
                        ? (byte)2
                        : instruction.Item2 is not null
                            ? (byte)1
                            : (byte)0
                );

                if (instruction.Item2 is not null) {
                    _writer.Write((byte)instruction.Item2.specialType);
                    WriteConstantValueValue(_writer, instruction.Item2);
                }

                if (instruction.Item3 is not null) {
                    // TODO arbitrary symbol
                    throw ExceptionUtilities.Unreachable();
                }
            }

            return null;
        }

        internal override BoundNode VisitTryStatement(BoundTryStatement node) {
            _writer.Write((byte)BoundKind.TryStatement);
            _writer.Write(
                node.catchBody is not null && node.finallyBody is not null
                    ? (byte)2
                    : node.catchBody is not null
                        ? (byte)1
                        : (byte)0
            );

            return base.VisitTryStatement(node);
        }

        internal override BoundNode VisitSwitchDispatch(BoundSwitchDispatch node) {
            _writer.Write((byte)BoundKind.SwitchDispatch);
            Visit(node.expression);
            Debug.Assert((uint)node.defaultLabel.metadataName.Length == Encoding.UTF8.GetBytes(node.defaultLabel.metadataName).Length);
            _writer.Write((uint)node.defaultLabel.metadataName.Length);
            _writer.Write(Encoding.UTF8.GetBytes(node.defaultLabel.metadataName));

            _writer.Write((ushort)node.cases.Length);

            foreach (var (value, label) in node.cases) {
                _writer.Write((byte)value.specialType);
                _writer.Write(value.value is null);

                if (value.value is not null)
                    WriteConstantValueValue(_writer, value);

                Debug.Assert((uint)label.metadataName.Length == Encoding.UTF8.GetBytes(label.metadataName).Length);
                _writer.Write((uint)label.metadataName.Length);
                _writer.Write(Encoding.UTF8.GetBytes(label.metadataName));
            }

            return null;
        }
    }
}
