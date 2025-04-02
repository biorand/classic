#define _CRT_SECURE_NO_WARNINGS
#define WIN32_LEAN_AND_MEAN

#include "biorand.h"

#include <algorithm>
#include <cstdint>
#include <stdio.h>
#include <windows.h>

struct SCE_TASK
{
    uint8_t Routine_0;
    uint8_t Status;
    uint8_t Sub_ctr;
    uint8_t Task_level;
    uint8_t Ifel_ctr[4];
    uint8_t Loop_ctr[4];
    uint8_t Loop_if_class[16];
    uint8_t* Data;
    int32_t Lstack[16];
    int32_t Lbreak[16];
    int16_t Lcnt[16];
    int32_t Stack[32];
    uint8_t** pS_SP;
    int32_t Ret_addr[4];
    void* pWork;
    int16_t Spd[3];
    int16_t Dspd[3];
    int16_t Aspd[3];
    int16_t Adspd[3];
    int32_t R_no_bak;
};

static WCHAR _dataPath[4096];
static FILETIME _lastDataTimestamp;
static void* _largeWorkArea;
static void* _taskMemory;

static bool ReadFileData(HANDLE hFile, void* address, size_t size)
{
    DWORD dwRead;
    return ReadFile(hFile, address, size, &dwRead, NULL) && dwRead == size;
}

static void ProcessDataFile(const wchar_t* path)
{
    auto hFile = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, 0, NULL);
    if (hFile == INVALID_HANDLE_VALUE)
        return;

    FILETIME timestamp;
    if (!GetFileTime(hFile, NULL, NULL, &timestamp)) {
        CloseHandle(hFile);
        return;
    }

    if (CompareFileTime(&timestamp, &_lastDataTimestamp) < 1) {
        CloseHandle(hFile);
        return;
    }

    _lastDataTimestamp = timestamp;
    while (true)
    {
        uint32_t address;
        uint32_t size;
        if (!ReadFileData(hFile, &address, sizeof(address)))
            break;
        if (!ReadFileData(hFile, &size, sizeof(size)))
            break;

        uint8_t dataStack[256];
        uint8_t* data = dataStack;
        if (size > sizeof(dataStack))
        {
            data = (uint8_t*)malloc(size);
        }
        if (data != nullptr)
        {
            if (ReadFileData(hFile, data, size))
            {
                WriteProcessMemory(GetCurrentProcess(), (LPVOID)address, data, size, NULL);
            }
            if (data != dataStack)
            {
                free(data);
                data = nullptr;
            }
        }
    }
    CloseHandle(hFile);
}

static void SetupDataPath(HMODULE hExecutable)
{
    DWORD pathLen = GetModuleFileNameW(hExecutable, _dataPath, std::size(_dataPath));
    for (int i = (int)pathLen - 1; i >= 0; i--)
    {
        if (_dataPath[i] == L'\\' || _dataPath[i] == L'/')
        {
            _dataPath[i + 1] = L'\0';
            break;
        }
    }
    wcsncat(_dataPath, L"mod_biorand\\biorand.dat", std::size(_dataPath) - wcslen(_dataPath));
}

static DWORD WINAPI ModThreadRunner(LPVOID)
{
    while (true)
    {
        Sleep(5000);
        ProcessDataFile(_dataPath);
    }
    return 0;
}

typedef BYTE(*fnLoadGame)(LPVOID, LPCSTR, DWORD);
static fnLoadGame _originalLoadGame;

static BYTE LoadGameHookCallback(LPVOID buffer, LPCSTR filename, DWORD bytes)
{
    auto result = _originalLoadGame(buffer, filename, bytes);

    // file: 0x689A98+0x1F4
    // buffer: 0x689A98+0x1F4
    // inventory: 0x98E79C + 0x598

    auto player = (uint8_t*)((BYTE*)buffer + 0x20A);
    auto stage = (uint16_t*)((BYTE*)buffer + 0x378);
    auto room = (uint8_t*)((BYTE*)buffer + 0x37A);
    auto item0 = (BYTE*)buffer + 0x598;
    if (*stage == 0 && *room == 4)
    {
        auto AddressInventoryLeon = (BYTE*)(0x400000 + 0x001401B8);
        auto AddressInventoryClaire = (BYTE*)(0x400000 + 0x001401D9);

        auto src = *player == 0 ? AddressInventoryLeon : AddressInventoryClaire;
        auto dst = item0;
        for (int i = 0; i < 11; i++)
        {
            *dst++ = *src++;
            *dst++ = *src++;
            *dst++ = *src++;
            dst++;
        }
    }
    return result;
}

typedef void(*fnSplInit)(void*);
static void Spl_Init_2(void* spl)
{
    *((uint16_t*)((uint8_t*)spl + 0x0156)) = 200; // Health
    *((uint16_t*)((uint8_t*)spl + 0x0162)) = 200; // Max health

    auto cl = *((uint8_t*)spl + 0x0008);
    auto routine = (fnSplInit*)(0x53CAC8);
    routine[cl](spl);
}

static void Write8(uintptr_t address, uint8_t value)
{
    WriteProcessMemory(GetCurrentProcess(), (LPVOID)address, &value, sizeof(value), NULL);
}

static void Write32(uintptr_t address, uintptr_t value)
{
    WriteProcessMemory(GetCurrentProcess(), (LPVOID)address, &value, sizeof(value), NULL);
}

static void Write(uintptr_t address, void* data, size_t len)
{
    WriteProcessMemory(GetCurrentProcess(), (LPVOID)address, data, len, NULL);
}

void WriteJump(uintptr_t address, void* fn)
{
    auto addr = reinterpret_cast<uintptr_t>(fn);
    auto value = addr - address - 5;
    uint8_t data[5] = { 0 };
    data[0] = 0xE9; // JMP
    memcpy(data + 1, &value, sizeof(value));
    Write(address, data, sizeof(data));
}

static SCE_TASK* get_task(int task)
{
    return &((SCE_TASK*)_taskMemory)[task];
}

// 0x004E3F60
static void event_init(SCE_TASK* task, int evt_no)
{
    typedef void(*fn)(SCE_TASK*,int);
    fn f = (fn)0x004E3F60;
    f(task, evt_no);
}

static SCE_TASK* get_empty_task(int min, int max)
{
    for (auto i = min; i < max; i++)
    {
        auto task = get_task(i);
        if (task->Status == 0)
        {
            return task;
        }
    }
    return nullptr;
}

// 0x004E3FA0
static void event_exec(int task_no, int evt_no)
{
    uint8_t sce_type = *((uint8_t*)0x00695E68);
    auto min = 32;
    auto max = 100;
    auto cap = 10;
    if (sce_type != 0)
    {
        min = 100;
        max = 140;
        cap = 14;
        if (task_no < cap)
            task_no = 100 + task_no;
    }

    auto task = task_no >= cap ?
        get_empty_task(min, max) :
        get_task(task_no);
    if (task != nullptr)
    {
        task->Sub_ctr = 0;
        memset(task->Spd, 0, 0x18);
        event_init(task, evt_no);
    }
}

static void ExtendScdTasks()
{
    auto maxTasks = 140;
    auto taskLen = 372;
    auto taskMemory = (uintptr_t)calloc(maxTasks, taskLen);
    _taskMemory = (void*)taskMemory;
    auto doorId = 100;
    Write32(0x0045006B, taskMemory + 0x01 + (  doorId * taskLen));    // door_init
    Write32(0x00450090, taskMemory + 0x01 + (maxTasks * taskLen));    // door_init
    Write32(0x00450098, taskMemory + 0x00 + (  doorId * taskLen));    // door_init

    Write32(0x00450166, taskMemory + 0x01 + (  doorId * taskLen));    // door_move
    Write32(0x00450214, taskMemory + 0x01 + (  doorId * taskLen));    // door_move
    Write32(0x00450246, taskMemory + 0x1C + (  doorId * taskLen));    // door_scheduler_main
    Write32(0x004E39E6, taskMemory + 0x01 + (       0 * taskLen));    // scd_init
    Write32(0x004E3E57, taskMemory + 0x01 + (       0 * taskLen));    // sce_work_clr_set
    Write32(0x004E4315, taskMemory + 0x1C + (       0 * taskLen));    // sce_scheduler_main
    Write32(0x004E44A9, taskMemory + 0x01 + (       0 * taskLen));    // evt_kill

    Write32(0x004E39E1, maxTasks);                                    // scd_init
    Write8(0x004E3E73, doorId);                                       // sce_work_clr_set

    Write32(0x004E438C, doorId * taskLen);                            // sce_scheduler_main

    WriteJump(0x004E3FA0, event_exec);
}

static void RE2_HookLoadGame()
{
    BYTE b = 0;
    ReadProcessMemory(GetCurrentProcess(), (LPVOID)0x004C5E13, &b, sizeof(b), NULL);
    if (b != 0xE8)
    {
        // Not RE 2
        return;
    }

    if (ReadProcessMemory(GetCurrentProcess(), (LPVOID)(0x004C5E13 + 1), &_originalLoadGame, sizeof(_originalLoadGame), NULL))
    {
        _originalLoadGame = (fnLoadGame)(0x004C5E13 + 5 + (BYTE*)_originalLoadGame);
        auto cb = (BYTE*)&LoadGameHookCallback - 0x004C5E13 - 5;
        if (WriteProcessMemory(GetCurrentProcess(), (LPVOID)(0x004C5E13 + 1), &cb, sizeof(cb), NULL))
        {
        }
    }

    // When a stage is loaded, it sets up a work buffer that is used for room storage and operations
    // This is tiny from PSX days, but we can make it much larger to reduce chance of crashes when
    // we pack rooms full of stuff like enemies and NPCs
    _largeWorkArea = malloc(16 * 1024 * 1024);
    WriteProcessMemory(GetCurrentProcess(), (LPVOID)(0x4DEF10), &_largeWorkArea, sizeof(_largeWorkArea), NULL);
    b = 0xEB;
    WriteProcessMemory(GetCurrentProcess(), (LPVOID)(0x4AC5D1), &b, sizeof(b), NULL);
    WriteProcessMemory(GetCurrentProcess(), (LPVOID)(0x4B1AE8), &b, sizeof(b), NULL);

    auto address = (uintptr_t)&Spl_Init_2 - 0x4EF9AA - 5;
    BYTE instr[] = { 0xE8, 0x00, 0x00, 0x00, 0x00, 0x90, 0x90, 0x90, 0x90, 0x90 };
    memcpy(&instr[1], &address, sizeof(address));
    WriteProcessMemory(GetCurrentProcess(), (LPVOID)(0x4EF9AA), instr, sizeof(instr), NULL);

    ExtendScdTasks();
}

static void ModMain(HMODULE hExecutable)
{
    /*
    auto mm = MemoryManager();
    auto checkByte = mm.Read<uint8_t>(0x004C5E13);
    if (checkByte == 0x66)
    {
        // RE 1
        MessageBoxA(NULL, "This is RE 1", "RE 1", MB_OK);
    }
    else if (checkByte == 0xE8)
    {
        // RE 2
        RE2_HookLoadGame();
    }
    else
    {
        // RE 3
    }

    SetupDataPath(hExecutable);
    ProcessDataFile(_dataPath);
    CreateThread(NULL, 0, &ModThreadRunner, NULL, 0, NULL);
    */
}

void BioRandMessageBox(const char* title, const char* body)
{
    MessageBoxA(NULL, body, title, MB_OK | MB_ICONEXCLAMATION);
}

#define DLL_EXPORT EXTERN_C __declspec(dllexport)

static std::unique_ptr<REBase> _rebase;

DLL_EXPORT void Modsdk_post_init()
{
    auto mm = MemoryManager();
    auto checkByte = mm.Read<uint8_t>(0x004C5E13);
    if (checkByte == 0x66)
    {
        // RE 1
        _rebase = init_re1(mm);
    }
    else if (checkByte == 0xE8)
    {
        // RE 2
    }
    else
    {
        // RE 3
    }
}

DLL_EXPORT void Modsdk_load(uint8_t* src, size_t pos, size_t size)
{
    if (_rebase != NULL)
    {
        _rebase->LoadGame(src, pos, size);
    }
}

DLL_EXPORT void Modsdk_save(uint8_t*& dst, size_t& size)
{
    if (_rebase != NULL)
    {
        _rebase->SaveGame(dst, size);
    }
}

BOOL WINAPI DllMain(HMODULE hModule, DWORD dwReason, LPVOID lpReserved)
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        // ModMain(GetModuleHandleA(NULL));
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}
