#include "biorand.h"

enum ITEM_ID
{
    ITEM_ID_NONE = 0,
    ITEM_ID_HERB_R = 67,
    ITEM_ID_HERB_G = 68,
    ITEM_ID_HERB_B = 69,
    ITEM_ID_HERB_GR = 70,
    ITEM_ID_HERB_GG = 71,
    ITEM_ID_HERB_GB = 72,
    ITEM_ID_HERB_GRB = 73,
    ITEM_ID_HERB_GGG = 74,
    ITEM_ID_HERB_GGB = 75,
};

enum MIX_TYPE
{
    MIX_COMMON,
    MIX_RELOAD0,
    MIX_RELOAD1,
    MIX_AMMO,
    MIX_VJOLT0,
    MIX_VJOLT1,
    MIX_GRENADE0,
    MIX_GRENADE1,
};

struct ITEM_DATA
{
    uint8_t max;
    uint8_t icon;
    uint8_t mix_num;
    uint8_t alt_name;
};

struct ITEM_MIX
{
    uint8_t with;
    uint8_t result;
    uint8_t remain;
    uint8_t type;
};

struct ITEM_MIX_TABLE
{
    uint8_t count;
    ITEM_MIX data[6];
};

static const ITEM_MIX_TABLE _blueHerbMix[] = {
    6,
    {
        { ITEM_ID_HERB_B, ITEM_ID_HERB_G, ITEM_ID_NONE, MIX_COMMON },
        { ITEM_ID_HERB_G, ITEM_ID_HERB_GB, ITEM_ID_NONE, MIX_COMMON },
        { ITEM_ID_HERB_GR, ITEM_ID_HERB_GRB, ITEM_ID_NONE, MIX_COMMON },
        { ITEM_ID_HERB_GG, ITEM_ID_HERB_GGB, ITEM_ID_NONE, MIX_COMMON },
        { ITEM_ID_HERB_GB, ITEM_ID_HERB_GG, ITEM_ID_NONE, MIX_COMMON },
        { ITEM_ID_HERB_GGB, ITEM_ID_HERB_GGG, ITEM_ID_NONE, MIX_COMMON }
    }
};

constexpr const char* SAVEDATA_MAGIC = "BIORAND";
constexpr uint32_t CURRENT_SAVEDATA_VERSION = 1;

struct CustomDataHeader
{
    uint8_t magic[8];
    uint32_t version;
};

struct CustomData
{
    CustomDataHeader header;
    uint32_t flag_locks[8];
};

static CustomData _customData;

class RE1 : public REBase
{
private:
    MemoryManager _mm;

public:
    RE1(const MemoryManager& mm)
    {
        _mm = mm;
    }

    void Apply()
    {
        InitCustomData();
        DisableDemo();
        FixFlamethrowerCombine();
        FixWasteHeal();
        FixNeptuneDamage();
        FixYawnPoison();
        UpdateMixes();
        RemoveDrawerInkHack();
        EnableChrisNoInk();
        EnableChrisLockpick();
        IncreaseLockLimit();
    }

    void LoadGame(const uint8_t* src, size_t pos, size_t size) override
    {
        CustomDataHeader header;
        std::memset(&header, 0, sizeof(header));

        const uint8_t* customData = src + pos;
        size_t customDataLen = std::min(sizeof(_customData), size - pos);
        if (customDataLen >= sizeof(header))
        {
            std::memcpy(&header, customData, sizeof(header));
        }

        InitCustomData();
        if (std::memcmp(header.magic, SAVEDATA_MAGIC, 8) != 0)
        {
            BioRandMessageBox("BioRand", "Save data is incompatible with BioRand.");
        }
        else if (header.version > CURRENT_SAVEDATA_VERSION)
        {
            BioRandMessageBox("BioRand", "Save data is incompatible with this version of BioRand.");
        }
        else
        {
            std::memcpy(&_customData, customData, customDataLen);
            _customData.header.version = CURRENT_SAVEDATA_VERSION;
        }
    }

    void SaveGame(uint8_t*& dst, size_t& size) override
    {
        dst = reinterpret_cast<uint8_t*>(&_customData);
        size = sizeof(_customData);
    }

private:
    static void InitCustomData()
    {
        memset(&_customData, 0, sizeof(_customData));
        memcpy(_customData.header.magic, SAVEDATA_MAGIC, 8);
        _customData.header.version = CURRENT_SAVEDATA_VERSION;
    }

    void DisableDemo()
    {
        uint8_t data[] = { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };
        _mm.Write(0x48E031, data, sizeof(data));
    }

    void FixFlamethrowerCombine()
    {
        uint8_t data[] = { 0x90, 0x90, 0x90, 0x90 };

        // and bx, 0x7F -> nop
        _mm.Write(0x4483BD, data, sizeof(data));

        // and bx, 0x7F -> nop
        _mm.Write(0x44842D, data, sizeof(data));
    }

    void FixWasteHeal()
    {
        // Allow using heal items when health is at max
        // jge 0447AA2h -> nop
        uint8_t data[] = { 0x90, 0x90 };
        _mm.Write(0x447A39, data, sizeof(data));

        // Allow using blue herbs when not poisoned
        // je 0447AADh -> nop
        _mm.Write(0x447AAD, data, sizeof(data));
        // je 0447AE7h -> nop
        _mm.Write(0x447AE7, data, sizeof(data));
    }

    void FixNeptuneDamage()
    {
        // Neptune has no death routine, so replace it with Cerberus's
        // 0x4AA0EC -> 0x004596D0
        _mm.Write<uint32_t>(0x4AA0EC, 0x004596D0);

        // Give Neptune a damage value for each weapon
        const int neptune = 11;
        const int numWeapons = 10;
        const int entrySize = 12;
        uint16_t damageValues[] = { 16, 14, 32, 40, 130, 20, 100, 200, 100, 900 };
        uint32_t enemyDataArrays[] = { 0x4AF908U, 0x4B0268 };
        for (int i = 0; i < 2; i++)
        {
            auto neptuneData = enemyDataArrays[i] + (neptune * (numWeapons * entrySize)) + 0x06;
            for (int i = 0; i < numWeapons; i++)
            {
                _mm.Write(neptuneData, damageValues[i]);
                neptuneData += entrySize;
            }
        }
    }

    void FixYawnPoison()
    {
        const uint8_t ST_POISON = 0x02;
        const uint8_t ST_POISON_YAWN = 0x20;

        _mm.Write(0x45B8C0 + 6, ST_POISON);
    }

    void UpdateMixes()
    {
        SetMixData(28, _blueHerbMix);
    }

    const ITEM_MIX_TABLE* GetMixData(uint8_t mixNum)
    {
        auto mixData = (const ITEM_MIX_TABLE**)0x4CCC08;
        return mixData[mixNum];
    }

    void SetMixData(uint8_t mixNum, const ITEM_MIX_TABLE* table)
    {
        auto mixData = (const ITEM_MIX_TABLE**)0x4CCC08;
        mixData[mixNum] = table;
    }

    void RemoveDrawerInkHack()
    {
        _mm.Nop(0x4563C0, 0x4563F7);
    }

    void EnableChrisNoInk()
    {
        _mm.Nop(0x456659, 0x45666B);
        _mm.Nop(0x456A90, 0x456A9B);
    }

    void EnableChrisLockpick()
    {
        _mm.Nop(0x455BA4, 0x455BAF); // Allow Chris to use lockpick
        _mm.Nop(0x455BCB, 0x455BD5); // Allow Jill to use sword key
    }

    void IncreaseLockLimit()
    {
        _mm.Write<uint8_t>(0x455B4C, 0); // Disable 0x40 bit check (Rebecca no entry)
        _mm.Write<uint8_t>(0x455B73, 0xFF); // Make it so 0 means unlocked, not ~0x80
        _mm.Nop(0x455B7A, 0x455B7D); // Mask to remove lock and Rebecca bits (for check)
        _mm.Nop(0x455C81, 0x455C83); // Mask to remove lock and Rebecca bits (for set)

        // Store flags in a new location
        auto flagAddress = _customData.flag_locks;
        _mm.Write(0x417F83, flagAddress);
        _mm.Write(0x418066, flagAddress);
        _mm.Write(0x4520B5, flagAddress);
        _mm.Write(0x4520D4, flagAddress);
        _mm.Write(0x455B83, flagAddress);
        _mm.Write(0x455C87, flagAddress);
        _mm.Write(0x455FDF, flagAddress);
        _mm.Write(0x4562CB, flagAddress);
        _mm.Write(0x456483, flagAddress);

        _mm.HookJmp(0x48DB4A, PostGameInit);
    }

    static void PostGameInit()
    {
        InitCustomData();
    }
};

std::unique_ptr<REBase> init_re1(const MemoryManager& mm)
{
    auto result = std::make_unique<RE1>(mm);
    result->Apply();
    return result;
}
