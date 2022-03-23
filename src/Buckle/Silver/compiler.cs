using System;
using System.Collections.Generic;

namespace Buckle {

    public enum CompilerStage {
        raw,
        preprocessed,
        compiled,
        assembled,
        linked,
    }

    public struct FileContent {
        List<string> lines;
        List<byte> bytes;
    }

    public struct FileState {
        public string in_filename;
        public CompilerStage stage;
        public string out_filename;
        public FileContent file_content;
    }

    public struct CompilerState {
        public CompilerStage finish_stage;
        public string link_output_filename;
        public List<byte> link_output_content;
        public List<FileState> tasks;
    }

    public enum DiagnosticType {
        error,
        warning,
        fatal,
        unknown,
    }

    public class Diagnostic {
        public DiagnosticType type { get; }
        public string msg { get; }

        public Diagnostic(DiagnosticType type_, string msg_) {
            type = type_;
            msg = msg_;
        }
    }

    public class Compiler {
        const int SUCCESS_EXIT_CODE = 0;
        const int ERROR_EXIT_CODE = 1;
        const int FATAL_EXIT_CODE = 2;

        public CompilerState state;
        public string me;
        public List<Diagnostic> diagnostics;

        public Compiler() { }

        public int Compile() {
            // produce_output_filenames();
            // clean_output_files();

            // Preprocessor.preprocess();
            // check_errors();

            // if (state.finish_stage == CompilerStage.preprocessed) {
            //     clean_up_normal_exit();
            //     return SUCCESS_EXIT_CODE;
            // }

            // Compiler.compile();
            // check_errors();

            // if (state.finish_stage == CompilerStage.compiled) {
            //     clean_up_normal_exit();
            //     return SUCCESS_EXIT_CODE;
            // }

            // Assembler.assemble();
            // check_errors();

            // if (state.finish_stage == CompilerStage.assembled) {
            //     clean_up_normal_exit();
            //     return SUCCESS_EXIT_CODE;
            // }

            // Linker.link();
            // check_errors();

            // if (state.finish_stage == CompilerStage.linked) {
            //     clean_up_normal_exit();
            //     return SUCCESS_EXIT_CODE;
            // }

            return FATAL_EXIT_CODE;
        }
    }
}
