// Redirects to steps in the compilation phase
#ifndef BUCKLE_H
#define BUCKLE_H

#include "utils.h"
#include "cmdline.h"

class Buckle {

    Buckle() {}

public:

    /// Preprocessing input
    /// @param code     input belte code
    /// @param state    compiler state
    /// @return error
    static int Preprocess(_Inout_ string& code, _In_ const CompilerState& state) noexcept;

    /// Preprocessing input
    /// @param code     input belte code
    /// @param state    compiler state
    /// @return error
    static int Compile(_Inout_ string& code, _In_ const CompilerState& state) noexcept;

    /// Preprocessing input
    /// @param code     input belte code
    /// @param state    compiler state
    /// @return error
    static int CompileNET(_Inout_ string& code, _In_ const CompilerState& state) noexcept;

    /// Preprocessing input
    /// @param code     input assembly code
    /// @param obj      output object code
    /// @param state    compiler state
    /// @return error
    static int Assemble(_In_ const string& code, _Out_ vector<unsigned char>& obj, _In_ const CompilerState& state) noexcept;

    /// Preprocessing input
    /// @param code     input code
    /// @param exe      output exectuable
    /// @param state    compiler state
    /// @return error
    static int Link(_In_ const vector<vector<unsigned char>>& binary, _Out_ vector<unsigned char> exe, _In_ const CompilerState& state) noexcept;

};

#endif
