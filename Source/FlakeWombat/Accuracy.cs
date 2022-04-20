using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace FlakeWombat
    {
    [HarmonyPatch(typeof(ShotReport), "HitFactorFromShooter", new Type[] { typeof(float), typeof(float) })]
    public class FlakeWombat_ShotReport_HitFactorFromShooter : AccuracyPatch
        {
        static bool Prefix(ref float __result, float accRating, float distance)
            {
            float level = StatDefOf.ShootingAccuracyPawn.postProcessCurve.EvaluateInverted(accRating);
            __result = 0.4f + level * 0.05f - distance * 0.025f;
            return false;
            }
        }

    [HarmonyPatch(typeof(VerbProperties), "GetHitChanceFactor")]
    public class FlakeWombat_VerbProperties_GetHitChanceFactor : AccuracyUnlimitedPatch
        {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionsIn, ILGenerator il)
            {
            foreach (CodeInstruction instruction in instructionsIn)
                {
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldloc_0)
                    {
                    yield return new CodeInstruction(OpCodes.Ret);
                    yield break;
                    }
                }
            }
        }
    }
