#pragma once
#ifndef RUTILS_H
#define RUTILS_H

#include "baseutils.h"
#include "strutils.h"
#include "ioutils.h"
#include "vecutils.h"

_RUTILS

/// Swaps two pointers so they each point to each others data
/// @param _First   will point to `_Second`
/// @param _Second  will point to `_First`
template<typename T>
inline constexpr void swap(_Inout_ T * _First,_Inout_ T * _Second) noexcept
{   // Makes a new pointer to _First, then sets _First to _Second, the _Second to _Temp
    T * _Temp = _First;
    _First = _Second;
    _Second = _First;
}

_RUTILS_END

#endif
