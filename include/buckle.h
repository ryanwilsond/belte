#ifndef BUCKLE_H
#define BUCKLE_H

#include "utils.h"
#include "cmdline.h"

class Buckle {

    Buckle() {}

public:

    /// Preprocessing input
    /// @param code     input belte code
    /// @param options  compiler options
    /// @return preprocessed code
    _NODISCARD static string Preprocess(_In_ const vector<string>& code, _In_ const CompilerOptions& options) noexcept;

    /// Preprocessing input
    /// @param code     input belte code
    /// @param options  compiler options
    /// @return produced assembly language code
    _NODISCARD static string Compile(_In_ const string& code, _In_ const CompilerOptions& options) noexcept;

    /// Preprocessing input
    /// @param code     input belte code
    /// @param options  compiler options
    /// @return produced idl code
    _NODISCARD static string CompileNET(_In_ const string& code, _In_ const CompilerOptions& options) noexcept;

    /// Preprocessing input
    /// @param code     input assembly code
    /// @param options  compiler options
    /// @return produced machine code
    _NODISCARD static vector<unsigned char> Assemble(_In_ const string& code, _In_ const CompilerOptions& options) noexcept;

    /// Preprocessing input
    /// @param code     input code
    /// @param options  compiler options
    /// @return produced executable
    _NODISCARD static vector<unsigned char> Link(_In_ const vector<vector<unsigned char>>& binary, _In_ const CompilerOptions& options) noexcept;

};

#endif
