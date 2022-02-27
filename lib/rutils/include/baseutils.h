#pragma once
#ifndef BASE_UTILS_H
#define BASE_UTILS_H

#include <Windows.h>

#include <string>
#include <vector>

#define _NODISCARD [[nodiscard]]
#define _UNUSED [[maybe_unused]]
#define _DEPRECATED [[deprecated]]

#define _RUTILS namespace rutils {
#define _RUTILS_END }

_RUTILS

using size_type = long long unsigned int;

_RUTILS_END

#endif
