using System;

namespace Buckle.CodeAnalysis;

[Flags]
internal enum AssemblyIdentityParts : short {
    Name = 1,
    Version = VersionMajor | VersionMinor | VersionBuild | VersionRevision,

    VersionMajor = 1 << 1,
    VersionMinor = 1 << 2,
    VersionBuild = 1 << 3,
    VersionRevision = 1 << 4,

    Culture = 1 << 5,
    PublicKey = 1 << 6,
    PublicKeyToken = 1 << 7,
    PublicKeyOrToken = PublicKey | PublicKeyToken,
    Retargetability = 1 << 8,
    ContentType = 1 << 9,

    Unknown = 1 << 10
}
