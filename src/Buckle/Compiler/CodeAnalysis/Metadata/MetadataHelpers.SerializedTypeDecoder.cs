using System.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal static partial class MetadataHelpers {
    private struct SerializedTypeDecoder {
        private static readonly char[] TypeNameDelimiters = ['+', ',', '[', ']', '*'];

        private readonly string _input;
        private int _offset;

        internal SerializedTypeDecoder(string s) {
            _input = s;
            _offset = 0;
        }

        private void Advance() {
            if (!EndOfInput) {
                _offset++;
            }
        }

        private void AdvanceTo(int i) {
            if (i <= _input.Length) {
                _offset = i;
            }
        }

        private bool EndOfInput => _offset >= _input.Length;

        private char Current => _input[_offset];

        internal AssemblyQualifiedTypeName DecodeTypeName(
            bool isTypeArgument = false,
            bool isTypeArgumentWithAssemblyName = false) {
            string topLevelType = null;
            ArrayBuilder<string> nestedTypesBuilder = null;
            AssemblyQualifiedTypeName[] typeArguments = null;
            var pointerCount = 0;
            ArrayBuilder<int> arrayRanksBuilder = null;
            string assemblyName = null;
            var decodingTopLevelType = true;
            var isGenericTypeName = false;

            var pooledStrBuilder = PooledStringBuilder.GetInstance();
            var typeNameBuilder = pooledStrBuilder.Builder;

            while (!EndOfInput) {
                var i = _input.IndexOfAny(TypeNameDelimiters, _offset);

                if (i >= 0) {
                    var c = _input[i];
                    var decodedString = DecodeGenericName(i);

                    isGenericTypeName = isGenericTypeName || decodedString.Contains(GenericTypeNameManglingChar);
                    typeNameBuilder.Append(decodedString);

                    switch (c) {
                        case '*':
                            if (arrayRanksBuilder is not null)
                                typeNameBuilder.Append(c);
                            else
                                pointerCount++;

                            Advance();
                            break;
                        case '+':
                            if (arrayRanksBuilder is not null || pointerCount > 0) {
                                typeNameBuilder.Append(c);
                            } else {
                                HandleDecodedTypeName(
                                    typeNameBuilder.ToString(),
                                    decodingTopLevelType,
                                    ref topLevelType,
                                    ref nestedTypesBuilder
                                );

                                typeNameBuilder.Clear();
                                decodingTopLevelType = false;
                            }

                            Advance();
                            break;
                        case '[':
                            if (isGenericTypeName && typeArguments is null) {
                                Advance();

                                if (arrayRanksBuilder is not null || pointerCount > 0)
                                    typeNameBuilder.Append(c);
                                else
                                    typeArguments = DecodeTypeArguments();
                            } else {
                                DecodeArrayShape(typeNameBuilder, ref arrayRanksBuilder);
                            }

                            break;
                        case ']':
                            if (isTypeArgument) {
                                goto ExitDecodeTypeName;
                            } else {
                                typeNameBuilder.Append(c);
                                Advance();
                                break;
                            }
                        case ',':
                            if (!isTypeArgument || isTypeArgumentWithAssemblyName) {
                                Advance();

                                if (!EndOfInput && char.IsWhiteSpace(Current))
                                    Advance();

                                assemblyName = DecodeAssemblyName(isTypeArgumentWithAssemblyName);
                            }

                            goto ExitDecodeTypeName;
                        default:
                            throw ExceptionUtilities.UnexpectedValue(c);
                    }
                } else {
                    typeNameBuilder.Append(DecodeGenericName(_input.Length));
                    goto ExitDecodeTypeName;
                }
            }

ExitDecodeTypeName:
            HandleDecodedTypeName(
                typeNameBuilder.ToString(),
                decodingTopLevelType,
                ref topLevelType,
                ref nestedTypesBuilder
            );

            pooledStrBuilder.Free();

            return new AssemblyQualifiedTypeName(
                topLevelType,
                nestedTypesBuilder?.ToArrayAndFree(),
                typeArguments,
                pointerCount,
                arrayRanksBuilder?.ToArrayAndFree(),
                assemblyName
            );
        }

        private static void HandleDecodedTypeName(
            string decodedTypeName,
            bool decodingTopLevelType,
            ref string topLevelType,
            ref ArrayBuilder<string> nestedTypesBuilder) {
            if (decodedTypeName.Length != 0) {
                if (decodingTopLevelType) {
                    topLevelType = decodedTypeName;
                } else {
                    nestedTypesBuilder ??= ArrayBuilder<string>.GetInstance();
                    nestedTypesBuilder.Add(decodedTypeName);
                }
            }
        }

        private string DecodeGenericName(int i) {
            var length = i - _offset;

            if (length == 0)
                return "";

            var start = _offset;
            AdvanceTo(i);

            return _input.Substring(start, _offset - start);
        }

        private AssemblyQualifiedTypeName[] DecodeTypeArguments() {
            if (EndOfInput)
                return null;

            var typeBuilder = ArrayBuilder<AssemblyQualifiedTypeName>.GetInstance();

            while (!EndOfInput) {
                typeBuilder.Add(DecodeTypeArgument());

                if (!EndOfInput) {
                    switch (Current) {
                        case ',':
                            Advance();

                            if (!EndOfInput && char.IsWhiteSpace(Current))
                                Advance();

                            break;
                        case ']':
                            Advance();
                            return typeBuilder.ToArrayAndFree();
                        default:
                            throw ExceptionUtilities.UnexpectedValue(EndOfInput);
                    }
                }
            }

            return typeBuilder.ToArrayAndFree();
        }

        private AssemblyQualifiedTypeName DecodeTypeArgument() {
            var isTypeArgumentWithAssemblyName = false;

            if (Current == '[') {
                isTypeArgumentWithAssemblyName = true;
                Advance();
            }

            var result = DecodeTypeName(
                isTypeArgument: true,
                isTypeArgumentWithAssemblyName: isTypeArgumentWithAssemblyName
            );

            if (isTypeArgumentWithAssemblyName) {
                if (!EndOfInput && Current == ']')
                    Advance();
            }

            return result;
        }

        private string DecodeAssemblyName(bool isTypeArgumentWithAssemblyName) {
            if (EndOfInput)
                return null;

            int i;

            if (isTypeArgumentWithAssemblyName) {
                i = _input.IndexOf(']', _offset);

                if (i < 0)
                    i = _input.Length;
            } else {
                i = _input.Length;
            }

            var name = _input.Substring(_offset, i - _offset);
            AdvanceTo(i);
            return name;
        }

        private void DecodeArrayShape(StringBuilder typeNameBuilder, ref ArrayBuilder<int> arrayRanksBuilder) {
            var start = _offset;
            var rank = 1;
            var isMultiDimensionalIfRankOne = false;
            Advance();

            while (!EndOfInput) {
                switch (Current) {
                    case ',':
                        rank++;
                        Advance();
                        break;
                    case ']':
                        arrayRanksBuilder ??= ArrayBuilder<int>.GetInstance();
                        arrayRanksBuilder.Add(rank == 1 && !isMultiDimensionalIfRankOne ? 0 : rank);
                        Advance();
                        return;
                    case '*':
                        if (rank != 1)
                            goto default;

                        Advance();

                        if (Current != ']') {
                            typeNameBuilder.Append(_input.Substring(start, _offset - start));
                            return;
                        }

                        isMultiDimensionalIfRankOne = true;
                        break;
                    default:
                        Advance();
                        typeNameBuilder.Append(_input.Substring(start, _offset - start));
                        return;
                }
            }

            typeNameBuilder.Append(_input.Substring(start, _offset - start));
        }
    }
}
