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

void MemoryManager::Nop(uint32_t address, uint32_t addressEnd)
{
    for (auto a = address; a < addressEnd; a++)
    {
        Write<uint8_t>(a, 0x90);
    }
}

void MemoryManager::Call(uint32_t address, void* fn)
{
    Write<uint8_t>(address, 0xE8);
    Write<uint32_t>(address + 1, reinterpret_cast<uint32_t>(fn) - address - 5);
}

void MemoryManager::HookJmp(uint32_t address, void* fn)
{
    Write<uint8_t>(address, 0xE9);
    Write<uint32_t>(address + 1, reinterpret_cast<uint32_t>(fn) - address - 5);
}

void* MemoryManager::AllocExecutableMemory(size_t len)
{
    return VirtualAlloc(nullptr, len, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
}
