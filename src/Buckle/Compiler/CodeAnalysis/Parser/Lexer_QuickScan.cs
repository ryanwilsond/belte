using System;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class Lexer {
    internal const int MaxCachedTokenSize = 42;

    private char[] _lazyCharWindow;

    private static readonly byte[,] StateTransitions = new byte[,] {
        // Initial
        {
            (byte)QuickScanState.Initial,               // White
            (byte)QuickScanState.Initial,               // CR
            (byte)QuickScanState.Initial,               // LF
            (byte)QuickScanState.Ident,                 // Letter
            (byte)QuickScanState.Number,                // Digit
            (byte)QuickScanState.Punctuation,           // Punct
            (byte)QuickScanState.Dot,                   // Dot
            (byte)QuickScanState.CompoundPunctStart,    // Compound
            (byte)QuickScanState.Bad,                   // Slash
            (byte)QuickScanState.Bad,                   // Complex
            (byte)QuickScanState.Bad,                   // EndOfFile
        },
        // Following White
        {
            (byte)QuickScanState.FollowingWhite,        // White
            (byte)QuickScanState.FollowingCR,           // CR
            (byte)QuickScanState.DoneAfterNext,         // LF
            (byte)QuickScanState.Done,                  // Letter
            (byte)QuickScanState.Done,                  // Digit
            (byte)QuickScanState.Done,                  // Punct
            (byte)QuickScanState.Done,                  // Dot
            (byte)QuickScanState.Done,                  // Compound
            (byte)QuickScanState.Bad,                   // Slash
            (byte)QuickScanState.Bad,                   // Complex
            (byte)QuickScanState.Done,                  // EndOfFile
        },
        // Following CR
        {
            (byte)QuickScanState.Done,                  // White
            (byte)QuickScanState.Done,                  // CR
            (byte)QuickScanState.DoneAfterNext,         // LF
            (byte)QuickScanState.Done,                  // Letter
            (byte)QuickScanState.Done,                  // Digit
            (byte)QuickScanState.Done,                  // Punct
            (byte)QuickScanState.Done,                  // Dot
            (byte)QuickScanState.Done,                  // Compound
            (byte)QuickScanState.Done,                  // Slash
            (byte)QuickScanState.Done,                  // Complex
            (byte)QuickScanState.Done,                  // EndOfFile
        },
        // Identifier
        {
            (byte)QuickScanState.FollowingWhite,        // White
            (byte)QuickScanState.FollowingCR,           // CR
            (byte)QuickScanState.DoneAfterNext,         // LF
            (byte)QuickScanState.Ident,                 // Letter
            (byte)QuickScanState.Ident,                 // Digit
            (byte)QuickScanState.Done,                  // Punct
            (byte)QuickScanState.Done,                  // Dot
            (byte)QuickScanState.Done,                  // Compound
            (byte)QuickScanState.Bad,                   // Slash
            (byte)QuickScanState.Bad,                   // Complex
            (byte)QuickScanState.Done,                  // EndOfFile
        },
        // Number
        {
            (byte)QuickScanState.FollowingWhite,        // White
            (byte)QuickScanState.FollowingCR,           // CR
            (byte)QuickScanState.DoneAfterNext,         // LF
            (byte)QuickScanState.Bad,                   // Letter
            (byte)QuickScanState.Number,                // Digit
            (byte)QuickScanState.Done,                  // Punct
            (byte)QuickScanState.Bad,                   // Dot
            (byte)QuickScanState.Done,                  // Compound
            (byte)QuickScanState.Bad,                   // Slash
            (byte)QuickScanState.Bad,                   // Complex
            (byte)QuickScanState.Done,                  // EndOfFile
        },
        // Punctuation
        {
            (byte)QuickScanState.FollowingWhite,        // White
            (byte)QuickScanState.FollowingCR,           // CR
            (byte)QuickScanState.DoneAfterNext,         // LF
            (byte)QuickScanState.Done,                  // Letter
            (byte)QuickScanState.Done,                  // Digit
            (byte)QuickScanState.Done,                  // Punct
            (byte)QuickScanState.Done,                  // Dot
            (byte)QuickScanState.Done,                  // Compound
            (byte)QuickScanState.Bad,                   // Slash
            (byte)QuickScanState.Bad,                   // Complex
            (byte)QuickScanState.Done,                  // EndOfFile
        },
        // Dot
        {
            (byte)QuickScanState.FollowingWhite,        // White
            (byte)QuickScanState.FollowingCR,           // CR
            (byte)QuickScanState.DoneAfterNext,         // LF
            (byte)QuickScanState.Done,                  // Letter
            (byte)QuickScanState.Bad,                   // Digit // TODO Could be Number if we someone account for inline IL
            (byte)QuickScanState.Done,                  // Punct
            (byte)QuickScanState.Bad,                   // Dot
            (byte)QuickScanState.Done,                  // Compound
            (byte)QuickScanState.Bad,                   // Slash
            (byte)QuickScanState.Bad,                   // Complex
            (byte)QuickScanState.Done,                  // EndOfFile
        },
        // Compound Punctuation
        {
            (byte)QuickScanState.FollowingWhite,        // White
            (byte)QuickScanState.FollowingCR,           // CR
            (byte)QuickScanState.DoneAfterNext,         // LF
            (byte)QuickScanState.Done,                  // Letter
            (byte)QuickScanState.Done,                  // Digit
            (byte)QuickScanState.Bad,                   // Punct
            (byte)QuickScanState.Bad,                   // Dot // TODO Could be Done if we account for ?. and ?..
            (byte)QuickScanState.Bad,                   // Compound
            (byte)QuickScanState.Bad,                   // Slash
            (byte)QuickScanState.Bad,                   // Complex
            (byte)QuickScanState.Done,                  // EndOfFile
        },
        // Done after next
        {
            (byte)QuickScanState.Done,                  // White
            (byte)QuickScanState.Done,                  // CR
            (byte)QuickScanState.Done,                  // LF
            (byte)QuickScanState.Done,                  // Letter
            (byte)QuickScanState.Done,                  // Digit
            (byte)QuickScanState.Done,                  // Punct
            (byte)QuickScanState.Done,                  // Dot
            (byte)QuickScanState.Done,                  // Compound
            (byte)QuickScanState.Done,                  // Slash
            (byte)QuickScanState.Done,                  // Complex
            (byte)QuickScanState.Done,                  // EndOfFile
        },
    };
    private static ReadOnlySpan<byte> CharProperties => [
        (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex,
        (byte)CharFlags.Complex,
        (byte)CharFlags.White,   // TAB
        (byte)CharFlags.LF,      // LF
        (byte)CharFlags.White,   // VT
        (byte)CharFlags.White,   // FF
        (byte)CharFlags.CR,      // CR
        (byte)CharFlags.Complex,
        (byte)CharFlags.Complex,
        (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex,
        (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex,

        // 32 .. 63
        (byte)CharFlags.White,    // SPC
        (byte)CharFlags.CompoundPunctStart, // !
        (byte)CharFlags.Complex,  // "
        (byte)CharFlags.Complex,  // #
        (byte)CharFlags.CompoundPunctStart, // $
        (byte)CharFlags.CompoundPunctStart, // %
        (byte)CharFlags.CompoundPunctStart, // &
        (byte)CharFlags.Complex,  // '
        (byte)CharFlags.Punct,    // (
        (byte)CharFlags.Punct,    // )
        (byte)CharFlags.CompoundPunctStart, // *
        (byte)CharFlags.CompoundPunctStart, // +
        (byte)CharFlags.Punct,    // ,
        (byte)CharFlags.CompoundPunctStart, // -
        (byte)CharFlags.Dot,      // .
        (byte)CharFlags.Slash,    // /
        (byte)CharFlags.Digit,    // 0
        (byte)CharFlags.Digit,    // 1
        (byte)CharFlags.Digit,    // 2
        (byte)CharFlags.Digit,    // 3
        (byte)CharFlags.Digit,    // 4
        (byte)CharFlags.Digit,    // 5
        (byte)CharFlags.Digit,    // 6
        (byte)CharFlags.Digit,    // 7
        (byte)CharFlags.Digit,    // 8
        (byte)CharFlags.Digit,    // 9
        (byte)CharFlags.CompoundPunctStart,  // :
        (byte)CharFlags.Punct,    // ;
        (byte)CharFlags.CompoundPunctStart,  // <
        (byte)CharFlags.CompoundPunctStart,  // =
        (byte)CharFlags.CompoundPunctStart,  // >
        (byte)CharFlags.CompoundPunctStart,  // ?

        // 64 .. 95
        (byte)CharFlags.Complex,  // @
        (byte)CharFlags.Letter,   // A
        (byte)CharFlags.Letter,   // B
        (byte)CharFlags.Letter,   // C
        (byte)CharFlags.Letter,   // D
        (byte)CharFlags.Letter,   // E
        (byte)CharFlags.Letter,   // F
        (byte)CharFlags.Letter,   // G
        (byte)CharFlags.Letter,   // H
        (byte)CharFlags.Letter,   // I
        (byte)CharFlags.Letter,   // J
        (byte)CharFlags.Letter,   // K
        (byte)CharFlags.Letter,   // L
        (byte)CharFlags.Letter,   // M
        (byte)CharFlags.Letter,   // N
        (byte)CharFlags.Letter,   // O
        (byte)CharFlags.Letter,   // P
        (byte)CharFlags.Letter,   // Q
        (byte)CharFlags.Letter,   // R
        (byte)CharFlags.Letter,   // S
        (byte)CharFlags.Letter,   // T
        (byte)CharFlags.Letter,   // U
        (byte)CharFlags.Letter,   // V
        (byte)CharFlags.Letter,   // W
        (byte)CharFlags.Letter,   // X
        (byte)CharFlags.Letter,   // Y
        (byte)CharFlags.Letter,   // Z
        (byte)CharFlags.Punct,    // [
        (byte)CharFlags.Complex,  // \
        (byte)CharFlags.Punct,    // ]
        (byte)CharFlags.CompoundPunctStart,    // ^
        (byte)CharFlags.Letter,   // _

        // 96 .. 127
        (byte)CharFlags.Complex,  // `
        (byte)CharFlags.Letter,   // a
        (byte)CharFlags.Letter,   // b
        (byte)CharFlags.Letter,   // c
        (byte)CharFlags.Letter,   // d
        (byte)CharFlags.Letter,   // e
        (byte)CharFlags.Complex,  // f
        (byte)CharFlags.Letter,   // g
        (byte)CharFlags.Letter,   // h
        (byte)CharFlags.Letter,   // i
        (byte)CharFlags.Letter,   // j
        (byte)CharFlags.Letter,   // k
        (byte)CharFlags.Letter,   // l
        (byte)CharFlags.Letter,   // m
        (byte)CharFlags.Letter,   // n
        (byte)CharFlags.Letter,   // o
        (byte)CharFlags.Letter,   // p
        (byte)CharFlags.Letter,   // q
        (byte)CharFlags.Letter,   // r
        (byte)CharFlags.Letter,   // s
        (byte)CharFlags.Letter,   // t
        (byte)CharFlags.Letter,   // u
        (byte)CharFlags.Letter,   // v
        (byte)CharFlags.Letter,   // w
        (byte)CharFlags.Letter,   // x
        (byte)CharFlags.Letter,   // y
        (byte)CharFlags.Letter,   // z
        (byte)CharFlags.Punct,    // {
        (byte)CharFlags.CompoundPunctStart,  // |
        (byte)CharFlags.Punct,    // }
        (byte)CharFlags.CompoundPunctStart,  // ~
        (byte)CharFlags.Complex,

        // 128 .. 159
        (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex,
        (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex,
        (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex,
        (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex,

        // 160 .. 191
        (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex,
        (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Letter, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex,
        (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Letter, (byte)CharFlags.Complex, (byte)CharFlags.Complex,
        (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Letter, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex, (byte)CharFlags.Complex,

        // 192 ..
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Complex,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,

        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Complex,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,

        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,

        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,

        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,

        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter,
        (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter, (byte)CharFlags.Letter
    ];

    private SyntaxToken QuickNext() {
        var start = _position;
        var state = QuickScanState.Initial;
        var i = _position;
        var n = text.length;
        n = Math.Min(n, i + MaxCachedTokenSize);

        var hashCode = Hash.FnvOffsetBias;

        var charWindow = GetTextAsCharWindow();
        var charPropLength = CharProperties.Length;

        for (; i < n; i++) {
            var c = charWindow[i];
            var uc = unchecked((int)c);

            var flags = uc < charPropLength ? (CharFlags)CharProperties[uc] : CharFlags.Complex;

            state = (QuickScanState)StateTransitions[(int)state, (int)flags];

            if (state >= QuickScanState.Done)
                goto exitWhile;

            hashCode = unchecked((hashCode ^ uc) * Hash.FnvPrime);
        }

        state = QuickScanState.Bad;
exitWhile:

        _position += i - _position;

        if (state == QuickScanState.Done) {
            var token = _cache.LookupToken(
                charWindow,
                start,
                i - start,
                hashCode,
                CreateQuickToken,
                this
            );

            return token;
        } else {
            _position = start;
            return null;
        }
    }

    private static SyntaxToken CreateQuickToken(Lexer lexer, int start) {
        lexer._position = start;
        var token = lexer.LexNextInternal();
        return token;
    }

    private char[] GetTextAsCharWindow() {
        // TODO Expensive operation. Using a text window might be better to prevent excessive scanning
        // TODO A text window would also allow LargeText to do its job and lessen memory strain
        _lazyCharWindow ??= text.ToString().ToCharArray();

        return _lazyCharWindow;
    }
}
