using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace FlakeWombat
    {
    public static class ModCompat
        {
        public static void Patch(Harmony harmony)
            {
            if (ModLister.HasActiveModWithName("Extra Stats") && Settings.cooldown)
                harmony.Patch(AccessTools.Method("ExtraStats.Extensions:GetRangedCooldown"), postfix: new HarmonyMethod(typeof(FlakeWombat_ExtraStats_Extensions_GetRangedCooldown), "Postfix"));
            }
        }

    public class FlakeWombat_ExtraStats_Extensions_GetRangedCooldown
        {
        public static float Postfix(float ret, Thing thing, float acc)
            {
            if (thing.def.isAmmoWeapon() && thing.def.techLevel < TechLevel.Industrial)
                {
                return Cooldown.AdjustCooldown(ret, acc);
                }
            return ret;
            }
        }
    }
