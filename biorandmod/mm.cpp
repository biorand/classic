#include "biorand.h"

#define _CRT_SECURE_NO_WARNINGS
#define WIN32_LEAN_AND_MEAN

#include <windows.h>

MemoryManager::MemoryManager()
{
    _handle = GetCurrentProcess();
}

void MemoryManager::Read(uint32_t address, void* buffer, size_t len)
{
    ReadProcessMemory(GetCurrentProcess(), (LPVOID)address, buffer, len, NULL);
}

void MemoryManager::Write(uint32_t address, const void* buffer, size_t len)
{
    WriteProcessMemory(GetCurrentProcess(), (LPVOID)address, buffer, len, NULL);
}
