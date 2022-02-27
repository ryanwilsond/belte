#ifndef COMPILER_H
#define COMPILER_H

#include "utils.h"
#include "cmdline.h"
#include "buckle.h"

/// Finishes compilation for Win64 systems
/// @param code     preprocessed code
/// @param options  compiler options
/// @return error
int compile_for_win64(_In_ const string& code, _In_ CompilerOptions& options) noexcept;

/// Finishes compilation for .NET
/// @param code     preprocessed code
/// @param options  compiler options
/// @return error
int compile_for_dotnet_core(_In_ const string& code, _In_ const CompilerOptions& options) noexcept;

/// Preprocessed code for all targets
/// @param options  compiler options
/// @return preprocessed code
_NODISCARD string preprocess_code(_In_ const CompilerOptions& options) noexcept;

#endif
