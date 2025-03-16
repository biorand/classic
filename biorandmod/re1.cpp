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
    ITEM_MIX data[4];
};

static const ITEM_MIX_TABLE _blueHerbMix[] = {
    4,
    {
        { ITEM_ID_HERB_B, ITEM_ID_HERB_G, ITEM_ID_NONE, MIX_COMMON },
        { ITEM_ID_HERB_G, ITEM_ID_HERB_GB, ITEM_ID_NONE, MIX_COMMON },
        { ITEM_ID_HERB_GR, ITEM_ID_HERB_GRB, ITEM_ID_NONE, MIX_COMMON },
        { ITEM_ID_HERB_GG, ITEM_ID_HERB_GGB, ITEM_ID_NONE, MIX_COMMON }
    }
};

class RE1
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
        DisableDemo();
        FixFlamethrowerCombine();
        FixWasteHeal();
        FixNeptuneDamage();
        FixYawnPoison();
        UpdateMixes();
    }

private:
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
};

void init_re1(const MemoryManager& mm)
{
    RE1 re1(mm);
    re1.Apply();
}
