#include "buckle.h"

int Buckle::Preprocess(_Inout_ string& code, _In_ const CompilerState& state) noexcept {
    return SUCCESS_EXIT;
}

int Buckle::Compile(_Inout_ string& code, _In_ const CompilerState& state) noexcept {
    return SUCCESS_EXIT;
}

int Buckle::CompileNET(_Inout_ string& code, _In_ const CompilerState& state) noexcept {
    return SUCCESS_EXIT;
}

int Buckle::Assemble(_In_ const string& code, _Out_ vector<unsigned char>& obj, _In_ const CompilerState& state) noexcept {
    return SUCCESS_EXIT;
}

int Buckle::Link(_In_ const vector<vector<unsigned char>>& binary, _Out_ vector<unsigned char> exe, _In_ const CompilerState& state) noexcept {
    return SUCCESS_EXIT;
}
