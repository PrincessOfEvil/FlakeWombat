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
    public static class Cooldown
        {
        public const float DECREASE_PER10_LEVELS = 0.75f;

        public static float AdjustCooldown(float input, float level) => input / (1f + DECREASE_PER10_LEVELS * level / 10f);
        }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedCooldown), typeof(Tool), typeof(Pawn), typeof(Thing))]
    public class FlakeWombat_VerbProperties_AdjustedCooldown : CooldownPatch
        {
        public static float Postfix(float ret, Tool tool, Pawn attacker, Thing equipment)
            {
            if ((equipment?.def.isAmmoWeapon() ?? false) && equipment.def.techLevel < TechLevel.Industrial && attacker != null)
                {
                return Cooldown.AdjustCooldown(ret, StatDefOf.ShootingAccuracyPawn.postProcessCurve.EvaluateInverted(attacker.GetStatValue(StatDefOf.ShootingAccuracyPawn)));
                }
            return ret;
            }
        }
    }
