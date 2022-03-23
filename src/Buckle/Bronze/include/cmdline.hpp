/* Handles command-line arguments */
#pragma once
#ifndef COMMAND_LINE_HPP
#define COMMAND_LINE_HPP

#include "utils.hpp"

/// Parses argv into vector
/// @param argc argument count
/// @param argv arguments
/// @return parsed arguments
_NODISCARD vector<string> convert_argv(int argc, _In_ char **argv) noexcept;

/// Gets program name and advances arguments
/// @param args arguments
void extract_name_and_expand(_Inout_ vector<string>& args) noexcept;

#endif
