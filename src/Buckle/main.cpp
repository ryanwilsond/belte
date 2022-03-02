#include "utils.h"
#include "cmdline.h"
#include "compiler.h"
#include "state.h"
#include "toplev.h"

string me;

int main(int argc, char **argv) {
    int error = SUCCESS_EXIT;
    vector<string> args = convert_argv(argv, argc);

    CompilerState compiler_state;
    decode_options(args, compiler_state);

    if (compiler_state.test) {
        return toplev::main();
    }

    clean_outfiles(compiler_state);

    error = preprocess_code(compiler_state);
    check_error(error);
    check_tasks(compiler_state);

    if (compiler_state.target == Targets::Win64) {
        error = compile_for_win64(compiler_state);
    } else if (compiler_state.target == Targets::NET) {
        error = compile_for_dotnet_core(compiler_state);
    }
    check_error(error);
    check_tasks(compiler_state);

    error = assemble_for_win64(compiler_state);
    check_error(error);
    check_tasks(compiler_state);

    error = link_for_win64(compiler_state);
    check_error(error);
    check_tasks(compiler_state);

    return error;
}
