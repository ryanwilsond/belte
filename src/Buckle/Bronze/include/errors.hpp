/* Global errno; error checking & handling */
#pragma once
#ifndef ERRORS_HPP
#define ERRORS_HPP

#define SUCCESS_EXIT_CODE 0
#define FATAL_EXIT_CODE 1
#define ERROR_EXIT_CODE 2 // compilation errors

#define COLOR_GRAY 8
#define COLOR_GREEN 10
#define COLOR_RED 12
#define COLOR_PURPLE 13
#define COLOR_WHITE 15

#include <string>
#include <cstdio>

#include <Windows.h>

#include "state.hpp"

extern int error;

using std::exit;
using std::string;
using std::printf;


/// Gets current console color
/// @param color    returned color
/// @return if successfully placed color in `ret`
bool GetConsoleColor(_Out_ WORD& color) noexcept;

/// Sets console color
/// @param color    color to use
/// @return if successfully changed color
bool SetConsoleColor(WORD color) noexcept;

/// Exits if seen error
void check_errors() noexcept;

/// Prints error message and changes global error to fatal
void RaiseFatalError(_In_ const string& msg) noexcept;

/// Prints error message and changes global error to error
void RaiseError(_In_ const string& msg) noexcept;

/// Prints warning message
void RaiseWarning(_In_ const string& msg) noexcept;

#endif
