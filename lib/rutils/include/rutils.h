#pragma once
#ifndef RUTILS_H
#define RUTILS_H

#include "baseutils.h"
#include "strutils.h"
#include "ioutils.h"
#include "vecutils.h"

_RUTILS

template<class T>
inline void swap(T *_First, T *_Second) noexcept {
    T *_Temp = new T(*_First);
    // finish this
}

_RUTILS_END

#endif
