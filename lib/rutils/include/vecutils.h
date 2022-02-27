#pragma once
#ifndef VEC_UTILS_H
#define VEC_UTILS_H

#include "baseutils.h"

_RUTILS

/// Concatenates two vectors into a single vector
/// @param _Left    vector to start the final vector, all elements unsorted
/// @param _Right   appended after left vector, all elements unsorted
/// @return left and right vector back-to-back unsorted copy
template <typename T>
_NODISCARD inline std::vector<T> combine(const std::vector<T>& _Left, const std::vector<T>& _Right) noexcept
{   // Preallocates memory to be faster, then inserts both vectors
    std::vector<T> _Comb;
    _Comb.reserve(_Left.size() + _Right.size());
    _Comb.insert(_Comb.end(), _Left.begin(), _Left.end());
    _Comb.insert(_Comb.end(), _Right.begin(), _Right.end());

    return _Comb;
}

/// Gets a subset of vector by copying selected elements, not just a pointer to the subset
/// Does not check for bounds errors
/// @param _Vec     vector to target
/// @param _Start   starting index inclusive
/// @param _Length  amount of elements starting at _Start from the vector
/// @return copy of selected elements
template <typename T>
_NODISCARD inline std::vector<T> subset(const std::vector<T>& _Vec, const size_type _Start, const size_type _Length)
{   // Gets iterators to start and end positions then uses vector constructor to automate
    auto _First = _Vec.begin() + static_cast<long long int>(_Start);
    auto _Last = _Vec.begin() + static_cast<long long int>(_Start) + static_cast<long long int>(_Length);
    std::vector<T> _Sub (_First, _Last);

    return _Sub;
}

_RUTILS_END

#endif
