/* Entry point for Buckle */

#include "utils.cpp"
#include "compiler.hpp"
#include "asm.hpp"
#include "preproc.hpp"
#include "link.hpp"
#include "cmdline.hpp"
#include "opts.hpp"

CompilerState state;
string me;

/// entry point, takes in command-line arguments
/// controls flow of compiler and calls modules
/// @param argc argument count
/// @param argv arguments
/// @return error
int main(int argc, _In_ char **argv) noexcept {
    vector<string> args = convert_argv(argc, argv);
    extract_name_and_expand(args);

    decode_options(args);
    check_errors();

    produce_output_filenames();
    clean_output_files();

    Preprocessor::preprocess();
    check_errors();

    if (state.finish_stage == CompilerStage::preprocessed) {
        clean_up_normal_exit();
        return SUCCESS_EXIT_CODE;
    }

    Compiler::compile();
    check_errors();

    if (state.finish_stage == CompilerStage::compiled) {
        clean_up_normal_exit();
        return SUCCESS_EXIT_CODE;
    }

    Assembler::assemble();
    check_errors();

    if (state.finish_stage == CompilerStage::assembled) {
        clean_up_normal_exit();
        return SUCCESS_EXIT_CODE;
    }

    Linker::link();
    check_errors();

    if (state.finish_stage == CompilerStage::linked) {
        clean_up_normal_exit();
        return SUCCESS_EXIT_CODE;
    }

    return FATAL_EXIT_CODE;
}
