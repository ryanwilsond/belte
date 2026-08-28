using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataReader {
    internal sealed partial class TemplateMetadata {
        internal sealed class TemplateMethodDecoder : TemplateDecoder {
            private readonly MethodEntry _methodEntry;
            private readonly uint _offsetAfterName;

            private bool _readTemplateParameters;
            private (string, GenericParameterAttributes, TemplateMetadataWriter.TemplateParameterFlags, TypeSymbol, TypeOrConstant)[] _templateParameters;
            private uint _offsetAfterTemplateParameters;

            private TypeSymbol _returnType;
            private uint _offsetAfterReturnType;

            private bool _readParameters;
            private (string, ParameterAttributes, TemplateMetadataWriter.ParameterFlags, TypeSymbol, ConstantValue)[] _parameters;
            private ushort _parameterCount;
            private uint _offsetAfterParameters;

            private BoundBlockStatement _methodBody;

            private bool _readConstraints;
            private BoundExpression[] _constraints;

            internal TemplateMethodDecoder(
                TemplateMetadata metadata,
                uint offset,
                uint offsetAfterName,
                uint size,
                MethodEntry methodEntry)
                : base(metadata, offset, size) {
                _methodEntry = methodEntry;
                _offsetAfterName = offsetAfterName;
            }

            internal MethodEntry methodEntry => _methodEntry;

            internal string GetMetadataName() {
                return _methodEntry.name;
            }

            internal ushort GetArity() {
                return _methodEntry.arity;
            }

            internal (string, GenericParameterAttributes, TemplateMetadataWriter.TemplateParameterFlags, TypeSymbol, TypeOrConstant) DecodeTemplateParameter(uint ordinal) {
                if (ordinal >= _methodEntry.arity)
                    throw ExceptionUtilities.Unreachable();

                DecodeTemplateParameters();

                return _templateParameters[ordinal];
            }

            internal (string, ParameterAttributes, TemplateMetadataWriter.ParameterFlags, TypeSymbol, ConstantValue) DecodeParameter(uint ordinal) {
                DecodeParameters();

                if (ordinal >= _parameterCount)
                    throw ExceptionUtilities.Unreachable();

                return _parameters[ordinal];
            }

            internal ushort GetParameterCount() {
                DecodeParameters();
                return _parameterCount;
            }

            internal TemplateMetadataWriter.ReturnFlags GetReturnFlags() {
                DecodeTemplateParameters();

                lock (_metadata) lock (_reader) {
                    _reader.BaseStream.Seek(_offsetAfterTemplateParameters, SeekOrigin.Begin);
                    return (TemplateMetadataWriter.ReturnFlags)_reader.ReadByte();
                }
            }

            internal MethodAttributes GetFlags() {
                lock (_metadata) lock (_reader) {
                    _reader.BaseStream.Seek(_offsetAfterName + 6, SeekOrigin.Begin);
                    return (MethodAttributes)_reader.ReadUInt32();
                }
            }

            internal TemplateMetadataWriter.MethodFlags GetAdditionalFlags() {
                lock (_metadata) lock (_reader) {
                    _reader.BaseStream.Seek(_offsetAfterName + 10, SeekOrigin.Begin);
                    return (TemplateMetadataWriter.MethodFlags)_reader.ReadUInt16();
                }
            }

            internal TypeSymbol GetReturnType() {
                if (_returnType is not null)
                    return _returnType;

                DecodeTemplateParameters();

                lock (_metadata) lock (this) lock (_reader) {
                    _reader.BaseStream.Seek(_offsetAfterTemplateParameters + 1, SeekOrigin.Begin);
                    var returnKind = _reader.ReadByte();
                    _returnType = ReadTypeSymbol(returnKind, _reader);
                    _offsetAfterReturnType = (uint)_reader.BaseStream.Position;
                }

                return _returnType;
            }

            internal BoundBlockStatement DecodeMethodBody(MethodSymbol methodSymbol) {
                if (_methodBody is not null)
                    return _methodBody;

                lock (_metadata) lock (this) lock (_reader) {
                    if (!_metadata.TryGetBoundTableOffsetForMethod(
                        methodEntry,
                        out var boundOffset,
                        out var boundIRSize)) {
                        _methodBody = null;
                        return _methodBody;
                    }

                    _methodBody = BoundNodeDecoder.DecodeMethodBody(
                        methodSymbol,
                        this,
                        _metadata,
                        _reader,
                        boundOffset,
                        boundIRSize
                    );
                }

                return _methodBody;
            }

            internal BoundExpression[] DecodeConstraints() {
                if (_readConstraints)
                    return _constraints;

                DecodeParameters();

                lock (_metadata) lock (this) lock (_reader) {
                    var position = _reader.BaseStream.Seek(_offsetAfterParameters, SeekOrigin.Begin);
                    Debug.Assert(_reader.BaseStream.Position == position && position == _offsetAfterParameters);

                    var count = _reader.ReadUInt16();
                    Debug.Assert(_reader.BaseStream.Position == _offsetAfterParameters + 2);

                    _constraints = new BoundExpression[count];

                    for (var i = 0; i < count; i++) {
                        var size = _reader.ReadUInt32();

                        _constraints[i] = BoundNodeDecoder.DecodeConstraint(
                            enclosingContext,
                            this,
                            _metadata,
                            _reader,
                            (uint)_reader.BaseStream.Position,
                            size - 4
                        );
                    }

                    _readConstraints = true;
                }

                return _constraints;
            }

            private void DecodeTemplateParameters() {
                if (_readTemplateParameters)
                    return;

                lock (_metadata) lock (this) lock (_reader) {
                    var position = _reader.BaseStream.Seek(_offsetAfterName + 12, SeekOrigin.Begin);
                    Debug.Assert(_reader.BaseStream.Position == position && position == _offsetAfterName + 12);

                    var count = _reader.ReadUInt16();
                    Debug.Assert(count == _methodEntry.arity);

                    _templateParameters = new (string, GenericParameterAttributes, TemplateMetadataWriter.TemplateParameterFlags, TypeSymbol, TypeOrConstant)[count];

                    for (var i = 0; i < count; i++) {
                        var nameSize = _reader.ReadUInt32();
                        var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
                        var flags = (TemplateMetadataWriter.TemplateParameterFlags)_reader.ReadByte();
                        var attributes = (GenericParameterAttributes)_reader.ReadUInt32();
                        var underlyingKind = _reader.ReadByte();
                        var underlyingType = ReadTypeSymbol(underlyingKind, _reader);
                        TypeOrConstant defaultValue = null;

                        if ((flags & TemplateMetadataWriter.TemplateParameterFlags.HasDefaultValue) != 0)
                            defaultValue = ReadTypeOrConstant(underlyingType, _reader);

                        _templateParameters[i] = (name, attributes, flags, underlyingType, defaultValue);
                    }

                    _offsetAfterTemplateParameters = (uint)_reader.BaseStream.Position;
                    _readTemplateParameters = true;
                }
            }

            private void DecodeParameters() {
                if (_readParameters)
                    return;

                _ = GetReturnType();

                lock (_metadata) lock (this) lock (_reader) {
                    var position = _reader.BaseStream.Seek(_offsetAfterReturnType, SeekOrigin.Begin);
                    Debug.Assert(_reader.BaseStream.Position == position && position == _offsetAfterReturnType);

                    _parameterCount = _reader.ReadUInt16();
                    _parameters = new (string, ParameterAttributes, TemplateMetadataWriter.ParameterFlags, TypeSymbol, ConstantValue)[_parameterCount];
                    Debug.Assert(_reader.BaseStream.Position == _offsetAfterReturnType + 2);

                    uint endPosition = 0;

                    for (var i = 0; i < _parameterCount; i++) {
                        var startPosition = (uint)_reader.BaseStream.Position;

                        if (i == 0)
                            Debug.Assert(startPosition == _offsetAfterReturnType + 2);
                        else
                            Debug.Assert(startPosition == endPosition);

                        var nameSize = _reader.ReadUInt32();
                        Debug.Assert(_reader.BaseStream.Position == startPosition + 4);
                        var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameSize));
                        Debug.Assert(_reader.BaseStream.Position == startPosition + 4 + nameSize);
                        var flags = (TemplateMetadataWriter.ParameterFlags)_reader.ReadByte();
                        Debug.Assert(_reader.BaseStream.Position == startPosition + 4 + nameSize + 1);
                        var attributes = (ParameterAttributes)_reader.ReadUInt32();
                        Debug.Assert(_reader.BaseStream.Position == startPosition + 4 + nameSize + 1 + 4);
                        var underlyingKind = _reader.ReadByte();
                        Debug.Assert(_reader.BaseStream.Position == startPosition + 4 + nameSize + 1 + 4 + 1);
                        var underlyingType = ReadTypeSymbol(underlyingKind, _reader);
                        ConstantValue defaultValue = null;

                        if ((attributes & ParameterAttributes.HasDefault) != 0 ||
                            (flags & TemplateMetadataWriter.ParameterFlags.HasOutDefaultValue) != 0) {
                            defaultValue = ReadTypeOrConstant(underlyingType, _reader).constant;
                            Debug.Assert(defaultValue is not null);
                        }

                        _parameters[i] = (name, attributes, flags, underlyingType, defaultValue);
                        endPosition = (uint)_reader.BaseStream.Position;
                    }

                    _offsetAfterParameters = (uint)_reader.BaseStream.Position;
                    _readParameters = true;
                }
            }
        }
    }
}
