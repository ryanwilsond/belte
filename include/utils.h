#ifndef UTILS_H
#define UTILS_H

#include <string>
#include <vector>

#include "rutils.h"

#include "err_handle.h"

using std::string;
using std::vector;
using namespace rutils;

#define UNUSED [[maybe_unused]]

/// Exits if error, better than littering code with if statements
/// @param error    error code to check if not success
/// @return error code
inline int check_error(int error) noexcept {
    if (error > 0) exit(error);
    return error;
}

#endif
