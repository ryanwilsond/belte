#include "errors.hpp"

int error;
extern string me;

void check_errors() noexcept {
    if (error) {
        clean_up_early_exit();
        exit(error);
    }
}

bool GetConsoleColor(_Out_ WORD& ret) noexcept {
    CONSOLE_SCREEN_BUFFER_INFO info;
    if (!GetConsoleScreenBufferInfo(GetStdHandle(STD_OUTPUT_HANDLE), &info)) return false;
    ret = info.wAttributes;
    return true;
}

bool SetConsoleColor(WORD color) noexcept {
    if (!SetConsoleTextAttribute(GetStdHandle(STD_OUTPUT_HANDLE), color)) return false;
    return true;
}

void RaiseFatalError(_In_ const string& msg) noexcept {
    printf("%s: ", me.c_str());

    SetConsoleColor(COLOR_RED);
    printf("fatal error: ");

    SetConsoleColor(COLOR_WHITE);
    printf("%s\n", msg.c_str());

    error = FATAL_EXIT_CODE;
}

void RaiseError(_In_ const string& msg) noexcept {
    printf("%s: ", me.c_str());

    SetConsoleColor(COLOR_RED);
    printf("error: ");

    SetConsoleColor(COLOR_WHITE);
    printf("%s\n", msg.c_str());

    error = ERROR_EXIT_CODE;
}

void RaiseWarning(_In_ const string& msg) noexcept {
    printf("%s: ", me.c_str());

    SetConsoleColor(COLOR_PURPLE);
    printf("warning: ");

    SetConsoleColor(COLOR_WHITE);
    printf("%s\n", msg.c_str());
}
