// Handles checking and handling the compiler state, and redirecting to the buckle compiler
#ifndef COMPILER_H
#define COMPILER_H

#include "utils.h"
#include "cmdline.h"
#include "buckle.h"

/// Compiles for Win64 systems
/// @param state    compiler state
/// @return error
int compile_for_win64(_Inout_ CompilerState& state) noexcept;

/// Finishes compilation for .NET
/// @param state    compiler state
/// @return error
int compile_for_dotnet_core(_Inout_ CompilerState& state) noexcept;

/// Preprocessed code for all targets
/// @param state    compiler state
/// @return error
int preprocess_code(_Inout_ CompilerState& state) noexcept;

/// Assemble for Win64 systems
/// @param state    compiler state
/// @return error
int assemble_for_win64(_Inout_ CompilerState& state) noexcept;

/// Link for Win64 systems
/// @param state    compiler state
/// @return error
int link_for_win64(_Inout_ CompilerState& state) noexcept;

#endif
