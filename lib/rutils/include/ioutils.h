#pragma once
#ifndef IO_UTILS_H
#define IO_UTILS_H

#include "baseutils.h"

#include <fstream>

#ifndef __has_include
    static_assert(false, "__has_include not supported");
#else
#    if __cplusplus >= 201703L && __has_include(<filesystem>)
#       include <filesystem>
        //  namespace fs = std::filesystem;
        using std::filesystem::current_path;
#   elif __has_include(<experimental/filesystem>)
#       include <experimental/filesystem>
        //  namespace fs = std::experimental::filesystem;
        using std::experimental::filesystem::current_path;
#   elif __has_include(<boost/filesystem.hpp>)
#       include <boost/filesystem.hpp>
        //  namespace fs = boost::filesystem;
        using boost::filesystem::current_path;
#   else
#       define _NO_FILESYSTEM_
#       include <unistd.h>
#   endif
#endif // __has_include

_RUTILS

/// Checks if a file exists
/// @param _Filename    name of file to check
/// @return if file was found
_NODISCARD inline bool file_exists(const std::string& _Filename) noexcept
{   // Checks if file exists by attempting to open it; if the file was opened it exists
    std::ifstream _Stream (_Filename.c_str());

    if (_Stream.is_open())
        {
        _Stream.close();
        return true;
        }

    return false;
}

/// Attempts to open file and read its contents by concating to string with newlines as padding
/// If failed to find file returns uninitialized string (use rutils::file_exists for certainty)
/// @param _Filename    name of file to read
/// @return lines of file back-to-back with newline padding
_NODISCARD inline std::string read_text(const std::string& _Filename) noexcept
{   // Checks if file exists, then reads line by line adding newline padding to string
    std::string _Buf;
    std::string _Buf_ln;
    std::ifstream _Stream (_Filename.c_str());

    if (_Stream.is_open())
        {
        while (std::getline(_Stream, _Buf_ln))
            {
            _Buf += _Buf_ln;
            _Buf += "\n";
            }

        _Stream.close();
        }

    return _Buf;
}

/// Attempts to open file and read its contents by appending lines to a vector, no padding
/// If failed to find file returns uninitialized vector (use rutils::file_exists for certainty)
/// @param _Filename    name of file to read
/// @return lines of file
_NODISCARD inline std::vector<std::string> read_lines(const std::string& _Filename) noexcept
{   // Checks if file exists then reads line by line appending to vector
    std::vector<std::string> _Buf;
    std::string _Buf_ln;
    std::ifstream _Stream (_Filename.c_str());

    if (_Stream.is_open())
        {
        while (std::getline(_Stream, _Buf_ln))
            {
            _Buf.push_back(_Buf_ln);
            }

        _Stream.close();
        }

    return _Buf;
}

/// Attemps to open file and read its contents byte by byte, appending to vector
/// If failed to find file returns uninitialized vector (use rutils::file_exists for certainty)
/// @param _Filename    name of file to read
/// @return bytes of file (unsigned)
_NODISCARD inline std::vector<unsigned char> read_bytes(const std::string& _Filename) noexcept
{   // Checks if file exists then reads byte by byte, casting to unsigned and appending to vector
    std::vector<unsigned char> _Buf;
    char _Buf_byte;
    std::ifstream _Stream (_Filename.c_str());

    if (_Stream.is_open())
        {
        while (_Stream.get(_Buf_byte))
            {
            _Buf.push_back(static_cast<unsigned char>(_Buf_byte));
            }

        _Stream.close();
        }

    return _Buf;
}

/// Writes single string to file; does not check if file exists before
/// @param _Filename    name of file to write
/// @param _Text        write data
inline void write_text(const std::string& _Filename, const std::string& _Text)
{   // Uses a binary stream to write a c-string to file
    std::ofstream _Stream (_Filename.c_str(), std::ofstream::binary);
    _Stream.write(_Text.c_str(), static_cast<long long int>(_Text.length()));
    _Stream.close();
}

/// Writes strings from vector to file; does not check if file exists before
/// @param _Filename    name of file to write
/// @param _Lines       lines to write (newline padding, all elements)
inline void write_lines(const std::string& _Filename, const std::vector<std::string>& _Lines)
{   // Concats strings in vector then writes a c-string to file using binary stream
    std::ofstream _Stream (_Filename.c_str(), std::ofstream::binary);
    std::string _Buf;

    for (std::string _Line : _Lines)
        {
        _Buf += _Line;
        _Buf += "\n";
        }

    _Stream.write(_Buf.c_str(), static_cast<long long int>(_Buf.length()));
    _Stream.close();
}

/// Writes bytes from vector to file; does not check if file exists before
/// @param _Filename    name of file to write
/// @param _Bytes       bytes to write (no padding, all elements)
inline void write_bytes(const std::string& _Filename, const std::vector<unsigned char>& _Bytes)
{   // Converts unsigned char 'bytes' to signed, then writes with binary stream
    std::ofstream _Stream (_Filename.c_str(), std::ios::binary);
    const size_type _Size = _Bytes.size();

    char *_Signed = new char[_Size];
    std::copy(_Bytes.begin(), _Bytes.end(), _Signed);

    _Stream.write(_Signed, static_cast<long long int>(_Size));
    _Stream.close();
    delete[] _Signed;
}

/// Attempts to find working directory
/// @return path to working directory
_NODISCARD inline std::string workingdir() noexcept
{   // Checks available options then finds directory respectively
    #ifdef _NO_FILESYSTEM_
        char *_Buf = new char[512]; // arbitrary
        getcwd(_Buf, 512);
        std::string _Path = _Buf;
        delete[] _Buf;

        return _Path;
    #else
        return current_path().string();
    #endif
}

/// Attempts to delete file
/// @param _Filename    file to delete
/// @return result from remove
inline int delete_file(std::string& _Filename) noexcept
{
    return remove(_Filename.c_str());
}

_RUTILS_END

#endif
