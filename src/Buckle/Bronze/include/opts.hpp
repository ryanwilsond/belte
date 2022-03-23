/* Parses command-line arguments */
#pragma once
#ifndef OPTIONS_H
#define OPTIONS_H

#include "utils.hpp"

/// Parses all command-line arguments and initializes compiler state
/// @param args command-line arguments
void decode_options(_In_ const vector<string>& args) noexcept;

/// Produces default output filenames for input files
void produce_output_filenames() noexcept;

/// Removes all about to be used output files to ensure they are updated
/// If compilation fails they wont be recreated
void clean_output_files() noexcept;

#endif
