using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using TemplateMethodDecoder = Buckle.CodeAnalysis.TemplateMetadataReader.TemplateMetadata.TemplateMethodDecoder;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataReader {
    private sealed class BoundNodeDecoder {
        private readonly uint _startPosition;
        private readonly uint _boundIRSize;
        private readonly BinaryReader _reader;
        private readonly TemplateMethodDecoder _methodDecoder;
        private readonly TemplateMetadata _metadata;
        private readonly MethodSymbol _methodSymbol;
        private readonly Dictionary<string, LabelSymbol> _labels = [];
        private readonly Stack<ImmutableArray<DataContainerSymbol>> _enclosingBlocks = [];

        private BoundNodeDecoder(
            MethodSymbol methodSymbol,
            TemplateMethodDecoder methodDecoder,
            TemplateMetadata metadata,
            BinaryReader reader,
            uint startPosition,
            uint boundIRSize) {
            _metadata = metadata;
            _methodSymbol = methodSymbol;
            _reader = reader;
            _startPosition = startPosition;
            _boundIRSize = boundIRSize;
            _methodDecoder = methodDecoder;
        }

        internal static BoundBlockStatement Decode(
            MethodSymbol methodSymbol,
            TemplateMethodDecoder methodDecoder,
            TemplateMetadata metadata,
            BinaryReader reader,
            uint startPosition,
            uint boundIRSize) {
#if DEBUG
            reader = new BinaryReader(new MemoryStream(((MemoryStream)reader.BaseStream).ToArray()));
#endif

            var decoder = new BoundNodeDecoder(methodSymbol, methodDecoder, metadata, reader, startPosition, boundIRSize);

            lock (reader) lock (reader.BaseStream) {
                reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);

                var nodeKind = reader.ReadByte();

                if (nodeKind != (byte)BoundKind.BlockStatement) {
                    Debug.Assert(false);
                    return null;
                }

                var decodedBody = decoder.ReadBlockStatement();

                if (decodedBody is null || reader.BaseStream.Position != startPosition + boundIRSize) {
                    Debug.Assert(false);
                    return null;
                }

                return decodedBody;
            }
        }

        private BoundBlockStatement ReadBlockStatement() {
            var localCount = _reader.ReadUInt16();
            var locals = ArrayBuilder<DataContainerSymbol>.GetInstance(localCount);

            for (var i = 0; i < localCount; i++) {
                var nameSize = _reader.ReadUInt32();
                var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
                var typeKind = _reader.ReadByte();
                var type = _methodDecoder.ReadTypeSymbol(typeKind, _reader);
                var flags = (TemplateMetadataWriter.LocalFlags)_reader.ReadByte();

                locals.Add(new SynthesizedDataContainerSymbol(
                    _methodSymbol,
                    type,
                    name,
                    (flags & TemplateMetadataWriter.LocalFlags.ByRef) != 0 ? RefKind.Ref : RefKind.None,
                    (flags & TemplateMetadataWriter.LocalFlags.IsPinned) != 0
                ));
            }

            var immutableLocals = locals.ToImmutableAndFree();

#if DEBUG
            var startSize = _enclosingBlocks.Count;
#endif

            _enclosingBlocks.Push(immutableLocals);

            var statementCount = _reader.ReadUInt16();
            var statements = ArrayBuilder<BoundStatement>.GetInstance(statementCount);

            var endPosition = _reader.BaseStream.Position;

            for (var i = 0; i < statementCount; i++) {
                Debug.Assert(_reader.BaseStream.Position == endPosition);
                var statement = ReadStatement();
                endPosition = _reader.BaseStream.Position;

                if (statement is null) {
                    Debug.Assert(false);
                    return null;
                }

                statements.Add(statement);
            }

            _enclosingBlocks.Pop();

#if DEBUG
            Debug.Assert(_enclosingBlocks.Count == startSize);
#endif

            return new BoundBlockStatement(null, statements.ToImmutableAndFree(), immutableLocals, []);
        }

        private bool IsValidPosition() {
            return _reader.BaseStream.Position < _startPosition + _boundIRSize;
        }

        private ConstantValue ReadConstantValue() {
            var specialType = (SpecialType)_reader.ReadByte();
            var isNull = _reader.ReadBoolean();

            if (isNull)
                return new ConstantValue(null, specialType);

            switch (specialType) {
                case SpecialType.String: {
                        var size = _reader.ReadUInt32();
                        var value = Encoding.UTF8.GetString(_reader.ReadBytes((int)size));
                        return new ConstantValue(value, SpecialType.String);
                    }
                case SpecialType.Bool: {
                        var value = _reader.ReadBoolean();
                        return new ConstantValue(value, SpecialType.Bool);
                    }
                case SpecialType.WinBool: {
                        var value = _reader.ReadInt32();
                        return new ConstantValue(value, SpecialType.WinBool);
                    }
                case SpecialType.Char: {
                        var value = _reader.ReadChar();
                        return new ConstantValue(value, SpecialType.Char);
                    }
                case SpecialType.Int8: {
                        var value = _reader.ReadSByte();
                        return new ConstantValue(value, SpecialType.Int8);
                    }
                case SpecialType.UInt8: {
                        var value = _reader.ReadByte();
                        return new ConstantValue(value, SpecialType.UInt8);
                    }
                case SpecialType.Int16: {
                        var value = _reader.ReadInt16();
                        return new ConstantValue(value, SpecialType.Int16);
                    }
                case SpecialType.UInt16: {
                        var value = _reader.ReadUInt16();
                        return new ConstantValue(value, SpecialType.UInt16);
                    }
                case SpecialType.Int32: {
                        var value = _reader.ReadInt32();
                        return new ConstantValue(value, SpecialType.Int32);
                    }
                case SpecialType.UInt32: {
                        var value = _reader.ReadUInt32();
                        return new ConstantValue(value, SpecialType.UInt32);
                    }
                case SpecialType.Int: {
                        var value = _reader.ReadInt64();
                        return new ConstantValue(value, SpecialType.Int);
                    }
                case SpecialType.Int64: {
                        var value = _reader.ReadInt64();
                        return new ConstantValue(value, SpecialType.Int64);
                    }
                case SpecialType.UInt64: {
                        var value = _reader.ReadUInt64();
                        return new ConstantValue(value, SpecialType.UInt64);
                    }
                case SpecialType.Float32: {
                        var value = _reader.ReadSingle();
                        return new ConstantValue(value, SpecialType.Float32);
                    }
                case SpecialType.Decimal: {
                        var value = _reader.ReadDouble();
                        return new ConstantValue(value, SpecialType.Decimal);
                    }
                case SpecialType.Float64: {
                        var value = _reader.ReadDouble();
                        return new ConstantValue(value, SpecialType.Float64);
                    }
                case SpecialType.IntPtr: {
                        var value = _reader.ReadInt64();
                        return new ConstantValue(value, SpecialType.IntPtr);
                    }
                case SpecialType.UIntPtr: {
                        var value = _reader.ReadUInt64();
                        return new ConstantValue(value, SpecialType.UIntPtr);
                    }
                default:
                    Debug.Assert(false);
                    return null;
            }
        }

        private BoundStatement ReadStatement() {
            var kind = _reader.ReadByte();

            if (!IsValidPosition()) {
                Debug.Assert(false);
                return null;
            }

            switch (kind) {
                case (byte)BoundKind.BlockStatement:
                    return ReadBlockStatement();
                case (byte)BoundKind.GotoStatement:
                    return ReadGotoStatement();
                case (byte)BoundKind.LabelStatement:
                    return ReadLabelStatement();
                case (byte)BoundKind.ConditionalGotoStatement:
                    return ReadConditionalGotoStatement();
                case (byte)BoundKind.LocalDeclarationStatement:
                    return ReadLocalDeclarationStatement();
                case (byte)BoundKind.ReturnStatement:
                    return ReadReturnStatement();
                case (byte)BoundKind.UnreachableStatement:
                    return ReadUnreachableStatement();
                case (byte)BoundKind.TryStatement:
                    return ReadTryStatement();
                case (byte)BoundKind.ExpressionStatement:
                    return ReadExpressionStatement();
                case (byte)BoundKind.InlineILStatement:
                    return ReadInlineILStatement();
                case (byte)BoundKind.SwitchDispatch:
                    return ReadSwitchDispatch();
                default:
                    Debug.Assert(false);
                    return null;
            }
        }

        private LabelSymbol GetLabel(string name) {
            if (_labels.TryGetValue(name, out var found))
                return found;

            var newLabel = new SynthesizedLabelSymbol(name);
            _labels.Add(name, newLabel);

            return newLabel;
        }

        private DataContainerSymbol GetLocal(string name) {
            foreach (var frame in _enclosingBlocks) {
                if (frame.Any(t => t.name == name))
                    return frame.First(t => t.name == name);
            }

            Debug.Assert(false);
            return null;
        }

        private MethodSymbol GetMethod(uint methodIndex) {
            var startPosition = _reader.BaseStream.Position;
            var result = _metadata.ResolveMethod(methodIndex);
            _reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);
            return result;
        }

        private BoundGotoStatement ReadGotoStatement() {
            var nameSize = _reader.ReadUInt32();
            var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
            var label = GetLabel(name);
            return new BoundGotoStatement(null, label, null);
        }

        private BoundLabelStatement ReadLabelStatement() {
            var nameSize = _reader.ReadUInt32();
            var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
            var label = GetLabel(name);
            return new BoundLabelStatement(null, label);
        }

        private BoundConditionalGotoStatement ReadConditionalGotoStatement() {
            var nameSize = _reader.ReadUInt32();
            var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
            var label = GetLabel(name);
            var jumpIfTrue = _reader.ReadByte();
            var condition = ReadExpression();
            return new BoundConditionalGotoStatement(null, label, condition, jumpIfTrue == 1);
        }

        private BoundLocalDeclarationStatement ReadLocalDeclarationStatement() {
            var nameSize = _reader.ReadUInt32();
            var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
            var local = GetLocal(name);
            var initializer = ReadExpression(backtrackIfNotExpression: true);

            return new BoundLocalDeclarationStatement(
                null,
                new BoundDataContainerDeclaration(null, local, initializer)
            );
        }

        private BoundReturnStatement ReadReturnStatement() {
            var refKind = (RefKind)_reader.ReadByte();
            var expression = ReadExpression(backtrackIfNotExpression: true);
            return new BoundReturnStatement(null, refKind, expression);
        }

        private BoundUnreachableStatement ReadUnreachableStatement() {
            return new BoundUnreachableStatement(null);
        }

        private BoundTryStatement ReadTryStatement() {
            var mode = _reader.ReadByte();
            var tryBody = ReadStatement() as BoundBlockStatement;
            var catchBody = mode == 2 || mode == 1 ? ReadStatement() as BoundBlockStatement : null;
            var finallyBody = mode == 2 || mode == 0 ? ReadStatement() as BoundBlockStatement : null;
            return new BoundTryStatement(null, tryBody, catchBody, finallyBody);
        }

        private BoundExpressionStatement ReadExpressionStatement() {
            var expression = ReadExpression();
            return new BoundExpressionStatement(null, expression);
        }

        private BoundInlineILStatement ReadInlineILStatement() {
            // TODO
            throw ExceptionUtilities.Unreachable();
        }

        private BoundSwitchDispatch ReadSwitchDispatch() {
            var expression = ReadExpression();
            var nameSize = _reader.ReadUInt32();
            var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
            var defaultLabel = GetLabel(name);
            var caseCount = _reader.ReadUInt16();
            var cases = ArrayBuilder<(ConstantValue, LabelSymbol)>.GetInstance(caseCount);

            for (var i = 0; i < caseCount; i++) {
                var constantValue = ReadConstantValue();
                var labelNameSize = _reader.ReadUInt32();
                var labelName = Encoding.UTF8.GetString(_reader.ReadBytes((int)labelNameSize));
                var label = GetLabel(labelName);
                cases.Add((constantValue, label));
            }

            return new BoundSwitchDispatch(null, expression, cases.ToImmutableAndFree(), defaultLabel);
        }

        private BoundExpression ReadExpression(bool backtrackIfNotExpression = false) {
            if (!IsValidPosition()) {
                if (!backtrackIfNotExpression)
                    Debug.Assert(false);

                return null;
            }

            var kind = _reader.ReadByte();

            if (!IsValidPosition()) {
                Debug.Assert(false);
                return null;
            }

            switch (kind) {
                case (byte)BoundKind.LiteralExpression:
                    return ReadLiteralExpression();
                case (byte)BoundKind.ThisExpression:
                    return ReadThisExpression();
                case (byte)BoundKind.DefaultExpression:
                    return ReadDefaultExpression();
                case (byte)BoundKind.BaseExpression:
                    return ReadBaseExpression();
                case (byte)BoundKind.CastExpression:
                    return ReadCastExpression();
                case (byte)BoundKind.DataContainerExpression:
                    return ReadDataContainerExpression();
                case (byte)BoundKind.ParameterExpression:
                    return ReadParameterExpression();
                case (byte)BoundKind.FieldAccessExpression:
                    return ReadFieldAccessExpression();
                case (byte)BoundKind.AssignmentOperator:
                    return ReadAssignmentOperator();
                case (byte)BoundKind.UnaryOperator:
                    return ReadUnaryOperator();
                case (byte)BoundKind.BinaryOperator:
                    return ReadBinaryOperator();
                case (byte)BoundKind.AsOperator:
                    return ReadAsOperator();
                case (byte)BoundKind.IsOperator:
                    return ReadIsOperator();
                case (byte)BoundKind.AddressOfOperator:
                    return ReadAddressOfOperator();
                case (byte)BoundKind.PointerIndirectionOperator:
                    return ReadPointerIndirectionOperator();
                case (byte)BoundKind.FunctionPointerLoad:
                    return ReadFunctionPointerLoad();
                case (byte)BoundKind.FunctionLoad:
                    return ReadFunctionLoad();
                case (byte)BoundKind.ConditionalOperator:
                    return ReadConditionalOperator();
                case (byte)BoundKind.NullAssertOperator:
                    return ReadNullAssertOperator();
                case (byte)BoundKind.CallExpression:
                    return ReadCallExpression();
                case (byte)BoundKind.ObjectCreationExpression:
                    return ReadObjectCreationExpression();
                case (byte)BoundKind.ArrayCreationExpression:
                    return ReadArrayCreationExpression();
                case (byte)BoundKind.ArrayAccessExpression:
                    return ReadArrayAccessExpression();
                case (byte)BoundKind.IndexerAccessExpression:
                    return ReadIndexerAccessExpression();
                case (byte)BoundKind.TypeOfExpression:
                    return ReadTypeOfExpression();
                case (byte)BoundKind.SizeOfOperator:
                    return ReadSizeOfOperator();
                case (byte)BoundKind.ThrowExpression:
                    return ReadThrowExpression();
                case (byte)BoundKind.FunctionPointerCallExpression:
                    return ReadFunctionPointerCallExpression();
                case (byte)BoundKind.ConvertedStackAllocExpression:
                    return ReadConvertedStackAllocExpression();
                case (byte)BoundKind.ArrayLength:
                    return ReadArrayLength();
                case (byte)BoundKind.TypeExpression:
                    return ReadTypeExpression();
                default:
                    if (backtrackIfNotExpression) {
                        _reader.BaseStream.Seek(-1, SeekOrigin.Current);
                        return null;
                    }

                    Debug.Assert(false);
                    return null;
            }
        }

        private BoundLiteralExpression ReadLiteralExpression() {
            var value = ReadConstantValue();
            return new BoundLiteralExpression(null, value, CorLibrary.Instance.GetSpecialType(value.specialType));
        }

        private TypeSymbol ReadType() {
            var typeKind = _reader.ReadByte();
            return _methodDecoder.ReadTypeSymbol(typeKind, _reader);
        }

        private BoundThisExpression ReadThisExpression() {
            var type = ReadType();
            return new BoundThisExpression(null, type);
        }

        private BoundDefaultExpression ReadDefaultExpression() {
            var type = ReadType();
            return new BoundDefaultExpression(null, false, new BoundTypeExpression(null, null, null, type), null, type);
        }

        private BoundBaseExpression ReadBaseExpression() {
            var type = ReadType();
            return new BoundBaseExpression(null, type);
        }

        private BoundCastExpression ReadCastExpression() {
            var type = ReadType();
            var conversionKind = (ConversionKind)_reader.ReadByte();
            var operand = ReadExpression();
            return new BoundCastExpression(null, operand, new Conversion(conversionKind), null, type);
        }

        private BoundDataContainerExpression ReadDataContainerExpression() {
            var nameSize = _reader.ReadUInt32();
            var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
            var local = GetLocal(name);
            return new BoundDataContainerExpression(null, local, null, local.type);
        }

        private BoundParameterExpression ReadParameterExpression() {
            var ordinal = _reader.ReadUInt16();
            var parameter = _methodSymbol.parameters[ordinal];
            return new BoundParameterExpression(null, parameter, null, parameter.type);
        }

        private BoundFieldAccessExpression ReadFieldAccessExpression() {
            var nameSize = _reader.ReadUInt32();
            var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
            var hasReceiver = _reader.ReadBoolean();

            if (hasReceiver) {
                var receiver = ReadExpression();
                var field = (FieldSymbol)receiver.type.GetMembers(name).Single(m => m is FieldSymbol);
                return new BoundFieldAccessExpression(null, receiver, field, null, field.type);
            } else {
                var type = ReadType();
                var field = (FieldSymbol)type.GetMembers(name).Single(m => m is FieldSymbol);
                return new BoundFieldAccessExpression(null, null, field, null, field.type);
            }
        }

        private BoundAssignmentOperator ReadAssignmentOperator() {
            var isRef = _reader.ReadByte() == 1;
            var left = ReadExpression();
            var right = ReadExpression();
            return new BoundAssignmentOperator(null, left, right, isRef, left.type);
        }

        private BoundUnaryOperator ReadUnaryOperator() {
            var type = ReadType();
            var operatorKind = (UnaryOperatorKind)_reader.ReadUInt32();
            var operand = ReadExpression();
            return new BoundUnaryOperator(null, operand, operatorKind, null, null, type);
        }

        private BoundBinaryOperator ReadBinaryOperator() {
            var type = ReadType();
            var operatorKind = (BinaryOperatorKind)_reader.ReadUInt32();
            var left = ReadExpression();
            var right = ReadExpression();
            return new BoundBinaryOperator(null, left, right, operatorKind, null, null, type);
        }

        private BoundAsOperator ReadAsOperator() {
            // TODO Double check the types are the same
            var type = ReadType();
            var operand = ReadExpression();

            return new BoundAsOperator(
                null,
                operand,
                new BoundTypeExpression(null, null, null, type),
                null,
                null,
                type
            );
        }

        private BoundIsOperator ReadIsOperator() {
            var flags = _reader.ReadByte();
            var isNullCheck = (flags & 1) != 0;
            var isNot = (flags & 2) != 0;

            if (isNullCheck) {
                var operand = ReadExpression();

                return new BoundIsOperator(
                    null,
                    operand,
                    new BoundLiteralExpression(null, ConstantValue.Null, null),
                    isNot,
                    null,
                    CorLibrary.Instance.GetSpecialType(SpecialType.Bool)
                );
            } else {
                var type = ReadType();
                var operand = ReadExpression();

                return new BoundIsOperator(
                    null,
                    operand,
                    new BoundTypeExpression(null, null, null, type),
                    isNot,
                    null,
                    CorLibrary.Instance.GetSpecialType(SpecialType.Bool)
                );
            }
        }

        private BoundAddressOfOperator ReadAddressOfOperator() {
            var type = ReadType();
            var operand = ReadExpression();
            return new BoundAddressOfOperator(null, operand, false, type);
        }

        private BoundPointerIndirectionOperator ReadPointerIndirectionOperator() {
            var type = ReadType();
            var operand = ReadExpression();
            return new BoundPointerIndirectionOperator(null, operand, false, type);
        }

        private BoundFunctionPointerLoad ReadFunctionPointerLoad() {
            var type = ReadType();
            var constrainedToType = ReadType();
            var methodIndex = _reader.ReadUInt32();
            var method = GetMethod(methodIndex);
            return new BoundFunctionPointerLoad(null, method, constrainedToType, type);
        }

        private BoundFunctionLoad ReadFunctionLoad() {
            var type = ReadType();
            var methodIndex = _reader.ReadUInt32();
            var method = GetMethod(methodIndex);
            var receiver = ReadExpression();
            return new BoundFunctionLoad(null, receiver, method, type);
        }

        private BoundConditionalOperator ReadConditionalOperator() {
            var type = ReadType();
            var condition = ReadExpression();
            var trueExpr = ReadExpression();
            var falseExpr = ReadExpression();
            return new BoundConditionalOperator(null, condition, false, trueExpr, falseExpr, null, type);
        }

        private BoundNullAssertOperator ReadNullAssertOperator() {
            var type = ReadType();
            var throwIfNull = _reader.ReadByte() == 1;
            var expression = ReadExpression();
            return new BoundNullAssertOperator(null, expression, throwIfNull, null, type);
        }

        private BoundCallExpression ReadCallExpression() {
            var type = ReadType();
            var methodIndex = _reader.ReadUInt32();
            var method = GetMethod(methodIndex);
            var hasReceiver = _reader.ReadBoolean();
            var receiver = hasReceiver ? ReadExpression() : null;
            var argumentCount = _reader.ReadUInt16();
            var argumentsBuilder = ArrayBuilder<BoundExpression>.GetInstance(argumentCount);

            for (var i = 0; i < argumentCount; i++)
                argumentsBuilder.Add(ReadExpression());

            var argumentRefKindCount = _reader.ReadUInt16();
            var argumentRefKindBuilder = ArrayBuilder<RefKind>.GetInstance(argumentRefKindCount);

            for (var i = 0; i < argumentRefKindCount; i++)
                argumentRefKindBuilder.Add((RefKind)_reader.ReadByte());

            return new BoundCallExpression(
                null,
                receiver,
                method,
                argumentsBuilder.ToImmutableAndFree(),
                argumentRefKindBuilder.ToImmutableAndFree(),
                default,
                default,
                type
            );
        }

        private BoundObjectCreationExpression ReadObjectCreationExpression() {
            var type = ReadType();
            var methodIndex = _reader.ReadUInt32();
            var method = GetMethod(methodIndex);
            var argumentCount = _reader.ReadUInt16();
            var argumentsBuilder = ArrayBuilder<BoundExpression>.GetInstance(argumentCount);

            for (var i = 0; i < argumentCount; i++)
                argumentsBuilder.Add(ReadExpression());

            var argumentRefKindCount = _reader.ReadUInt16();
            var argumentRefKindBuilder = ArrayBuilder<RefKind>.GetInstance(argumentRefKindCount);

            for (var i = 0; i < argumentRefKindCount; i++)
                argumentRefKindBuilder.Add((RefKind)_reader.ReadByte());

            return new BoundObjectCreationExpression(
                null,
                method,
                argumentsBuilder.ToImmutableAndFree(),
                argumentRefKindBuilder.ToImmutableAndFree(),
                default,
                default,
                false,
                type
            );
        }

        private BoundArrayCreationExpression ReadArrayCreationExpression() {
            var type = ReadType();
            var sizeCount = _reader.ReadUInt16();
            var sizesBuilder = ArrayBuilder<BoundExpression>.GetInstance(sizeCount);

            for (var i = 0; i < sizeCount; i++)
                sizesBuilder.Add(ReadExpression());

            var hasInitializer = _reader.ReadBoolean();
            var initializer = hasInitializer ? ReadExpression() as BoundInitializerList : null;

            return new BoundArrayCreationExpression(null, sizesBuilder.ToImmutableAndFree(), initializer, type);
        }

        private BoundArrayAccessExpression ReadArrayAccessExpression() {
            var type = ReadType();
            var receiver = ReadExpression();
            var index = ReadExpression();
            return new BoundArrayAccessExpression(null, receiver, index, null, type);
        }

        private BoundIndexerAccessExpression ReadIndexerAccessExpression() {
            var type = ReadType();
            var receiver = ReadExpression();
            var index = ReadExpression();
            return new BoundIndexerAccessExpression(null, receiver, index, null, null, type);
        }

        private BoundTypeOfExpression ReadTypeOfExpression() {
            var type = ReadType();

            return new BoundTypeOfExpression(
                null,
                new BoundTypeExpression(null, null, null, type),
                CorLibrary.Instance.GetSpecialType(SpecialType.Type)
            );
        }

        private BoundTypeExpression ReadTypeExpression() {
            var type = ReadType();
            return new BoundTypeExpression(null, null, null, type);
        }

        private BoundSizeOfOperator ReadSizeOfOperator() {
            var type = ReadType();

            return new BoundSizeOfOperator(
                null,
                new BoundTypeExpression(null, null, null, type),
                null,
                CorLibrary.Instance.GetSpecialType(SpecialType.Int32)
            );
        }

        private BoundThrowExpression ReadThrowExpression() {
            var expression = ReadExpression();
            return new BoundThrowExpression(null, expression, null);
        }

        private BoundFunctionPointerCallExpression ReadFunctionPointerCallExpression() {
            // TODO
            throw ExceptionUtilities.Unreachable();
        }

        private BoundConvertedStackAllocExpression ReadConvertedStackAllocExpression() {
            var type = ReadType();
            var elementType = ReadType();
            var count = ReadExpression();
            return new BoundConvertedStackAllocExpression(null, elementType, count, type);
        }

        private BoundArrayLength ReadArrayLength() {
            var type = ReadType();
            var receiver = ReadExpression();
            return new BoundArrayLength(null, receiver, type);
        }
    }
}
