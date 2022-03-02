// Handles parsing the command line
#ifndef COMMAND_LINE_H
#define COMMAND_LINE_H

#include "utils.h"
#include "state.h"

/// Parses command-line arguments into a vector of strings to use easier
/// @param argv array of c-style strings for each argument
/// @param argc number of arguments
/// @return parsed vector of strings
_NODISCARD vector<string> convert_argv(_In_reads_(argc) char **argv, int argc) noexcept;

/// Decodes all command-line arguments to affect compiler behaviour and settings
/// @param args     parsed arguments
/// @param state    compiler state
/// @return error
int decode_options(_In_ const vector<string>& args, _Out_ CompilerState& state) noexcept;

/// Removes all files that are going to be used to not make them useable if compilation fails
/// @param state    compiler state
void clean_outfiles(_In_ CompilerState& state) noexcept;

#endif
