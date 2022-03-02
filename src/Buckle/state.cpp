#include "state.h"

void check_tasks(_In_ CompilerState& state) noexcept {
    for (size_t i=0; i<state.tasks.size(); i++) {
        FileTask& task = state.tasks[i];

        if (task.state.GetStep() == task.out_step) {
            switch (task.out_step) {
                case Steps::Compile:
                case Steps::Preprocess:
                    write_text(task.out, task.state.GetCodeS());
                    break;
                case Steps::Link:
                case Steps::Assemble:
                    write_bytes(task.out, task.state.GetCodeO());
                    break;
                default:
                    break;
            }
        }
    }
}
