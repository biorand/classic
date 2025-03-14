#pragma once

#include <cstdint>

class MemoryManager
{
private:
    void* _handle;

public:
    MemoryManager();

    void Read(uint32_t address, void* buffer, size_t len);
    void Write(uint32_t address, const void* buffer, size_t len);

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

void init_re1(const MemoryManager&);
