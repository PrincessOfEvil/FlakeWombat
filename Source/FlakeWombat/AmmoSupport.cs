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

    public class StockGenerator_Tag_IgnoreStuff : StockGenerator_Tag
        {
        public static readonly FieldRef<StockGenerator_Tag, IntRange> THING_DEF_COUNT_RANGE = AccessTools.FieldRefAccess<IntRange>(typeof(StockGenerator_Tag), "thingDefCountRange");
        public static readonly FieldRef<StockGenerator_Tag, List<ThingDef>> EXCLUDED_THING_DEFS = AccessTools.FieldRefAccess<List<ThingDef>>(typeof(StockGenerator_Tag), "excludedThingDefs");
        public override IEnumerable<Thing> GenerateThings(int forTile, Faction faction = null)
            {
            List<ThingDef> generatedDefs = new List<ThingDef>();
            var excludedThingDefs = EXCLUDED_THING_DEFS(this);
            int numThingDefsToUse = THING_DEF_COUNT_RANGE(this).RandomInRange;
            for (int i = 0; i < numThingDefsToUse; i++)
                {
                if (!DefDatabase<ThingDef>.AllDefs.Where((ThingDef d) => this.HandlesThingDef(d) && d.tradeability.TraderCanSell() && d.PlayerAcquirable && (excludedThingDefs == null || !excludedThingDefs.Contains(d)) && !generatedDefs.Contains(d)).TryRandomElementByWeight(SelectionWeight, out var chosenThingDef))
                    {
                    break;
                    }
                yield return StockGeneratorUtility.TryMakeForStockSingle(chosenThingDef, base.RandomCountOf(chosenThingDef), faction);
                generatedDefs.Add(chosenThingDef);
                chosenThingDef = null;
                }
            }
        }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DrawColor), MethodType.Getter )]
    public class FW_Thing_DrawColor : AmmoPatch
        {
        public static Color Postfix(Color ret, Thing __instance)
            {
            if (__instance.def.HasModExtension<DefExtension_Unstuffed>()) return __instance.def.graphicData?.color ?? Color.white;

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

    [HarmonyPatch(typeof(CompReloadable), nameof(CompReloadable.UsedOnce))]
    public class FW_CompReloadable_UsedOnce : AmmoPatch
        {
        public static bool Prefix(CompReloadable __instance)
            {
            return __instance is not CompAmmo || !Settings.ammoStatic || !__instance.Wearer.def.race.IsMechanoid;
            }
        public static void Postfix(CompReloadable __instance)
            {
            if (__instance.Wearer == null || __instance is not CompAmmo) return;

            if (__instance.RemainingCharges > 0) return;
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
            __instance.Wearer.jobs.jobQueue.EnqueueLast(JobMaker.MakeJob(JobDefOf.Goto, __instance.Wearer.Position));
            }
        }

    [HarmonyPatch(typeof(PawnInventoryGenerator), nameof(PawnInventoryGenerator.GenerateInventoryFor))]
    public class FW_PawnInventoryGenerator_GenerateInventoryFor : AmmoPatch
        {
        public static void Postfix(Pawn p)
            {
            if (p.def.race.IsMechanoid) return;
            var weapon = p.getWeapon();
            var comp = weapon?.TryGetComp<CompAmmo>();
            if (comp == null) return;
            Thing ammo = ThingMaker.MakeThing(comp.AmmoDef, GenStuff.RandomStuffFor(comp.AmmoDef));
            ammo.stackCount = comp.MaxCharges * Ammo.ammoPawnStart;
            p.inventory.TryAddAndUnforbid(ammo);
            }
        }
    [HarmonyPatch(typeof(ScenPart_StartingThing_Defined), nameof(ScenPart_StartingThing_Defined.PlayerStartingThings))]
    public class FW_ScenPart_StartingThing_Defined_PlayerStartingThings : AmmoSpawnPatch
        {
        public static IEnumerable<Thing> Postfix(IEnumerable<Thing> things)
            {
            foreach (Thing thing in things)
                {
                yield return thing;
                if (!thing.def.isAmmoWeapon()) continue;
                CompReloadable comp = thing.TryGetComp<CompAmmo>();
                if (comp == null) continue;
                Thing ammo = ThingMaker.MakeThing(comp.AmmoDef, GenStuff.RandomStuffFor(comp.AmmoDef));
                ammo.stackCount = comp.MaxCharges * Ammo.ammoStart;
                yield return ammo;
                }
            }
        }
    [HarmonyPatch(typeof(ScenPart_ScatterThings), "GenerateIntoMap")]
    public class FW_ScenPart_ScatterThings_GenerateIntoMap : AmmoSpawnPatch
        {
        private static readonly FieldRef<ScenPart_ScatterThings, ThingDef> THING = AccessTools.FieldRefAccess<ThingDef>(typeof(ScenPart_ScatterThings), "thingDef");
        private static readonly MethodInfo NEAR_PLAYER_START = AccessTools.PropertyGetter(typeof(ScenPart_ScatterThings), "NearPlayerStart");
        public static void Postfix(Map map, ScenPart_ScatterThings __instance)
            {
            var t = THING(__instance);
            if (!t.isAmmoWeapon()) return;
            CompProperties_Reloadable comp = t.GetCompProperties<CompProperties_Ammo>();
            if (comp == null) return;
            GenStep_ScatterThings genStep_ScatterThings = new GenStep_ScatterThings
                {
                nearPlayerStart = (bool)FW_ScenPart_ScatterThings_GenerateIntoMap.NEAR_PLAYER_START.Invoke(__instance, new object[] { }),
                allowFoggedPositions = !(bool)FW_ScenPart_ScatterThings_GenerateIntoMap.NEAR_PLAYER_START.Invoke(__instance, new object[] { }),
                thingDef = comp.ammoDef,
                stuff = GenStuff.RandomStuffFor(t),
                count = comp.maxCharges * Ammo.ammoStart,
                spotMustBeStandable = true,
                minSpacing = 5f,
                clusterSize = ((t.category == ThingCategory.Building) ? 1 : 4),
                allowRoofed = __instance.allowRoofed
                };
            genStep_ScatterThings.Generate(map, default(GenStepParams));
            }
        }
    }
