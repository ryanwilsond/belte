#include "compiler.h"

int compile_for_win64(_In_ const string& code, _In_ CompilerOptions& options) noexcept {
    string assembly = Buckle::Compile(code, options);

    if (options.step == Steps::Compile) {
        write_text(options.out, assembly);
    }

    vector<unsigned char> binary = Buckle::Assemble(assembly, options);

    if (options.step == Steps::Assemble) {
        write_bytes(options.out, binary);
    }

    vector<vector<unsigned char>> bins = {binary};

    vector<unsigned char> executable = Buckle::Link(bins, options);

    if (options.step == Steps::Link) {
        write_bytes(options.out, executable);
    }

    return 0;
}

int compile_for_dotnet_core(_In_ const string& code, _In_ const CompilerOptions& options) noexcept {
    string idl = Buckle::CompileNET(code, options);

    // not implementing idl compiler
    write_text(options.out, idl);

    return 0;
}

_NODISCARD string preprocess_code(_In_ const CompilerOptions& options) noexcept {
    vector<string> codes;

    for (string file : options.in) {
        codes.push_back(read_text(file));
    }

    string processed = Buckle::Preprocess(codes, options);

    return processed;
}
