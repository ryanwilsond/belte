#include "utils.h"
#include "cmdline.h"
#include "compiler.h"

string me;

int main(int argc, char **argv) {
    vector<string> args = convert_argv(argv, argc);

    CompilerOptions options;
    decode_options(args, options);

    string processed = preprocess_code(options);

    if (options.step == Steps::Preprocess) {
        write_text(options.out, processed);
        return 0;
    }

    if (options.target == Targets::Win64) {
        return compile_for_win64(processed, options);
    } else if (options.target == Targets::NET) {
        return compile_for_dotnet_core(processed, options);
    }

    return 0;
}
