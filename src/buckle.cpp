#include "buckle.h"

_NODISCARD string Buckle::Preprocess(_In_ const vector<string>& code, _In_ const CompilerOptions& options) noexcept {
    string processed;

    return processed;
}

_NODISCARD string Buckle::Compile(_In_ const string& code, _In_ const CompilerOptions& options) noexcept {
    string assembly;

    return assembly;
}

_NODISCARD string Buckle::CompileNET(_In_ const string& code, _In_ const CompilerOptions& options) noexcept {
    string idl;

    return idl;
}

_NODISCARD vector<unsigned char> Buckle::Assemble(_In_ const string& code, _In_ const CompilerOptions& options) noexcept {
    vector<unsigned char> binary;

    return binary;
}

_NODISCARD vector<unsigned char> Buckle::Link(_In_ const vector<vector<unsigned char>>& binary, _In_ const CompilerOptions& options) noexcept {
    vector<unsigned char> executable;

    return executable;
}
