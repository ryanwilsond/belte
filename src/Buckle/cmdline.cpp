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

int decode_options(_In_ const vector<string>& args, _Out_ CompilerState& state) noexcept {
        state.test = true;
        return SUCCESS_EXIT;

    int error = SUCCESS_EXIT;
    me = args[0];

    if (args.size() == 1) {
        error = RaiseError("No input files.");
    }

    return error;
}

void clean_outfiles(_In_ CompilerState& state) noexcept {
    for (size_t i=0; i<state.tasks.size(); i++) {
        if (file_exists(state.tasks[i].out)) {
            delete_file(state.tasks[i].out);
        }
    }
}
