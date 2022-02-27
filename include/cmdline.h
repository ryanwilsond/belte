#ifndef COMMAND_LINE_H
#define COMMAND_LINE_H

#include "utils.h"

enum Steps {
    Link,
    Assemble,
    Compile,
    Preprocess
};

enum Targets {
    Win64,
    NET
};

struct CompilerOptions {
    int optimize;
    int step;
    int target;
    string out;
    vector<string> in;
};

/// Parses command-line arguments into a vector of strings to use easier
/// @param argv array of c-style strings for each argument
/// @param argc number of arguments
/// @return parsed vector of strings
_NODISCARD vector<string> convert_argv(_In_reads_(argc) char **argv, int argc) noexcept;

/// Decodes all command-line arguments to affect compiler behaviour and settings
/// @param args     parsed arguments
/// @param options  all compiler option and flag evaluations
/// @return error
int decode_options(_In_ const vector<string>& args, _Out_ CompilerOptions& options) noexcept;

#endif
