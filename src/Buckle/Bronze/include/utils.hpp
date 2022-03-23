/* Commonly used utility functions and libraries */
#pragma once
#ifndef UTILITIES_HPP
#define UTILITIES_HPP

#include <rutils.h>
#include <string>
#include <vector>
#include <map>
#include <iostream>

#include <cstdio>
#include <cwchar>
#include <clocale>
#include <io.h>
#include <fcntl.h>

#include "errors.hpp"
#include "state.hpp"

#ifndef _O_U16TEXT
    #define _O_U16TEXT 0x20000
#endif
#ifndef _O_U8TEXT
    #define _O_U8TEXT 0x40000
#endif
#ifndef _O_TEXT
    #define _O_TEXT 0x4000
#endif

using namespace rutils;

using std::printf;
using std::string;
using std::vector;
using std::map;
using std::make_unique;
using std::make_shared;
using std::unique_ptr;
using std::shared_ptr;
using std::cout;
using std::getline;
using std::cin;
using std::endl;
using std::wcout;
using wstring = std::basic_string<wchar_t>;

extern CompilerState state;
extern string me;

#endif
