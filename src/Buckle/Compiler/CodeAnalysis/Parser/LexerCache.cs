using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

using SyntaxTrivia = Buckle.CodeAnalysis.Syntax.InternalSyntax.SyntaxTrivia;
using SyntaxToken = Buckle.CodeAnalysis.Syntax.InternalSyntax.SyntaxToken;

internal partial class LexerCache {
    private static readonly ObjectPool<CachingIdentityFactory<string, SyntaxKind>> KeywordKindPool =
        CachingIdentityFactory<string, SyntaxKind>.CreatePool(
            512,
            (key) => {
                var kind = SyntaxFacts.GetKeywordType(key);

                // if (kind == SyntaxKind.None) {
                //     kind = SyntaxFacts.GetContextualKeywordKind(key);
                // }

                return kind;
            }
        );

    private readonly TextKeyedCache<SyntaxTrivia> _triviaMap;
    private readonly TextKeyedCache<SyntaxToken> _tokenMap;
    private readonly CachingIdentityFactory<string, SyntaxKind> _keywordKindMap;
    internal const int MaxKeywordLength = 10;

    internal LexerCache() {
        _triviaMap = TextKeyedCache<SyntaxTrivia>.GetInstance();
        _tokenMap = TextKeyedCache<SyntaxToken>.GetInstance();
        _keywordKindMap = KeywordKindPool.Allocate();
    }

    internal void Free() {
        _keywordKindMap.Free();
        _triviaMap.Free();
        _tokenMap.Free();
    }

    internal bool TryGetKeywordKind(string key, out SyntaxKind kind) {
        if (key.Length > MaxKeywordLength) {
            kind = SyntaxKind.None;
            return false;
        }

        kind = _keywordKindMap.GetOrMakeValue(key);
        return kind != SyntaxKind.None;
    }

    internal SyntaxTrivia LookupTrivia<TArg>(
        char[] textBuffer,
        int keyStart,
        int keyLength,
        int hashCode,
        Func<TArg, int, SyntaxTrivia> createTriviaFunction,
        TArg data) {
        var value = _triviaMap.FindItem(textBuffer, keyStart, keyLength, hashCode);

        if (value is null) {
            value = createTriviaFunction(data, keyStart);
            _triviaMap.AddItem(textBuffer, keyStart, keyLength, hashCode, value);
        }

        return value;
    }

    internal SyntaxToken LookupToken<TArg>(
        char[] textBuffer,
        int keyStart,
        int keyLength,
        int hashCode,
        Func<TArg, int, SyntaxToken> createTokenFunction,
        TArg data) {
        var value = _tokenMap.FindItem(textBuffer, keyStart, keyLength, hashCode);

        if (value is null) {
            value = createTokenFunction(data, keyStart);
            _tokenMap.AddItem(textBuffer, keyStart, keyLength, hashCode, value);
        }

        return value;
    }
}
