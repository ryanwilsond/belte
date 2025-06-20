using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using AssemblyNameFlags = System.Reflection.AssemblyNameFlags;
using AssemblyContentType = System.Reflection.AssemblyContentType;
using System.Text;

namespace Buckle.CodeAnalysis;

[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
internal sealed partial class AssemblyIdentity : IEquatable<AssemblyIdentity> {
    private static readonly ConcurrentCache<string, (AssemblyIdentity identity, AssemblyIdentityParts parts)> TryParseDisplayNameCache =
        new ConcurrentCache<string, (AssemblyIdentity identity, AssemblyIdentityParts parts)>(1024, ReferenceEqualityComparer.Instance);

    internal const string InvariantCultureDisplay = "neutral";

    private readonly AssemblyContentType _contentType;
    private readonly string _name;
    private readonly Version _version;
    private readonly string _cultureName;
    private readonly ImmutableArray<byte> _publicKey;
    private ImmutableArray<byte> _lazyPublicKeyToken;
    private readonly bool _isRetargetable;
    private string? _lazyDisplayName;
    private int _lazyHashCode;

    private const int PublicKeyTokenBytes = 8;
    internal const int PublicKeyTokenSize = 8;

    private AssemblyIdentity(AssemblyIdentity other, Version version) {
        _contentType = other.contentType;
        _name = other._name;
        _cultureName = other._cultureName;
        _publicKey = other._publicKey;
        _lazyPublicKeyToken = other._lazyPublicKeyToken;
        _isRetargetable = other._isRetargetable;

        _version = version;
        _lazyDisplayName = null;
        _lazyHashCode = 0;
    }

    internal AssemblyIdentity WithVersion(Version version)
        => (version == _version) ? this : new AssemblyIdentity(this, version);

    internal AssemblyIdentity(
        string? name,
        Version? version = null,
        string? cultureName = null,
        ImmutableArray<byte> publicKeyOrToken = default,
        bool hasPublicKey = false,
        bool isRetargetable = false,
        AssemblyContentType contentType = AssemblyContentType.Default) {
        _name = name;
        _version = version ?? NullVersion;
        _cultureName = NormalizeCultureName(cultureName);
        _isRetargetable = isRetargetable;
        _contentType = contentType;
        InitializeKey(publicKeyOrToken, hasPublicKey, out _publicKey, out _lazyPublicKeyToken);
    }

    internal AssemblyIdentity(
        string name,
        Version version,
        string? cultureName,
        ImmutableArray<byte> publicKeyOrToken,
        bool hasPublicKey,
        bool isRetargetable) {
        _name = name;
        _version = version ?? NullVersion;
        _cultureName = NormalizeCultureName(cultureName);
        _isRetargetable = isRetargetable;
        _contentType = AssemblyContentType.Default;
        InitializeKey(publicKeyOrToken, hasPublicKey, out _publicKey, out _lazyPublicKeyToken);
    }

    internal AssemblyIdentity(
        bool noThrow,
        string name,
        Version? version = null,
        string? cultureName = null,
        ImmutableArray<byte> publicKeyOrToken = default,
        bool hasPublicKey = false,
        bool isRetargetable = false,
        AssemblyContentType contentType = AssemblyContentType.Default) {
        _name = name;
        _version = version ?? NullVersion;
        _cultureName = NormalizeCultureName(cultureName);
        _contentType = IsValid(contentType) ? contentType : AssemblyContentType.Default;
        _isRetargetable = isRetargetable && _contentType != AssemblyContentType.WindowsRuntime;
        InitializeKey(publicKeyOrToken, hasPublicKey, out _publicKey, out _lazyPublicKeyToken);
    }

    private static string NormalizeCultureName(string? cultureName) {
        return cultureName is null ||
            AssemblyIdentityComparer.CultureComparer.Equals(cultureName, InvariantCultureDisplay)
                ? ""
                : cultureName;
    }

    private static void InitializeKey(
        ImmutableArray<byte> publicKeyOrToken,
        bool hasPublicKey,
        out ImmutableArray<byte> publicKey,
        out ImmutableArray<byte> publicKeyToken) {
        if (hasPublicKey) {
            publicKey = publicKeyOrToken;
            publicKeyToken = default;
        } else {
            publicKey = [];
            publicKeyToken = publicKeyOrToken.NullToEmpty();
        }
    }

    internal static bool IsValidCultureName(string? name) {
        return name == null || name.IndexOf('\0') < 0;
    }

    private static bool IsValidName(string name) {
        return !string.IsNullOrEmpty(name) && name.IndexOf('\0') < 0;
    }

    internal static readonly Version NullVersion = new Version(0, 0, 0, 0);

    private static bool IsValid(Version? value) {
        return value == null
            || value.Major >= 0
            && value.Minor >= 0
            && value.Build >= 0
            && value.Revision >= 0
            && value.Major <= ushort.MaxValue
            && value.Minor <= ushort.MaxValue
            && value.Build <= ushort.MaxValue
            && value.Revision <= ushort.MaxValue;
    }

    private static bool IsValid(AssemblyContentType value) {
        return value >= AssemblyContentType.Default && value <= AssemblyContentType.WindowsRuntime;
    }

    internal string name => _name;

    internal Version version => _version;

    internal string cultureName => _cultureName;

    internal AssemblyNameFlags flags
        => (_isRetargetable ? AssemblyNameFlags.Retargetable : AssemblyNameFlags.None) |
           (hasPublicKey ? AssemblyNameFlags.PublicKey : AssemblyNameFlags.None);

    internal AssemblyContentType contentType => _contentType;

    internal bool hasPublicKey => _publicKey.Length > 0;

    internal ImmutableArray<byte> publicKey => _publicKey;

    internal ImmutableArray<byte> publicKeyToken {
        get {
            if (_lazyPublicKeyToken.IsDefault) {
                ImmutableInterlocked.InterlockedCompareExchange(
                    ref _lazyPublicKeyToken,
                    CalculatePublicKeyToken(_publicKey),
                    default
                );
            }

            return _lazyPublicKeyToken;
        }
    }

    internal bool isStrongName => hasPublicKey || _lazyPublicKeyToken.Length > 0;

    internal bool isRetargetable => _isRetargetable;

    internal static bool IsFullName(AssemblyIdentityParts parts) {
        const AssemblyIdentityParts nvc = AssemblyIdentityParts.Name |
                                          AssemblyIdentityParts.Version |
                                          AssemblyIdentityParts.Culture;

        return (parts & nvc) == nvc && (parts & AssemblyIdentityParts.PublicKeyOrToken) != 0;
    }

    public static bool operator ==(AssemblyIdentity left, AssemblyIdentity right) {
        return EqualityComparer<AssemblyIdentity>.Default.Equals(left, right);
    }

    public static bool operator !=(AssemblyIdentity left, AssemblyIdentity right) {
        return !(left == right);
    }

    public bool Equals(AssemblyIdentity obj) {
        return obj is not null
            && (_lazyHashCode == 0 || obj._lazyHashCode == 0 || _lazyHashCode == obj._lazyHashCode)
            && MemberwiseEqual(this, obj) == true;
    }

    public override bool Equals(object? obj) {
        return Equals(obj as AssemblyIdentity);
    }

    public override int GetHashCode() {
        if (_lazyHashCode == 0) {
            _lazyHashCode =
                Hash.Combine(AssemblyIdentityComparer.SimpleNameComparer.GetHashCode(_name),
                Hash.Combine(_version.GetHashCode(), GetHashCodeIgnoringNameAndVersion()));
        }

        return _lazyHashCode;
    }

    internal int GetHashCodeIgnoringNameAndVersion() {
        return
            Hash.Combine((int)_contentType,
            Hash.Combine(_isRetargetable,
            AssemblyIdentityComparer.CultureComparer.GetHashCode(_cultureName)));
    }

    internal static ImmutableArray<byte> CalculatePublicKeyToken(ImmutableArray<byte> publicKey) {
        var hash = CryptographicHashProvider.ComputeSha1(publicKey);

        int l = hash.Length - 1;
        var result = ArrayBuilder<byte>.GetInstance(PublicKeyTokenSize);

        for (var i = 0; i < PublicKeyTokenSize; i++)
            result.Add(hash[l - i]);

        return result.ToImmutableAndFree();
    }

    internal static bool? MemberwiseEqual(AssemblyIdentity x, AssemblyIdentity y) {
        if (ReferenceEquals(x, y))
            return true;

        if (!AssemblyIdentityComparer.SimpleNameComparer.Equals(x._name, y._name))
            return false;

        if (x._version.Equals(y._version) && EqualIgnoringNameAndVersion(x, y))
            return true;

        return null;
    }

    internal static bool EqualIgnoringNameAndVersion(AssemblyIdentity x, AssemblyIdentity y) {
        return
            x.isRetargetable == y.isRetargetable &&
            x.contentType == y.contentType &&
            AssemblyIdentityComparer.CultureComparer.Equals(x.cultureName, y.cultureName) &&
            KeysEqual(x, y);
    }

    internal static bool KeysEqual(AssemblyIdentity x, AssemblyIdentity y) {
        var xToken = x._lazyPublicKeyToken;
        var yToken = y._lazyPublicKeyToken;

        if (!xToken.IsDefault && !yToken.IsDefault)
            return xToken.SequenceEqual(yToken);

        if (xToken.IsDefault && yToken.IsDefault)
            return x._publicKey.SequenceEqual(y._publicKey);

        if (xToken.IsDefault)
            return x.publicKeyToken.SequenceEqual(yToken);
        else
            return xToken.SequenceEqual(y.publicKeyToken);
    }

    public static AssemblyIdentity FromAssemblyDefinition(System.Reflection.Assembly assembly) {
        if (assembly == null) {
            throw new ArgumentNullException(nameof(assembly));
        }

        return FromAssemblyDefinition(assembly.GetName());
    }

    internal static AssemblyIdentity FromAssemblyDefinition(System.Reflection.AssemblyName name) {
        var publicKeyBytes = name.GetPublicKey();
        var publicKey = (publicKeyBytes is not null) ? ImmutableArray.Create(publicKeyBytes) : [];

        return new AssemblyIdentity(
            name.Name,
            name.Version,
            name.CultureName,
            publicKey,
            hasPublicKey: publicKey.Length > 0,
            isRetargetable: (name.Flags & AssemblyNameFlags.Retargetable) != 0,
            contentType: name.ContentType
        );
    }

    internal static AssemblyIdentity FromAssemblyReference(System.Reflection.AssemblyName name) {
        return new AssemblyIdentity(
            name.Name,
            name.Version,
            name.CultureName,
            ImmutableArray.Create(name.GetPublicKeyToken()),
            hasPublicKey: false,
            isRetargetable: (name.Flags & AssemblyNameFlags.Retargetable) != 0,
            contentType: name.ContentType
        );
    }

    internal static bool TryParseDisplayName(string displayName, out AssemblyIdentity identity) {
        return displayName is null
            ? throw new ArgumentNullException(nameof(displayName))
            : TryParseDisplayName(displayName, out identity, parts: out _);
    }

    internal static bool TryParseDisplayName(
        string displayName,
        out AssemblyIdentity identity,
        out AssemblyIdentityParts parts) {
        if (!TryParseDisplayNameCache.TryGetValue(displayName, out var identityAndParts)) {
            if (TryParseDisplayName(displayName, out var localIdentity, out var localParts)) {
                identityAndParts = (localIdentity, localParts);
                TryParseDisplayNameCache.TryAdd(displayName, identityAndParts);
            }
        }

        identity = identityAndParts.identity;
        parts = identityAndParts.parts;
        return identity != null;

        static bool TryParseDisplayName(
            string displayName,
            out AssemblyIdentity identity,
            out AssemblyIdentityParts parts) {
            identity = null;
            parts = 0;

            if (displayName is null)
                throw new ArgumentNullException(nameof(displayName));

            if (displayName.IndexOf('\0') >= 0)
                return false;

            var position = 0;

            if (!TryParseNameToken(displayName, ref position, out var simpleName))
                return false;

            var parsedParts = AssemblyIdentityParts.Name;
            var seen = AssemblyIdentityParts.Name;

            Version version = null;
            string culture = null;
            var isRetargetable = false;
            var contentType = AssemblyContentType.Default;
            var publicKey = default(ImmutableArray<byte>);
            var publicKeyToken = default(ImmutableArray<byte>);

            while (position < displayName.Length) {
                if (displayName[position] != ',')
                    return false;

                position++;

                if (!TryParseNameToken(displayName, ref position, out var propertyName))
                    return false;

                if (position >= displayName.Length || displayName[position] != '=')
                    return false;

                position++;

                if (!TryParseNameToken(displayName, ref position, out var propertyValue))
                    return false;

                if (string.Equals(propertyName, "Version", StringComparison.OrdinalIgnoreCase)) {
                    if ((seen & AssemblyIdentityParts.Version) != 0)
                        return false;

                    seen |= AssemblyIdentityParts.Version;

                    if (propertyValue == "*")
                        continue;

                    if (!TryParseVersion(propertyValue, out var versionLong, out var versionParts))
                        return false;

                    version = ToVersion(versionLong);
                    parsedParts |= versionParts;
                } else if (string.Equals(propertyName, "Culture", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(propertyName, "Language", StringComparison.OrdinalIgnoreCase)) {
                    if ((seen & AssemblyIdentityParts.Culture) != 0)
                        return false;

                    seen |= AssemblyIdentityParts.Culture;

                    if (propertyValue == "*")
                        continue;

                    culture = string.Equals(propertyValue, InvariantCultureDisplay, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : propertyValue;

                    parsedParts |= AssemblyIdentityParts.Culture;
                } else if (string.Equals(propertyName, "PublicKey", StringComparison.OrdinalIgnoreCase)) {
                    if ((seen & AssemblyIdentityParts.PublicKey) != 0)
                        return false;

                    seen |= AssemblyIdentityParts.PublicKey;

                    if (propertyValue == "*")
                        continue;

                    if (!TryParsePublicKey(propertyValue, out var value))
                        return false;

                    publicKey = value;
                    parsedParts |= AssemblyIdentityParts.PublicKey;
                } else if (string.Equals(propertyName, "PublicKeyToken", StringComparison.OrdinalIgnoreCase)) {
                    if ((seen & AssemblyIdentityParts.PublicKeyToken) != 0)
                        return false;

                    seen |= AssemblyIdentityParts.PublicKeyToken;

                    if (propertyValue == "*")
                        continue;

                    if (!TryParsePublicKeyToken(propertyValue, out var value))
                        return false;

                    publicKeyToken = value;
                    parsedParts |= AssemblyIdentityParts.PublicKeyToken;
                } else if (string.Equals(propertyName, "Retargetable", StringComparison.OrdinalIgnoreCase)) {
                    if ((seen & AssemblyIdentityParts.Retargetability) != 0)
                        return false;

                    seen |= AssemblyIdentityParts.Retargetability;

                    if (propertyValue == "*")
                        continue;

                    if (string.Equals(propertyValue, "Yes", StringComparison.OrdinalIgnoreCase))
                        isRetargetable = true;
                    else if (string.Equals(propertyValue, "No", StringComparison.OrdinalIgnoreCase))
                        isRetargetable = false;
                    else
                        return false;

                    parsedParts |= AssemblyIdentityParts.Retargetability;
                } else if (string.Equals(propertyName, "ContentType", StringComparison.OrdinalIgnoreCase)) {
                    if ((seen & AssemblyIdentityParts.ContentType) != 0)
                        return false;

                    seen |= AssemblyIdentityParts.ContentType;

                    if (propertyValue == "*")
                        continue;

                    if (string.Equals(propertyValue, "WindowsRuntime", StringComparison.OrdinalIgnoreCase))
                        contentType = AssemblyContentType.WindowsRuntime;
                    else
                        return false;

                    parsedParts |= AssemblyIdentityParts.ContentType;
                } else {
                    parsedParts |= AssemblyIdentityParts.Unknown;
                }
            }

            if (isRetargetable && contentType == AssemblyContentType.WindowsRuntime)
                return false;

            var hasPublicKey = !publicKey.IsDefault;
            var hasPublicKeyToken = !publicKeyToken.IsDefault;

            identity = new AssemblyIdentity(
                simpleName,
                version,
                culture,
                hasPublicKey ? publicKey : publicKeyToken,
                hasPublicKey,
                isRetargetable,
                contentType
            );

            if (hasPublicKey && hasPublicKeyToken && !identity.publicKeyToken.SequenceEqual(publicKeyToken)) {
                identity = null;
                return false;
            }

            parts = parsedParts;
            return true;
        }
    }

    private static bool TryParsePublicKey(string value, out ImmutableArray<byte> key) {
        if (!TryParseHexBytes(value, out key) ||
            !MetadataHelpers.IsValidPublicKey(key)) {
            key = default;
            return false;
        }

        return true;
    }

    private static bool TryParseHexBytes(string value, out ImmutableArray<byte> result) {
        if (value.Length == 0 || (value.Length % 2) != 0) {
            result = default;
            return false;
        }

        var length = value.Length / 2;
        var bytes = ArrayBuilder<byte>.GetInstance(length);

        for (var i = 0; i < length; i++) {
            var hi = HexValue(value[i * 2]);
            var lo = HexValue(value[i * 2 + 1]);

            if (hi < 0 || lo < 0) {
                result = default;
                bytes.Free();
                return false;
            }

            bytes.Add((byte)((hi << 4) | lo));
        }

        result = bytes.ToImmutableAndFree();
        return true;
    }

    internal static int HexValue(char c) {
        if (c >= '0' && c <= '9')
            return c - '0';

        if (c >= 'a' && c <= 'f')
            return c - 'a' + 10;

        if (c >= 'A' && c <= 'F')
            return c - 'A' + 10;

        return -1;
    }

    private static bool TryParseNameToken(string displayName, ref int position, out string value) {
        var i = position;

        while (true) {
            if (i == displayName.Length) {
                value = null;
                return false;
            } else if (!IsWhiteSpace(displayName[i])) {
                break;
            }

            i++;
        }

        char quote;

        if (IsQuote(displayName[i]))
            quote = displayName[i++];
        else
            quote = '\0';

        var valueStart = i;
        var valueEnd = displayName.Length;
        var containsEscapes = false;

        while (true) {
            if (i >= displayName.Length) {
                i = displayName.Length;
                break;
            }

            var c = displayName[i];

            if (c == '\\') {
                containsEscapes = true;
                i += 2;
                continue;
            }

            if (quote == '\0') {
                if (IsNameTokenTerminator(c)) {
                    break;
                } else if (IsQuote(c)) {
                    value = null;
                    return false;
                }
            } else if (c == quote) {
                valueEnd = i;
                i++;
                break;
            }

            i++;
        }

        if (quote == '\0') {
            var j = i - 1;

            while (j >= valueStart && IsWhiteSpace(displayName[j]))
                j--;

            valueEnd = j + 1;
        } else {
            while (i < displayName.Length) {
                var c = displayName[i];

                if (!IsWhiteSpace(c)) {
                    if (!IsNameTokenTerminator(c)) {
                        value = null;
                        return false;
                    }

                    break;
                }

                i++;
            }
        }

        position = i;

        if (valueEnd == valueStart) {
            value = null;
            return false;
        }

        if (!containsEscapes) {
            value = displayName.Substring(valueStart, valueEnd - valueStart);
            return true;
        } else {
            return TryUnescape(displayName, valueStart, valueEnd, out value);
        }
    }

    private static bool IsWhiteSpace(char c) {
        return c == ' ' || c == '\t' || c == '\r' || c == '\n';
    }

    private static void EscapeName(StringBuilder result, string? name) {
        if (string.IsNullOrEmpty(name))
            return;

        var quoted = false;

        if (IsWhiteSpace(name[0]) || IsWhiteSpace(name[name.Length - 1])) {
            result.Append('"');
            quoted = true;
        }

        for (var i = 0; i < name.Length; i++) {
            var c = name[i];

            switch (c) {
                case ',':
                case '=':
                case '\\':
                case '"':
                case '\'':
                    result.Append('\\');
                    result.Append(c);
                    break;
                case '\t':
                    result.Append("\\t");
                    break;
                case '\r':
                    result.Append("\\r");
                    break;
                case '\n':
                    result.Append("\\n");
                    break;
                default:
                    result.Append(c);
                    break;
            }
        }

        if (quoted)
            result.Append('"');
    }

    private static bool TryUnescape(string str, int start, int end, out string value) {
        var sb = PooledStringBuilder.GetInstance();

        var i = start;

        while (i < end) {
            var c = str[i++];

            if (c == '\\') {
                if (!Unescape(sb.Builder, str, ref i)) {
                    value = null;
                    return false;
                }
            } else {
                sb.Builder.Append(c);
            }
        }

        value = sb.ToStringAndFree();
        return true;
    }

    private static bool Unescape(StringBuilder sb, string str, ref int i) {
        if (i == str.Length)
            return false;

        var c = str[i++];

        switch (c) {
            case ',':
            case '=':
            case '\\':
            case '/':
            case '"':
            case '\'':
                sb.Append(c);
                return true;
            case 't':
                sb.Append('\t');
                return true;
            case 'n':
                sb.Append('\n');
                return true;
            case 'r':
                sb.Append('\r');
                return true;
            case 'u':
                var semicolon = str.IndexOf(';', i);

                if (semicolon == -1)
                    return false;

                try {
                    var codepoint = Convert.ToInt32(str.Substring(i, semicolon - i), 16);

                    if (codepoint == 0)
                        return false;

                    sb.Append(char.ConvertFromUtf32(codepoint));
                } catch {
                    return false;
                }

                i = semicolon + 1;
                return true;
            default:
                return false;
        }
    }

    internal static bool TryParseVersion(string str, out ulong result, out AssemblyIdentityParts parts) {
        const int MaxVersionParts = 4;
        const int BitsPerVersionPart = 16;

        parts = 0;
        result = 0;
        var partOffset = BitsPerVersionPart * (MaxVersionParts - 1);
        var partIndex = 0;
        var partValue = 0;
        var partHasValue = false;
        var partHasWildcard = false;

        var i = 0;

        while (true) {
            var c = (i < str.Length) ? str[i++] : '\0';

            if (c == '.' || c == 0) {
                if (partIndex == MaxVersionParts || partHasValue && partHasWildcard)
                    return false;

                result |= ((ulong)partValue) << partOffset;

                if (partHasValue || partHasWildcard)
                    parts |= (AssemblyIdentityParts)((int)AssemblyIdentityParts.VersionMajor << partIndex);

                if (c == 0)
                    return true;

                partValue = 0;
                partOffset -= BitsPerVersionPart;
                partIndex++;
                partHasWildcard = partHasValue = false;
            } else if (c >= '0' && c <= '9') {
                partHasValue = true;
                partValue = partValue * 10 + c - '0';

                if (partValue > ushort.MaxValue)
                    return false;
            } else if (c == '*') {
                partHasWildcard = true;
            } else {
                return false;
            }
        }
    }

    internal static Version ToVersion(ulong version) {
        return new Version(
            unchecked((ushort)(version >> 48)),
            unchecked((ushort)(version >> 32)),
            unchecked((ushort)(version >> 16)),
            unchecked((ushort)version)
        );
    }

    internal IVTConclusion PerformIVTCheck(
        ImmutableArray<byte> assemblyWantingAccessKey,
        ImmutableArray<byte> grantedToPublicKey) {
        var q1 = isStrongName;
        var q2 = !grantedToPublicKey.IsDefaultOrEmpty;
        var q3 = !assemblyWantingAccessKey.IsDefaultOrEmpty;
        var q4 = (q2 & q3) && ByteSequenceComparer.Equals(grantedToPublicKey, assemblyWantingAccessKey);

        if (q2 && !q4)
            return IVTConclusion.PublicKeyDoesntMatch;

        if (!q1 && q3)
            return IVTConclusion.OneSignedOneNot;

        return IVTConclusion.Match;
    }

    private static bool TryParsePublicKeyToken(string value, out ImmutableArray<byte> token) {
        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "neutral", StringComparison.OrdinalIgnoreCase)) {
            token = [];
            return true;
        }

        if (value.Length != (PublicKeyTokenBytes * 2) || !TryParseHexBytes(value, out var result)) {
            token = default;
            return false;
        }

        token = result;
        return true;
    }

    private static bool IsQuote(char c) {
        return c == '"' || c == '\'';
    }

    private static bool IsNameTokenTerminator(char c) {
        return c == '=' || c == ',';
    }
}
