#include "opts.hpp"

void decode_options(_In_ const vector<string>& args) noexcept {
    bool specify_stage = false;
    bool specify_out = false;

    // defaults
    state.finish_stage = CompilerStage::linked;
    state.link_output = "a.exe";

    // arg parsing
    for (size_t i=0; i<args.size(); i++) {
        string arg = args[i];

        if (startswith(arg, "-")) {
            if (arg == "-E") {
                specify_stage = true;
                state.finish_stage = CompilerStage::preprocessed;
            } else if (arg == "-S") {
                specify_stage = true;
                state.finish_stage = CompilerStage::compiled;
            } else if (arg == "-c") {
                specify_stage = true;
                state.finish_stage = CompilerStage::assembled;
            } else if (arg == "-r") {
                error = SUCCESS_EXIT_CODE;
                return;
            } else if (arg == "-o") {
                specify_out = true;
                if (i >= args.size()-1) {
                    RaiseFatalError("Missing output filename (with '-o').");
                }

                state.link_output = args[i++];
            } else {
                RaiseFatalError(format("Unknown argument '%s'.", arg));
            }
        } else {
            string filename = arg;
            vector<string> parts = split_str(filename, ".");
            string type = parts[parts.size()-1];
            FileState task;
            task.in_filename = filename;

            if (type == "ble") {
                task.stage = CompilerStage::raw;
            } else if (type == "pble") {
                task.stage = CompilerStage::preprocessed;
            } else if (type == "s" || type == "asm") {
                task.stage = CompilerStage::compiled;
            } else if (type == "o" || type == "obj") {
                task.stage = CompilerStage::assembled;
            } else {
                RaiseWarning(format("Unknown file extension '%s'. Ignoring input file '%s'.", type, filename));
            }

            state.tasks.push_back(task);
        }
    }

    // final error checking
    if (specify_out && specify_stage) RaiseFatalError("Cannot specify output file with '-E', '-S', or '-c'.");
    if (state.tasks.size() == 0) RaiseFatalError("No input files.");

}

void produce_output_filenames() noexcept {
    if (state.finish_stage == CompilerStage::linked) return;

    for (FileState file : state.tasks) {
        string inter = split_str(file.in_filename, ".")[0];

        switch (state.finish_stage) {
            case CompilerStage::preprocessed:
                inter += ".pble";
                break;
            case CompilerStage::compiled:
                inter += ".s";
                break;
            case CompilerStage::assembled:
                inter += ".o";
                break;
            default: break;
        }
    }
}

void clean_output_files() noexcept {
    if (state.finish_stage == CompilerStage::linked) {
        delete_file(state.link_output);
        return;
    }

    for (FileState file : state.tasks) {
        delete_file(file.out_filename);
    }
}
