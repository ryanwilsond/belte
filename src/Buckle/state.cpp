#include "state.hpp"

extern CompilerState state;

void clean_up_early_exit() noexcept { }

void clean_up_normal_exit() noexcept {

    if (state.finish_stage == CompilerStage::linked) {
        write_bytes(state.link_output, state.link_content);
        return;
    }

    for (FileState file : state.tasks) {
        if (file.stage == state.finish_stage) {
            if (file.stage == CompilerStage::assembled) {
                write_bytes(file.out_filename, file.FileContent.object);
            } else {
                write_lines(file.out_filename, file.FileContent.lines);
            }
        }
    }
}
