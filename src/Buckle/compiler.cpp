#include "compiler.h"

int compile_for_win64(_Inout_ CompilerState& state) noexcept {
    int error = SUCCESS_EXIT;

    for (size_t i=0; i<state.tasks.size(); i++) {
        FileTask& task = state.tasks[i];

        if (task.state.GetStep() == Steps::Preprocess && task.out_step != Steps::Preprocess) {
            task.state.SetStep(Steps::Compile);
            error = Buckle::Compile(task.state.GetCodeS(), state);
            if (error != FAILURE_EXIT) break;
        }
    }

    return error;
}

int compile_for_dotnet_core(_Inout_ CompilerState& state) noexcept {
    int error = SUCCESS_EXIT;

    for (size_t i=0; i<state.tasks.size(); i++) {
        FileTask& task = state.tasks[i];

        if (task.state.GetStep() == Steps::Preprocess && task.out_step != Steps::Preprocess) {
            task.state.SetStep(Steps::Compile);
            error = Buckle::CompileNET(task.state.GetCodeS(), state);
            if (error != FAILURE_EXIT) break;
        }
    }

    return error;
}

int preprocess_code(_Inout_ CompilerState& state) noexcept {
    int error = SUCCESS_EXIT;

    for (size_t i=0; i<state.tasks.size(); i++) {
        FileTask& task = state.tasks[i];

        if (task.state.GetStep() == Steps::Raw) {
            task.state.SetStep(Steps::Preprocess);
            error = Buckle::Preprocess(task.state.GetCodeS(), state);
            if (error != FAILURE_EXIT) break;
        }
    }

    return error;
}

int assemble_for_win64(_Inout_ CompilerState& state) noexcept {
    int error = SUCCESS_EXIT;

    for (size_t i=0; i<state.tasks.size(); i++) {
        FileTask& task = state.tasks[i];

        if (task.state.GetStep() == Steps::Compile && \
            task.out_step != Steps::Preprocess && task.out_step != Steps::Assemble) {
            task.state.SetStep(Steps::Preprocess);
            error = Buckle::Assemble(task.state.GetCodeS(), task.state.GetCodeO(), state);
            if (error != FAILURE_EXIT) break;
        }
    }

    return error;
}

int link_for_win64(_Inout_ CompilerState& state) noexcept {
    int error = SUCCESS_EXIT;

    vector<vector<unsigned char>> objects;

    for (size_t i=0; i<state.tasks.size(); i++) {
        FileTask& task = state.tasks[i];

        if (task.state.GetStep() == Steps::Assemble && task.out_step == Steps::Link) {
            objects.push_back(task.state.GetCodeO());
        }
    }

    if (state.tasks.size() > 0) {
        vector<unsigned char> exe;
        error = Buckle::Link(objects, exe, state);
        string out = state.tasks[0].out;
        state.tasks.clear();
        FileTask final_task = FileTask();
        final_task.state.SetSrc(Steps::Link, exe);
        final_task.out_step = Steps::Link;
        final_task.out = out;
        state.tasks.push_back(final_task);
    }

    return error;
}
