#include "cmdline.hpp"

vector<string> convert_argv(int argc, _In_ char **argv) noexcept {
    return vector<string>(argv, argv+argc);
}

void extract_name_and_expand(_Inout_ vector<string>& args) noexcept {
    vector<string> parts = split_str(args[0], "\\");
    me = parts[parts.size()-1];
    me = split_str(me, ".")[0];
    args.erase(args.begin());
}
