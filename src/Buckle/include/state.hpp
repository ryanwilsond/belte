/* Compiler state and clean up */
#pragma once
#ifndef STATE_HPP
#define STATE_HPP

#include <string>
#include <vector>

#include <rutils.h>

using std::string;
using std::vector;
using namespace rutils;

enum CompilerStage {
    raw,
    preprocessed,
    compiled,
    assembled,
    linked,
};

struct FileState {
    string in_filename;
    CompilerStage stage;
    string out_filename = "";

    struct {
        vector<string> lines;
        vector<unsigned char> object;
    } FileContent;

};

struct CompilerState {
    CompilerStage finish_stage;
    string link_output = "";
    vector<unsigned char> link_content;
    vector<FileState> tasks;
};

/// Cleans up resources on error
void clean_up_early_exit() noexcept;

/// Cleans up resources on compilation finish
void clean_up_normal_exit() noexcept;

#endif
