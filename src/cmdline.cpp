#include "cmdline.h"
#include "utils.h"

extern string me;

_NODISCARD vector<string> convert_argv(_In_reads_(argc) char **argv, int argc) noexcept {
    vector<string> args;

    for (int i=0; i<argc; i++) {
        args.push_back(argv[i]);
    }

    return args;
}

int decode_options(_In_ const vector<string>& args, _Out_ CompilerOptions& options) noexcept {
    int error = 0;
    me = args[0];

    if (args.size() == 1) {
        error = RaiseError("No input files.");
    }

    return check_error(error);
}
