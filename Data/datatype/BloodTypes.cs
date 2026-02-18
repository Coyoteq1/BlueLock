using System.Collections.Generic;

namespace VAuto.Zone.Data.DataType
{
    public static class BloodTypes
    {
        public static readonly Dictionary<string, string> ByShortName = new()
        {
            ["Brute"] = "BloodType_Brute",
            ["Creature"] = "BloodType_Creature",
            ["DraculaTheImmortal"] = "BloodType_DraculaTheImmortal",
            ["Draculin"] = "BloodType_Draculin",
            ["GateBoss"] = "BloodType_GateBoss",
            ["Mutant"] = "BloodType_Mutant",
            ["None"] = "BloodType_None",
            ["Rogue"] = "BloodType_Rogue",
            ["Scholar"] = "BloodType_Scholar",
            ["VBlood"] = "BloodType_VBlood",
            ["Warrior"] = "BloodType_Warrior",
            ["Worker"] = "BloodType_Worker",
        };
    }
}
