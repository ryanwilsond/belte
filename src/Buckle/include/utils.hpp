/* Commonly used utility functions and libraries */
#pragma once
#ifndef UTILITIES_HPP
#define UTILITIES_HPP

#include <rutils.h>
#include <cstdio>
#include <string>
#include <vector>
#include <map>
#include <iostream>
#include <fcntl.h>

#include "errors.hpp"
#include "state.hpp"

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

extern CompilerState state;
extern string me;

#endif
