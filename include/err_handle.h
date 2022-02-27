#ifndef ERROR_HANDLING_H
#define ERROR_HANDLING_H

#include <string>
#include <stdio.h>
#include <windows.h>
#include <memory>
#include <stdarg.h>

#define COLOR_GREEN 10
#define COLOR_RED 12
#define COLOR_PURPLE 13
#define COLOR_WHITE 15

using std::string;

extern string me;

/// Implementation of std::format
/// @param format   format string
/// @return formatted string
inline string StringFormat(_In_ const string& format, ...) {
    va_list argptr;
    va_start(argptr, format);
    const int size_s = std::vsnprintf(nullptr, 0, format.c_str(), argptr) + 1; // +1 for terminator
    va_end(argptr);
    va_start(argptr, format); // reset argptr, probably better way to do this
    auto size = static_cast<size_t>( size_s );
    auto buf = std::make_unique<char[]>( size );
    std::vsnprintf(buf.get(), size, format.c_str(), argptr);
    va_end(argptr);
    return string(buf.get(), buf.get() + size - 1); // remove terminator space
}

inline void _print_color_me(_In_ const string& msg1, int color, _In_ const string& msg2) noexcept {
    HANDLE hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
    SetConsoleTextAttribute(hConsole, COLOR_WHITE);
    printf("%s: ", me.c_str());
    SetConsoleTextAttribute(hConsole, color);
    printf("%s", msg1.c_str());
    SetConsoleTextAttribute(hConsole, COLOR_WHITE);
    printf("%s", msg2.c_str());
}

/// Prints an error
/// @param line    line number
/// @param format   format string (printf syntax)
/// @return error
inline int RaiseError(int line, _In_ const string& format, ...) {
    va_list argptr;
    va_start(argptr, format);
    const int size_s = std::vsnprintf(nullptr, 0, format.c_str(), argptr) + 1;
    va_end(argptr);
    va_start(argptr, format);
    auto size = static_cast<size_t>( size_s );
    auto buf = std::make_unique<char[]>( size );
    std::vsnprintf(buf.get(), size, format.c_str(), argptr);
    va_end(argptr);
    string msg (buf.get(), buf.get() + size - 1);

    _print_color_me("error: ", COLOR_RED, StringFormat("%i: %s\n", line, msg));
    return 1;
}

/// @overload no line number
inline int RaiseError(_In_ const string& format, ...) {
    va_list argptr;
    va_start(argptr, format);
    const int size_s = std::vsnprintf(nullptr, 0, format.c_str(), argptr) + 1;
    va_end(argptr);
    va_start(argptr, format);
    auto size = static_cast<size_t>( size_s );
    auto buf = std::make_unique<char[]>( size );
    std::vsnprintf(buf.get(), size, format.c_str(), argptr);
    va_end(argptr);
    string msg (buf.get(), buf.get() + size - 1);

    _print_color_me("error: ", COLOR_RED, msg);
    return 1;
}

/// Prints an error
/// @param line     line number
/// @param format   format string (printf syntax)
/// @return error
inline int RaiseWarning(int line, _In_ const string& format, ...) {
    va_list argptr;
    va_start(argptr, format);
    const int size_s = std::vsnprintf(nullptr, 0, format.c_str(), argptr) + 1;
    va_end(argptr);
    va_start(argptr, format);
    auto size = static_cast<size_t>( size_s );
    auto buf = std::make_unique<char[]>( size );
    std::vsnprintf(buf.get(), size, format.c_str(), argptr);
    va_end(argptr);
    string msg (buf.get(), buf.get() + size - 1);

    _print_color_me("error: ", COLOR_PURPLE, StringFormat("%i: %s\n", line, msg));
    return 2;
}

/// @overload no line number
inline int RaiseWarning(_In_ const string& format, ...) {
    va_list argptr;
    va_start(argptr, format);
    const int size_s = std::vsnprintf(nullptr, 0, format.c_str(), argptr) + 1;
    va_end(argptr);
    va_start(argptr, format);
    auto size = static_cast<size_t>( size_s );
    auto buf = std::make_unique<char[]>( size );
    std::vsnprintf(buf.get(), size, format.c_str(), argptr);
    va_end(argptr);
    string msg (buf.get(), buf.get() + size - 1);

    _print_color_me("error: ", COLOR_PURPLE, msg);
    return 2;
}


#endif
