// Temp for learning more about compilers
#ifndef TOP_LEVEL_H
#define TOP_LEVEL_H

#include "utils.h"
#include <iostream>

using std::cout;
using std::endl;

namespace toplev {

/// Attemps to cast, throws if fails
/// @param _Val value to cast to <T>
/// @return (T)_Val if success
template <class T, class U>
_NODISCARD inline T try_cast(_UNUSED _In_ U& _Val) {
    if (typeid(T) == typeid(U)) {
        const T* _Val_CT = reinterpret_cast<const T*>(&_Val);
        T* _Val_T = const_cast<T*>(_Val_CT);
        return *_Val_T;
    }

    string _Msg = format("Failed to cast from '%s' to '%s'", typeid(U).name(), typeid(T).name());
    throw std::runtime_error(_Msg);
    // unreached
    T _Null_Temp = T();
    return _Null_Temp;
}

//* TEMP
/// Entry point for repl
/// @return error
int main();

}

#endif
