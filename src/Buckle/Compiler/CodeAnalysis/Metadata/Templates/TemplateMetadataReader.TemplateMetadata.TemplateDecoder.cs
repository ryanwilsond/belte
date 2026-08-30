using System.Diagnostics;
using System.IO;
using System.Text;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataReader {
    internal sealed partial class TemplateMetadata {
        internal abstract class TemplateDecoder {
            private protected readonly TemplateMetadata _metadata;
            private protected readonly uint _offset;
            private protected readonly uint _size;

            private Symbol _enclosingContext;

            private protected TemplateDecoder(TemplateMetadata metadata, uint offset, uint size) {
                _metadata = metadata;
                _offset = offset;
                _size = size;
            }

            private protected BinaryReader _reader => _metadata._reader;

            private protected Compilation _compilation => _metadata._compilation;

            internal Symbol enclosingContext => _enclosingContext;

            internal void SetEnclosingContext(Symbol symbol) {
                _enclosingContext = symbol;
            }

            internal TypeSymbol ReadTypeSymbol(byte kind, BinaryReader reader) {
                switch (kind) {
                    case 1:
                        var specialType = (SpecialType)reader.ReadByte();

                        if (!specialType.CanEncodeToTemplateMetadata())
                            return null;

                        return _compilation.GetSpecialType(specialType);
                    case 2: {
                            var nestedKind = reader.ReadByte();
                            var elementType = ReadTypeSymbol(nestedKind, reader);

                            return ArrayTypeSymbol.CreateSZArray(
                                _compilation.assembly,
                                new TypeWithAnnotations(elementType)
                            );
                        }
                    case 3: {
                            var nestedKind = reader.ReadByte();
                            var elementType = ReadTypeSymbol(nestedKind, reader);
                            return new PointerTypeSymbol(new TypeWithAnnotations(elementType));
                        }
                    case 4: {
                            var templateParameterKind = (TemplateParameterKind)reader.ReadByte();
                            var ordinal = reader.ReadUInt16();

                            Debug.Assert(_enclosingContext is not null);

                            switch (templateParameterKind) {
                                case TemplateParameterKind.Method: {
                                        if (_enclosingContext is not MethodSymbol method
                                            || method.GetArity() <= ordinal) {
                                            return null;
                                        }

                                        return method.templateParameters[ordinal];
                                    }
                                case TemplateParameterKind.Type: {
                                        if (_enclosingContext is MethodSymbol method) {
                                            var enclosing = method.containingType;

                                            if (enclosing.arity <= ordinal)
                                                return null;

                                            return enclosing.templateParameters[ordinal];
                                        } else {
                                            var enclosing = (NamedTypeSymbol)_enclosingContext;

                                            if (enclosing.arity <= ordinal)
                                                return null;

                                            return enclosing.templateParameters[ordinal];
                                        }
                                    }
                                default:
                                    return null;
                            }
                        }
                    case 5: {
                            var parameterCount = reader.ReadUInt16();
                            var parameterTypes = ArrayBuilder<TypeWithAnnotations>.GetInstance(parameterCount);

                            for (var i = 0; i < parameterCount; i++) {
                                var paramKind = reader.ReadByte();
                                parameterTypes.Add(new TypeWithAnnotations(ReadTypeSymbol(paramKind, reader)));
                            }

                            var returnKind = reader.ReadByte();
                            var returnType = new TypeWithAnnotations(ReadTypeSymbol(returnKind, reader));

                            return FunctionPointerTypeSymbol.CreateFromParts(
                                CallingConvention.Unspecified,
                                returnType,
                                RefKind.None,
                                parameterTypes.ToImmutableAndFree(),
                                default
                            );
                        }
                    case 6: {
                            var parameterCount = reader.ReadUInt16();
                            var parameterTypes = ArrayBuilder<TypeWithAnnotations>.GetInstance(parameterCount);

                            for (var i = 0; i < parameterCount; i++) {
                                var paramKind = reader.ReadByte();
                                parameterTypes.Add(new TypeWithAnnotations(ReadTypeSymbol(paramKind, reader)));
                            }

                            var returnKind = reader.ReadByte();
                            var returnType = new TypeWithAnnotations(ReadTypeSymbol(returnKind, reader));

                            return FunctionTypeSymbol.CreateFromParts(
                                returnType,
                                RefKind.None,
                                parameterTypes.ToImmutableAndFree(),
                                default
                            );
                        }
                    case 7: {
                            var typeEntryIndex = reader.ReadUInt32();
                            var typeSymbol = _metadata.ResolveType(typeEntryIndex);

                            if (typeSymbol is not NamedTypeSymbol namedTypeSymbol)
                                return null;

                            var arity = namedTypeSymbol.arity;
                            var builder = ArrayBuilder<TypeOrConstant>.GetInstance(arity);

                            for (var i = 0; i < arity; i++) {
                                var expectedType = namedTypeSymbol.templateParameters[i].underlyingType;
                                var typeOrConstant = ReadTypeOrConstant(expectedType.type, reader);

                                if (typeOrConstant is null)
                                    return null;

                                builder.Add(typeOrConstant);
                            }

                            return namedTypeSymbol.Construct(builder.ToImmutableAndFree());
                        }
                    case 8: {
                            var typeEntryIndex = reader.ReadUInt32();
                            return _metadata.ResolveType(typeEntryIndex);
                        }
                    default:
                        Debug.Assert(false);
                        return null;
                }
            }

            internal TypeOrConstant ReadTypeOrConstant(TypeSymbol underlyingType, BinaryReader reader) {
                switch (underlyingType.specialType) {
                    case SpecialType.Type:
                        var argumentKind = reader.ReadByte();
                        return new TypeOrConstant(ReadTypeSymbol(argumentKind, reader));
                    case SpecialType.String: {
                            var size = reader.ReadUInt32();
                            var value = Encoding.UTF8.GetString(reader.ReadBytes((int)size));
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.String));
                        }
                    case SpecialType.Bool: {
                            var value = reader.ReadBoolean();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.Bool));
                        }
                    case SpecialType.WinBool: {
                            var value = reader.ReadInt32();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.WinBool));
                        }
                    case SpecialType.Char: {
                            var value = reader.ReadChar();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.Char));
                        }
                    case SpecialType.Int8: {
                            var value = reader.ReadSByte();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.Int8));
                        }
                    case SpecialType.UInt8: {
                            var value = reader.ReadByte();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.UInt8));
                        }
                    case SpecialType.Int16: {
                            var value = reader.ReadInt16();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.Int16));
                        }
                    case SpecialType.UInt16: {
                            var value = reader.ReadUInt16();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.UInt16));
                        }
                    case SpecialType.Int32: {
                            var value = reader.ReadInt32();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.Int32));
                        }
                    case SpecialType.UInt32: {
                            var value = reader.ReadUInt32();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.UInt32));
                        }
                    case SpecialType.Int: {
                            var value = reader.ReadInt64();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.Int));
                        }
                    case SpecialType.Int64: {
                            var value = reader.ReadInt64();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.Int64));
                        }
                    case SpecialType.UInt64: {
                            var value = reader.ReadUInt64();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.UInt64));
                        }
                    case SpecialType.Float32: {
                            var value = reader.ReadSingle();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.Float32));
                        }
                    case SpecialType.Decimal: {
                            var value = reader.ReadDouble();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.Decimal));
                        }
                    case SpecialType.Float64: {
                            var value = reader.ReadDouble();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.Float64));
                        }
                    case SpecialType.IntPtr: {
                            var value = reader.ReadInt64();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.IntPtr));
                        }
                    case SpecialType.UIntPtr: {
                            var value = reader.ReadUInt64();
                            return new TypeOrConstant(new ConstantValue(value, SpecialType.UIntPtr));
                        }
                    default:
                        Debug.Assert(false);
                        return null;
                }
            }

            private protected AttributeData[] DecodeCustomAttributesCore(uint startPosition) {
                lock (_metadata) lock (this) lock (_reader) {
                    var position = _reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);
                    Debug.Assert(_reader.BaseStream.Position == position && position == startPosition);

                    var count = _reader.ReadUInt16();
                    Debug.Assert(_reader.BaseStream.Position == startPosition + 2);

                    var attributes = new AttributeData[count];

                    for (var i = 0; i < count; i++) {
                        var size = _reader.ReadUInt32();
                        var constructorIndex = _reader.ReadUInt32();
                        var constructor = _metadata.ResolveMethod(constructorIndex);
                        Debug.Assert(constructor.methodKind == MethodKind.Constructor);
                        var parameterTypes = constructor.GetParameterTypes();

                        var arguments = ArrayBuilder<TypedConstant>.GetInstance(parameterTypes.Length);

                        for (var j = 0; j < parameterTypes.Length; j++) {
                            var parameterType = parameterTypes[j].type;
                            arguments.Add(DecodeTypedConstant(parameterType));
                        }

                        attributes[i] = new MetadataAttributeData(
                            _compilation,
                            constructor.containingType,
                            constructor,
                            arguments.ToImmutableAndFree()
                        );
                    }

                    return attributes;
                }

                TypedConstant DecodeTypedConstant(TypeSymbol type) {
                    var typedConstantKind = type.GetAttributeParameterTypedConstantKind(_compilation);

                    if (type.IsNullableType()) {
                        var isNull = _reader.ReadBoolean();

                        if (isNull)
                            return new TypedConstant(type, typedConstantKind, null);
                    }

                    if (type.StrippedType() is ArrayTypeSymbol arrayType) {
                        var count = _reader.ReadUInt32();
                        var builder = ArrayBuilder<TypedConstant>.GetInstance((int)count);

                        for (var i = 0; i < count; i++)
                            builder.Add(DecodeTypedConstant(arrayType.elementType));

                        return new TypedConstant(type, builder.ToImmutableAndFree());
                    }

                    var value = ReadTypeOrConstant(type.StrippedType(), _reader);
                    Debug.Assert(value.isConstant);
                    return new TypedConstant(type, typedConstantKind, value.constant.value);
                }
            }
        }
    }
}
