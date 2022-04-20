using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using static HarmonyLib.AccessTools;

namespace FlakeWombat
    {
    public class DefExtension_Unstuffed : DefModExtension
        {
        public DefExtension_Unstuffed() { }
        }

    [HarmonyPatch(typeof(Thing), "DrawColor", MethodType.Getter )]
    public class FW_Thing_DrawColor : AmmoPatch
        {
        public static Color Postfix(Color ret, Thing __instance)
            {
            if (__instance.def.HasModExtension<DefExtension_Unstuffed>())
                if (__instance.def.graphicData != null)
                    return __instance.def.graphicData.color;
                else return Color.white;

            return ret;
            }
        }
    [HarmonyPatch(typeof(GenLabel), "NewThingLabel", new[] { typeof(BuildableDef), typeof(ThingDef), typeof(int) })]
    public class FW_GenLabel_NewThingLabel : AmmoPatch
        {
        public static void Prefix(BuildableDef entDef, ref ThingDef stuffDef, int stackCount)
            {
            if (entDef.HasModExtension<DefExtension_Unstuffed>())
                stuffDef = null;
            }
        }

    [HarmonyPatch(typeof(CompReloadable), "UsedOnce")]
    public class FW_CompReloadable_UsedOnce : AmmoPatch
        {
        public static bool Prefix(CompReloadable __instance)
            {
            if (Settings.ammoStatic && __instance is CompAmmo) return false;
            return true;
            }
        public static void Postfix(CompReloadable __instance)
            {
            if (__instance.Wearer == null || __instance is not CompAmmo) return;

            if (__instance.RemainingCharges <= 0)
                {
                if (__instance.Wearer.CurJobDef == JobDefOf.Hunt) __instance.Wearer.jobs.StopAll();

                List<Thing> list = ReloadableUtility.FindEnoughAmmo(__instance.Wearer, __instance.Wearer.Position, __instance, forceReload: false);
                if (list == null)
                    {
                    return;
                    }

                Job job = JobMaker.MakeJob(JobDefOf.Reload, (LocalTargetInfo)(Thing)__instance.parent);
                job.targetQueueB = list.Select(t => new LocalTargetInfo(t)).ToList(); 
                job.count = list.Sum(t => t.stackCount);
                job.count = Math.Min(job.count, __instance.MaxAmmoNeeded(true));

                __instance.Wearer.jobs.TryTakeOrderedJob(job);
                __instance.Wearer.jobs.jobQueue.EnqueueLast(JobMaker.MakeJob(JobDefOf.Goto, (LocalTargetInfo)__instance.Wearer.Position));
                }
            }
        }

    [HarmonyPatch(typeof(PawnInventoryGenerator), "GenerateInventoryFor")]
    public class FW_PawnInventoryGenerator_GenerateInventoryFor : AmmoPatch
        {
        public static void Postfix(Pawn p)
            {
            var weapon = p.getWeapon();
            var comp = weapon?.TryGetComp<CompAmmo>();
            if (comp != null)
                {
                Thing ammo = ThingMaker.MakeThing(comp.AmmoDef, GenStuff.RandomStuffFor(comp.AmmoDef));
                ammo.stackCount = comp.MaxCharges * Ammo.ammoPawnStart;
                p.inventory.TryAddAndUnforbid(ammo);
                }
            }
        }
    [HarmonyPatch(typeof(ScenPart_StartingThing_Defined), "PlayerStartingThings")]
    public class FW_ScenPart_StartingThing_Defined_PlayerStartingThings : AmmoSpawnPatch
        {
        public static IEnumerable<Thing> Postfix(IEnumerable<Thing> things)
            {
            foreach (Thing thing in things)
                {
                yield return thing;
                if (thing.def.isAmmoWeapon())
                    {
                    CompReloadable comp = thing.TryGetComp<CompAmmo>();
                    if (comp != null)
                        {
                        Thing ammo = ThingMaker.MakeThing(comp.AmmoDef, GenStuff.RandomStuffFor(comp.AmmoDef));
                        ammo.stackCount = comp.MaxCharges * Ammo.ammoStart;
                        yield return ammo;
                        }
                    }
                }
            }
        }
    [HarmonyPatch(typeof(ScenPart_ScatterThings), "GenerateIntoMap")]
    public class FW_ScenPart_ScatterThings_GenerateIntoMap : AmmoSpawnPatch
        {
        private static FieldRef<ScenPart_ScatterThings, ThingDef> thing = AccessTools.FieldRefAccess<ThingDef>(typeof(ScenPart_ScatterThings), "thingDef");
        private static MethodInfo NearPlayerStart = AccessTools.PropertyGetter(typeof(ScenPart_ScatterThings), "NearPlayerStart");
        public static void Postfix(Map map, ScenPart_ScatterThings __instance)
            {
            var t = thing(__instance);
            if (t.isAmmoWeapon())
                {
                CompProperties_Reloadable comp = t.GetCompProperties<CompProperties_Ammo>();
                if (comp != null)
                    {
                    GenStep_ScatterThings genStep_ScatterThings = new GenStep_ScatterThings();
                    genStep_ScatterThings.nearPlayerStart = (bool)NearPlayerStart.Invoke(__instance, new object[] { });
                    genStep_ScatterThings.allowFoggedPositions = !(bool)NearPlayerStart.Invoke(__instance, new object[] { });
                    genStep_ScatterThings.thingDef = comp.ammoDef;
                    genStep_ScatterThings.stuff = GenStuff.RandomStuffFor(t);
                    genStep_ScatterThings.count = comp.maxCharges * Ammo.ammoStart;
                    genStep_ScatterThings.spotMustBeStandable = true;
                    genStep_ScatterThings.minSpacing = 5f;
                    genStep_ScatterThings.clusterSize = ((t.category == ThingCategory.Building) ? 1 : 4);
                    genStep_ScatterThings.allowRoofed = __instance.allowRoofed;
                    genStep_ScatterThings.Generate(map, default(GenStepParams));
                    }
                }
            }
        }
    }
