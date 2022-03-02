// Common use utilities
#ifndef UTILS_H
#define UTILS_H

#include <string>
#include <vector>
#include <map>

#include "rutils.h"

#include "err_handle.h"

#define SUCCESS_EXIT 0
#define FAILURE_EXIT 1

using std::string;
using std::vector;
using std::map;
using namespace rutils;

#define UNUSED [[maybe_unused]]

/// Exits if error, better than littering code with if statements
/// @param error    error code to check if not success
/// @return error code
inline int check_error(int error) noexcept {
    if (error == FAILURE_EXIT) exit(error);
    return error;
}

#endif
