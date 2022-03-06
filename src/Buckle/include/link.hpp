/* Linker entry point */
#pragma once
#ifndef LINKER_HPP
#define LINKER_HPP

#include "utils.hpp"

class Linker {
private:

    Linker() {}

public:

    static void link() noexcept;

};

#endif
