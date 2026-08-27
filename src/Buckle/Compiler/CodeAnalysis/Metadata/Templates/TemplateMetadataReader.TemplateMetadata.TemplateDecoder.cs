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

                                switch (expectedType.specialType) {
                                    case SpecialType.Type:
                                        var argumentKind = reader.ReadByte();
                                        builder.Add(new TypeOrConstant(ReadTypeSymbol(argumentKind, reader)));
                                        break;
                                    case SpecialType.String: {
                                            var size = reader.ReadUInt32();
                                            var value = Encoding.UTF8.GetString(reader.ReadBytes((int)size));
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.String)));
                                            break;
                                        }
                                    case SpecialType.Bool: {
                                            var value = reader.ReadBoolean();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.Bool)));
                                            break;
                                        }
                                    case SpecialType.WinBool: {
                                            var value = reader.ReadInt32();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.WinBool)));
                                            break;
                                        }
                                    case SpecialType.Char: {
                                            var value = reader.ReadChar();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.Char)));
                                            break;
                                        }
                                    case SpecialType.Int8: {
                                            var value = reader.ReadSByte();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.Int8)));
                                            break;
                                        }
                                    case SpecialType.UInt8: {
                                            var value = reader.ReadByte();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.UInt8)));
                                            break;
                                        }
                                    case SpecialType.Int16: {
                                            var value = reader.ReadInt16();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.Int16)));
                                            break;
                                        }
                                    case SpecialType.UInt16: {
                                            var value = reader.ReadUInt16();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.UInt16)));
                                            break;
                                        }
                                    case SpecialType.Int32: {
                                            var value = reader.ReadInt32();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.Int32)));
                                            break;
                                        }
                                    case SpecialType.UInt32: {
                                            var value = reader.ReadUInt32();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.UInt32)));
                                            break;
                                        }
                                    case SpecialType.Int: {
                                            var value = reader.ReadInt64();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.Int)));
                                            break;
                                        }
                                    case SpecialType.Int64: {
                                            var value = reader.ReadInt64();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.Int64)));
                                            break;
                                        }
                                    case SpecialType.UInt64: {
                                            var value = reader.ReadUInt64();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.UInt64)));
                                            break;
                                        }
                                    case SpecialType.Float32: {
                                            var value = reader.ReadSingle();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.Float32)));
                                            break;
                                        }
                                    case SpecialType.Decimal: {
                                            var value = reader.ReadDouble();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.Decimal)));
                                            break;
                                        }
                                    case SpecialType.Float64: {
                                            var value = reader.ReadDouble();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.Float64)));
                                            break;
                                        }
                                    case SpecialType.IntPtr: {
                                            var value = reader.ReadInt64();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.IntPtr)));
                                            break;
                                        }
                                    case SpecialType.UIntPtr: {
                                            var value = reader.ReadUInt64();
                                            builder.Add(new TypeOrConstant(new ConstantValue(value, SpecialType.UIntPtr)));
                                            break;
                                        }
                                    default:
                                        return null;
                                }
                            }

                            return namedTypeSymbol.Construct(builder.ToImmutableAndFree());
                        }
                    case 8: {
                            var typeEntryIndex = reader.ReadUInt32();
                            return _metadata.ResolveType(typeEntryIndex);
                        }
                    default:
                        return null;
                }
            }
        }
    }
}
