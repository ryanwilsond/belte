using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace BelteLspServer;

internal class SemanticTokensHandler : SemanticTokensHandlerBase {
    private readonly CompilationManager _manager;
    private readonly ILogger<SemanticTokensHandler> _logger;

    public SemanticTokensHandler(ILogger<SemanticTokensHandler> logger, CompilationManager manager) {
        _logger = logger;
        _manager = manager;
    }

    protected override async Task Tokenize(
        SemanticTokensBuilder builder,
        ITextDocumentIdentifierParams identifier,
        CancellationToken cancellationToken) {
        await Task.Yield();

        var tree = _manager.GetTree(identifier.TextDocument.Uri);

        if (tree is null)
            return;

        // _manager.compilation.GetSemanticModel()

        // TokenizeNode(builder, syntaxTree.GetRoot());

        TokenizeNode(tree.GetRoot());

        void TokenizeNode(SyntaxNodeOrToken node) {
            if (node.isToken) {
                TokenizeToken(node.AsToken());
            } else {
                foreach (var child in node.ChildNodesAndTokens())
                    TokenizeNode(child);
            }
        }

        void TokenizeToken(SyntaxToken token) {
            foreach (var trivia in token.leadingTrivia)
                TokenizeTrivia(trivia);

            TokenizeTokenContinued(token);

            foreach (var trivia in token.trailingTrivia)
                TokenizeTrivia(trivia);
        }

        void TokenizeTrivia(SyntaxTrivia trivia) {
            TokenizeSimpleCase(trivia.kind, trivia.location, trivia.span);
        }

        void TokenizeSimpleCase(SyntaxKind kind, TextLocation location, TextSpan span) {
            SemanticTokenType tokenType = null;

            if (kind.IsKeyword())
                tokenType = SemanticTokenType.Keyword;
            else if (kind == SyntaxKind.NumericLiteralToken)
                tokenType = SemanticTokenType.Number;
            else if (kind is SyntaxKind.StringLiteralToken or SyntaxKind.CharacterLiteralToken)
                tokenType = SemanticTokenType.String;
            else if (kind.IsComment())
                tokenType = SemanticTokenType.Comment;
            else if (SyntaxFacts.GetBinaryPrecedence(kind) + SyntaxFacts.GetUnaryPrecedence(kind) + SyntaxFacts.GetTernaryPrecedence(kind) > 0)
                tokenType = SemanticTokenType.Operator;

            builder.Push(location.startLine, location.startCharacter, span.length, (string)tokenType);
        }

        void TokenizeTokenContinued(SyntaxToken token) {
            var kind = token.kind;

            if (kind != SyntaxKind.IdentifierToken) {
                TokenizeSimpleCase(kind, token.location, token.span);
                return;
            }

            var parentKind = token.parent.kind;
            SemanticTokenType tokenType = null;

            if (parentKind == SyntaxKind.ClassDeclaration) {
                tokenType = SemanticTokenType.Class;
            } else if (parentKind == SyntaxKind.StructDeclaration) {
                tokenType = SemanticTokenType.Struct;
            } else if (parentKind == SyntaxKind.VariableDeclaration) {
                tokenType = SemanticTokenType.Variable;
            } else if (parentKind == SyntaxKind.MethodDeclaration) {
                tokenType = SemanticTokenType.Method;
            } else if (parentKind == SyntaxKind.LocalFunctionStatement) {
                tokenType = SemanticTokenType.Function;
            } else if (parentKind is SyntaxKind.IdentifierName or SyntaxKind.TemplateName) {
                var tokenToLookAt = token.parent.parent;

                while (tokenToLookAt.kind is SyntaxKind.QualifiedName or SyntaxKind.AliasQualifiedName)
                    tokenToLookAt = tokenToLookAt.parent;

                if (tokenToLookAt.kind == SyntaxKind.NamespaceDeclaration)
                    tokenType = SemanticTokenType.Namespace;

                if (tokenToLookAt.kind is SyntaxKind.ReferenceType or SyntaxKind.ArrayType or SyntaxKind.NonNullableType)
                    tokenType = SemanticTokenType.Type;

                if (tokenToLookAt is MemberDeclarationSyntax or
                                     BaseMethodDeclarationSyntax or
                                     VariableDeclarationSyntax or
                                     ParameterSyntax) {
                    tokenType = SemanticTokenType.Type;
                }
            } else if (parentKind == SyntaxKind.Parameter) {
                if (token.parent.parent.parent.kind == SyntaxKind.TemplateParameterList)
                    tokenType = SemanticTokenType.TypeParameter;
                else
                    tokenType = SemanticTokenType.Parameter;
            }

            builder.Push(token.location.startLine, token.location.startCharacter, token.span.length, (string)tokenType);
        }
    }

    protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
        ITextDocumentIdentifierParams @params,
        CancellationToken cancellationToken) {
        return Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));
    }

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(
        SemanticTokensCapability capability,
        ClientCapabilities clientCapabilities) {
        return new SemanticTokensRegistrationOptions {
            DocumentSelector = TextDocumentSelector.ForLanguage("belte"),
            Legend = new SemanticTokensLegend {
                TokenTypes = capability.TokenTypes,
                TokenModifiers = capability.TokenModifiers,
            },
            Full = new SemanticTokensCapabilityRequestFull {
                Delta = true
            },
            Range = true
        };
    }
}
