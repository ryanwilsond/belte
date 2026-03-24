using System;
using System.Collections.Generic;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class QuickAttributeChecker {
    private readonly Dictionary<string, QuickAttributes> _nameToAttributeMap;
    private static QuickAttributeChecker _lazyPredefinedQuickAttributeChecker;

    internal static QuickAttributeChecker Predefined {
        get {
            if (_lazyPredefinedQuickAttributeChecker is null) {
                Interlocked.CompareExchange(
                    ref _lazyPredefinedQuickAttributeChecker,
                    CreatePredefinedQuickAttributeChecker(),
                    null
                );
            }

            return _lazyPredefinedQuickAttributeChecker;
        }
    }

    private static QuickAttributeChecker CreatePredefinedQuickAttributeChecker() {
        var result = new QuickAttributeChecker();
        // result.AddName(AttributeDescription.TypeIdentifierAttribute.name, QuickAttributes.TypeIdentifier);
        // result.AddName(AttributeDescription.TypeForwardedToAttribute.name, QuickAttributes.TypeForwardedTo);
        // result.AddName(AttributeDescription.AssemblyKeyNameAttribute.name, QuickAttributes.AssemblyKeyName);
        // result.AddName(AttributeDescription.AssemblyKeyFileAttribute.name, QuickAttributes.AssemblyKeyFile);
        // result.AddName(AttributeDescription.AssemblySignatureKeyAttribute.name, QuickAttributes.AssemblySignatureKey);
        return result;
    }

    private QuickAttributeChecker() {
        _nameToAttributeMap = new Dictionary<string, QuickAttributes>(StringComparer.Ordinal);
    }

    private QuickAttributeChecker(QuickAttributeChecker previous) {
        _nameToAttributeMap = new Dictionary<string, QuickAttributes>(
            previous._nameToAttributeMap,
            StringComparer.Ordinal
        );
    }

    private void AddName(string name, QuickAttributes newAttributes) {
        var currentValue = QuickAttributes.None;
        _nameToAttributeMap.TryGetValue(name, out currentValue);

        var newValue = newAttributes | currentValue;
        _nameToAttributeMap[name] = newValue;
    }

    internal QuickAttributeChecker AddAliasesIfAny(
        SyntaxList<UsingDirectiveSyntax> usingsSyntax,
        bool onlyGlobalAliases = false) {
        if (usingsSyntax.Count == 0)
            return this;

        QuickAttributeChecker newChecker = null;

        foreach (var usingDirective in usingsSyntax) {
            if (usingDirective.alias is not null &&
                usingDirective.name != null &&
                (!onlyGlobalAliases || usingDirective.globalKeyword is not null)) {
                var name = usingDirective.alias.name.identifier.text;
                var target = usingDirective.name.GetUnqualifiedName().identifier.text;

                if (_nameToAttributeMap.TryGetValue(target, out var foundAttributes))
                    (newChecker ??= new QuickAttributeChecker(this)).AddName(name, foundAttributes);
            }
        }

        if (newChecker is not null)
            return newChecker;

        return this;
    }

    public bool IsPossibleMatch(AttributeSyntax attr, QuickAttributes pattern) {
        // string name = attr.identifier.GetUnqualifiedName().Identifier.ValueText;
        var name = attr.name.GetUnqualifiedName().identifier.text;

        if (_nameToAttributeMap.TryGetValue(name, out var foundAttributes) ||
            _nameToAttributeMap.TryGetValue(name + "Attribute", out foundAttributes)) {
            return (foundAttributes & pattern) != 0;
        }

        return false;
    }
}

internal static class QuickAttributeHelpers {
    /// <summary>
    /// Returns the <see cref="QuickAttributes"/> that corresponds to the particular type
    /// <paramref name="name"/> passed in.  If <paramref name="inAttribute"/> is <see langword="true"/>
    /// then the name will be checked both as-is as well as with the 'Attribute' suffix.
    /// </summary>
    public static QuickAttributes GetQuickAttributes(string name, bool inAttribute) {

        var result = QuickAttributes.None;
        // if (matches(AttributeDescription.TypeIdentifierAttribute)) {
        //     result |= QuickAttributes.TypeIdentifier;
        // } else if (matches(AttributeDescription.TypeForwardedToAttribute)) {
        //     result |= QuickAttributes.TypeForwardedTo;
        // } else if (matches(AttributeDescription.AssemblyKeyNameAttribute)) {
        //     result |= QuickAttributes.AssemblyKeyName;
        // } else if (matches(AttributeDescription.AssemblyKeyFileAttribute)) {
        //     result |= QuickAttributes.AssemblyKeyFile;
        // } else if (matches(AttributeDescription.AssemblySignatureKeyAttribute)) {
        //     result |= QuickAttributes.AssemblySignatureKey;
        // }

        return result;

        // bool Matches(AttributeDescription attributeDescription) {
        //     if (name == attributeDescription.name)
        //         return true;

        //     if (inAttribute &&
        //         (name.Length + nameof(Attribute).Length) == attributeDescription.name.Length &&
        //         attributeDescription.name.StartsWith(name)) {
        //         return true;
        //     }

        //     return false;
        // }
    }
}
