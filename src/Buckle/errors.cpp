#include "errors.hpp"

int error;
extern string me;

void check_errors() noexcept {
    if (error) {
        clean_up_early_exit();
        exit(error);
    }
}

void RaiseFatalError(_In_ const string& msg) noexcept {
    printf("%s: ", me.c_str());

    HANDLE hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
    SetConsoleTextAttribute(hConsole, COLOR_RED);
    printf("fatal error: ");

    SetConsoleTextAttribute(hConsole, COLOR_WHITE);
    printf("%s\n", msg.c_str());

    error = FATAL_EXIT_CODE;
}

void RaiseError(_In_ const string& msg) noexcept {
    printf("%s: ", me.c_str());

    HANDLE hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
    SetConsoleTextAttribute(hConsole, COLOR_RED);
    printf("error: ");

    SetConsoleTextAttribute(hConsole, COLOR_WHITE);
    printf("%s\n", msg.c_str());

    error = ERROR_EXIT_CODE;
}

void RaiseWarning(_In_ const string& msg) noexcept {
    printf("%s: ", me.c_str());

    HANDLE hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
    SetConsoleTextAttribute(hConsole, COLOR_PURPLE);
    printf("warning: ");

    SetConsoleTextAttribute(hConsole, COLOR_WHITE);
    printf("%s\n", msg.c_str());
}
