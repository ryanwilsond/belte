// Handles compiler state
#ifndef STATE_H
#define STATE_H

#include "utils.h"

enum Steps {
    Link,
    Assemble,
    Compile,
    Preprocess,
    Raw,
};

enum Targets {
    Win64,
    NET
};

class FileState {
private:
    int step_;
    string code_s_;
    vector<unsigned char> code_o_;

public:
    FileState() {}

    void SetSrc(int step, string code) {
        this->step_ = step;
        this->code_s_ = code;
    }

    void SetSrc(int step, vector<unsigned char> code) {
        this->step_ = step;
        this->code_o_ = code;
    }

    int GetStep() const {
        return this->step_;
    }

    void SetStep(int step) {
        this->step_ = step;
    }

    string& GetCodeS() {
        return this->code_s_;
    }

    vector<unsigned char>& GetCodeO() {
        return this->code_o_;
    }

};

struct FileTask {
    string in;
    string out;
    int in_step;
    int out_step;
    FileState state;
};

struct CompilerState {
    int optimize;
    int target;
    int step;
    bool test;
    vector<FileTask> tasks;
};

/// Checks files and check if they are done and can be written
/// @param state    compiler state
void check_tasks(_In_ CompilerState& state) noexcept;

#endif
