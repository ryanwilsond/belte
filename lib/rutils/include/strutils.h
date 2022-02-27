#pragma once
#ifndef STR_UTILS_H
#define STR_UTILS_H

#include "baseutils.h"

#pragma GCC diagnostic ignored "-Wdeprecated-declarations"

_RUTILS

/// Calculates length of null-terminated string
/// @param _Ptr     null-terminated string
/// @return size of string by element count
_NODISCARD inline constexpr size_type sizeof_str(_In_z_ const char * const _Ptr) noexcept
{   // Iterates over string until a string terminator is found
    size_type _Size = 0;

    for (_Size=0; _Ptr[_Size]!='\0'; _Size++)
        {
        }

    return _Size;
}

/// Replaces all uppercase ascii characters with their respective lowercase characters
/// @param _Ptr     const pointer to const char * string, not modified, must be of size `_Count`
///                 not checked if null terminated
/// @param _Count   size of input string by elements
/// @return new string with replaced characters
/// @deprecated use std::string overload
_DEPRECATED _NODISCARD _Ret_z_ inline const char * lower(_In_reads_(_Count) const char * const _Ptr, const size_type _Count) noexcept
{   // Iterates over entire input string and checks if inside uppercase letters based on ascii
    // if so replaces with lowercase and appends to string; else appends to string
    char * _New_ptr = new char[_Count+1];
    _New_ptr[_Count] = '\0';

    for (size_type _Index=0; _Index<_Count; ++_Index)
        {
        const char _Elem = _Ptr[_Index];

        if (_Elem >= 'A' && _Elem <= 'Z')
            {
            _New_ptr[_Index] = _Elem - ('Z' - 'z');
            }
        else
            {
            _New_ptr[_Index] = _Elem;
            }
        }

    return _New_ptr;
}

/// @overload uses a null-terminated string
/// @deprecated use std::string overload
_DEPRECATED _NODISCARD _Ret_z_ inline const char * lower(_In_z_ const char * const _Ptr) noexcept
{
    const size_type _Count = sizeof_str(_Ptr);
    return lower(_Ptr, _Count);
}

/// @overload uses std::string
_NODISCARD inline std::string lower(const std::string& _Str) noexcept
{
    std::string _New;

    for (char _Elem : _Str)
        {
        if (_Elem >= 'A' && _Elem <= 'Z')
            {
            _New += _Elem - ('Z' - 'z');
            }
        else
            {
            _New += _Elem;
            }
        }

    return _New;
}

/// Replaces all lowercase ascii characters with their respective uppercase characters
/// @param _Ptr     const pointer to const char * string, not modified, must be of size `_Count`
///                 not checked if null terminated
/// @param _Count   size of input string by elements
/// @return new string with replaced characters
/// @deprecated use std::string overload
_DEPRECATED _NODISCARD _Ret_z_ inline const char * upper(_In_reads_(_Count) const char * const _Ptr, const size_type _Count) noexcept
{   // Iterates over entire input string and checks if inside lowercase letters based on ascii
    // if so replaces with uppercase and appends to string; else appends to string
    char * _New_ptr = new char[_Count+1];
    _New_ptr[_Count] = '\0';

    for (size_type _Index=0; _Index<_Count; ++_Index)
        {
        const char _Elem = _Ptr[_Index];

        if (_Elem >= 'a' && _Elem <= 'z')
            {
            _New_ptr[_Index] = _Elem + ('Z' - 'z');
            }
        else
            {
            _New_ptr[_Index] = _Elem;
            }
        }

    return _New_ptr;
}

/// @overload uses a null-terminated string
/// @deprecated use std::string overload
_DEPRECATED _NODISCARD _Ret_z_ inline const char * upper(_In_z_ const char * const _Ptr) noexcept
{
    const size_type _Count = sizeof_str(_Ptr);
    return upper(_Ptr, _Count);
}

/// @overload uses std::string
_NODISCARD inline std::string upper(const std::string& _Str) noexcept
{
    std::string _New;

    for (char _Elem : _Str)
        {
        if (_Elem >= 'a' && _Elem <= 'z')
            {
            _New += _Elem + ('Z' - 'z');
            }
        else
            {
            _New += _Elem;
            }
        }

    return _New;
}

/// Separates a string into segments dividing up by a deliminator, excluding deliminator
/// @param _Str     string to be split
/// @param _Delim   deliminator to split on, being removed in the process
/// @return string segments back-to-back
_NODISCARD inline std::vector<std::string> split_str(const std::string& _Str, const std::string& _Delim) noexcept
{   // Finds instance of delim, then adds substr to vector; increases offset to not repeat chunks
    std::vector<std::string> _Segs;    
    size_type _Off = 0;
    size_type _Pos = 0;

    while ((_Pos = _Str.find(_Delim, _Off)) != std::string::npos)
        {
        _Segs.push_back(_Str.substr(_Off, _Pos));
        _Off = _Pos;
        }

    return _Segs;
}

/// @overload delim as single space
_NODISCARD inline std::vector<std::string> split_str(const std::string& _Str) noexcept
{
    return split_str(_Str, " ");
}

/// Checks if string begins with a substring
/// @param _Str     string to check the beginning of
/// @param _Sub     substring to check for
/// @return if _Str starts with _Sub
_NODISCARD inline bool startswith(const std::string& _Str, const std::string& _Sub) noexcept
{   // Checks if the first instance of _Sub starts at index 0
    if (_Str.rfind(_Sub, 0) == 0)
        {
        return true;
        }

    return false;
}

/// Checks if string ends with a substring accounting for substring length
/// @param _Str     string to check end of
/// @param _Sub     substring to check for
/// @return if _Str ends with _Sub
_NODISCARD inline bool endswith(const std::string& _Str, const std::string& _Sub) noexcept
{   // Checks if substring can fit inside string to avoid index errors
    // checks if the last n characters (length of substring) match substring
    if (_Str.length() >= _Sub.length())
        {
        if (_Str.compare(_Str.length() - _Sub.length(), _Sub.length(), _Sub) == 0)
            {
            return true;
            }
        }

    return false;
}

/// Removes all instances if a substring from string
/// @param _Str     string to strip substring of
/// @param _Sub     substring to remove
/// @return string without full instances of substring
_NODISCARD inline std::string remove_str(const std::string& _Str, const std::string& _Sub) noexcept
{   // Copies string then erases instances of substring until none are found
    std::string _Copy = _Str;

    while (_Copy.find(_Sub) != std::string::npos)
        {
        _Copy.erase(_Copy.find(_Sub), _Sub.length());
        }

    return _Copy;
}

/// Removes all whitespace from string (' ', '\n', '\r', '\t') not just trimmed
/// @param _Str     string to strip of whitespace
/// @return string without any whitespace
_NODISCARD inline std::string remove_whitespace(const std::string& _Str) noexcept
{   // Calls remove for every type of whitespace
    // faster to iterate over entire string once than 4 calls to remove
    std::string _Copy = _Str;

    for (size_type _Index; _Index<_Copy.length(); ++_Index)
        {
        if (_Copy[_Index] == ' ' || _Copy[_Index] == '\n' || _Copy[_Index] == '\r' || _Copy[_Index] == '\t')
            {
            _Copy.erase(_Index, 1);
            _Index--; // entire string moves, so this makes _Index not increment
            }
        }

    return _Copy;
}

/// Concatenates all elements of vector into string with a seperator
/// @param _Segs    vector of string segments to concat to string (all elements)
/// @param _Sep     seperator between every element (not beginning or end)
/// @return combined string
_NODISCARD inline std::string join(const std::vector<std::string>& _Segs, const std::string& _Sep) noexcept
{   // Even though its a vector utility, its specific for string vectors so its here
    // Iterates vector (apart from last element) appending element and seperator to string
    // finally adds last vector element
    std::string _Comb;
    const size_type _Segs_size = _Segs.size();

    for (size_type _Index; _Index<_Segs_size-1; ++_Index)
        {
        _Comb += _Segs[_Index];
        _Comb += _Sep;
        }

    _Comb += _Segs[_Segs_size-1];
    return _Comb;
}

_RUTILS_END

#endif
