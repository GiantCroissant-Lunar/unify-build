#pragma once

#ifdef _WIN32
  #define MYLIB_EXPORT __declspec(dllexport)
#else
  #define MYLIB_EXPORT __attribute__((visibility("default")))
#endif

extern "C" {
    MYLIB_EXPORT int mylib_add(int a, int b);
}
