#pragma once

#define _CRT_SECURE_NO_WARNINGS

#include <cstdint>
#include <memory>

class MemoryManager
{
private:
    void* _handle;

public:
    MemoryManager();

    void Read(uint32_t address, void* buffer, size_t len);
    void Write(uint32_t address, const void* buffer, size_t len);
    void Nop(uint32_t address, uint32_t addressEnd);
    void Call(uint32_t address, void* fn);
    void HookJmp(uint32_t address, void* fn);
    void* AllocExecutableMemory(size_t len);

    template<typename T>
    T Read(uint32_t address)
    {
        T value{};
        Read(address, &value, sizeof(value));
        return value;
    }

    template<typename T>
    void Write(uint32_t address, const T& value)
    {
        Write(address, &value, sizeof(value));
    }
};

class REBase
{
public:
    virtual void LoadGame(const uint8_t* src, size_t pos, size_t size)
    {
    }

    virtual void SaveGame(uint8_t*& dst, size_t& size)
    {
        dst = nullptr;
        size = 0;
    }
};

std::unique_ptr<REBase> init_re1(const MemoryManager&);

void BioRandMessageBox(const char* title, const char* body);
