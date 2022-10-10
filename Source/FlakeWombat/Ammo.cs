using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using HugsLib;
using Verse.AI;
using static HarmonyLib.AccessTools;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ArrangeStaticMemberQualifier

namespace FlakeWombat
    {
    public static class Ammo
        {
        public static int revolverAmmo = 6;
        public static int neolithicAmmo = 12;
        public static int neolithicHeavyAmmo = 3;
        public static int ammo = 10;
        public static int heavyAmmo = 5;
        public static int superHeavyAmmo = 1;

        public static int mercyCutoff = 5;
        public static int heavyCutoff = 20;
        public static int superHeavyCutoff = 40;
        public static float ammoPerSecondCutoff = 2f;

        public static int damagePerShell = 18;
        public static int damagePerNade = 20;
        public static int damagePerCell = 10;

        public static int baseReload = 10;

        public static int ammoStart = 3;
        public static int ammoPawnStart = 8;

        public static float bulletSpeedMultiplierPistol = 2f;
        public static float bulletSpeedMultiplierRifle = 4f;

        public static void OnEarlyDefsLoaded()
            {
            foreach (ThingDef thing in from t in DefDatabase<ThingDef>.AllDefs
                                       where t.isAmmoWeapon()
                                       select t)
                {
                var comp = thing.GetCompProperties<CompProperties_Ammo>() ?? new CompProperties_Ammo() { compClass = typeof(CompAmmo) };

                comp.ammoDef = thing.ammoBaseDef();

                comp.maxCharges = thing.shotsPerMag();
                comp.ammoCountPerCharge = thing.ammoPerShot();

                comp.baseReloadTicks = baseReload * thing.Verbs[0].burstShotCount;

                comp.chargeNoun = "shot";

                thing.comps.Add(comp);
                }
            }
        public static void OnLateDefsLoaded()
            {
            foreach (ThingDef thing in from t in DefDatabase<ThingDef>.AllDefs
                                       where t.isAmmoWeapon()
                                       select t)
                {
                var bullet = thing.Verbs[0].defaultProjectile;

                bullet.projectile.speed *= thing.isShotgun() || thing.isPistol() ? bulletSpeedMultiplierPistol : thing.isRifle() ? bulletSpeedMultiplierRifle : 1f;
                }
            }
        }

    public class CompAmmo : CompReloadable
        {
        protected static readonly FieldRef<CompReloadable, int> REMAINING_CHARGES = AccessTools.FieldRefAccess<int>(typeof(CompReloadable), "remainingCharges");
        public AmmoSubTypeDef currentAmmo;
        protected List<AmmoSubTypeDef> ammoTypesCached;

        public ThingStuffPairWithQuality? currentAmmoThing;

        public ThingDef RealAmmo => parent.def.ammoDef(currentAmmo);
        
        public override void PostExposeData()
            {
            base.PostExposeData();
            Scribe_Defs.Look(ref currentAmmo, "FW.currentAmmo");
            Scribe_Deep.Look(ref currentAmmoThing, "FW.currentAmmoThing");
            }
        public override string CompInspectStringExtra()
            {
            return
                "Ammo: " + this.RealAmmo.label + "\n" +
                "Ammo per second: " + parent.def.ammoPerSecond() + "\n" +
                base.CompInspectStringExtra();
            }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
            {
            foreach (var stat in base.SpecialDisplayStats())
                yield return stat.withCategory(DefOf.StatCategory_Ammo);

            yield return new StatDrawEntry(DefOf.StatCategory_Ammo, "FW_AmmoPerMinute".Translate(), (parent.def.ammoPerSecond() * parent.def.ammoPerShot() * 60).ToString("F2"), "FW_AmmoPerMinute.desc".Translate(), 2);
            }

        public IEnumerable<Gizmo> GetAmmoGizmos()
            {
            if (!Wearer.IsColonistPlayerControlled || !Wearer.drafter.Drafted) yield break;
            if (!Settings.ammoStatic)
                {
                Command_Action reload = new()
                    {
                    defaultLabel = "FW_ReloadShort".Translate(),
                    defaultDesc = "Reload".Translate(parent.Named("GEAR"), AmmoDef.Named("AMMO")),
                    icon = ContentFinder<Texture2D>.Get("UI/Widgets/RotRight"),
                    action = forceReload
                    };

                yield return reload;
                }

            Command_Action changeAmmo = new()
                {
                defaultLabel = "FW_ChangeAmmo".Translate(),
                defaultDesc = "FW_ChangeAmmo".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Widgets/RotLeft"),
                action = delegate
                    {
                    if (ammoTypesCached.EnumerableNullOrEmpty()) initAmmoCache();
                    List<FloatMenuOption> list = ammoTypesCached.Select(ammo => new FloatMenuOption(ammo.LabelCap, delegate
                                                                     {
                                                                     unloadAmmo();
                                                                     currentAmmo = ammo;
                                                                     }))
                                                                .ToList();
                    FloatMenu floatMenu = new(list)
                        {
                        vanishIfMouseDistant = true
                        };
                    Find.WindowStack.Add(floatMenu);
                    }
                };

            yield return changeAmmo;
            
            if (Prefs.DevMode)
                {
                Command_Action devReload = new()
                    {
                    defaultLabel = "Debug: Reload to full",
                    action = delegate
                        {
                        REMAINING_CHARGES(this) = MaxCharges;
                        }
                    };
                yield return devReload;
                }
            }
        public void forceReload()
            {
            List<Thing> list = ReloadableUtility.FindEnoughAmmo(Wearer, Wearer.Position, this, forceReload: false);
            if (list == null)
                {
                return;
                }
            Wearer.jobs.StartJob(JobGiver_Reload.MakeReloadJob(this, list), JobCondition.InterruptForced);
            }

        public void unloadAmmo()
            {
            if (Settings.ammoStatic) return; 
            var a = ThingMaker.MakeThing(RealAmmo, RealAmmo.MadeFromStuff ? currentAmmoThing?.stuff ?? RealAmmo.defaultStuff : null);
            a.stackCount = RemainingCharges;
            Wearer.inventory.TryAddAndUnforbid(a);
            if (!Wearer.inventory.Contains(a) && a.stackCount > 0) GenSpawn.Spawn(a, Wearer.Position, Wearer.MapHeld);
            CompAmmo.REMAINING_CHARGES(this) = 0;
            }

        protected void initAmmoCache() 
            {
            ammoTypesCached = new List<AmmoSubTypeDef>();
            ammoTypesCached.AddRange(from a in DefDatabase<AmmoSubTypeDef>.AllDefsListForReading
                                     where
                                    (a.level == TechLevel.Undefined || a.level == parent.def.techLevel) &&
                                    ((a.type & parent.def.ammoType()) == parent.def.ammoType())
                                     select a);
            }
        }
    public class CompProperties_Ammo : CompProperties_Reloadable
        {
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
            {
            return base.SpecialDisplayStats(req).Select(stat => stat.withCategory(DefOf.StatCategory_Ammo));
            }
        }
    [HarmonyPatch(typeof(CompReloadable), nameof(CompReloadable.ReloadFrom))]
    public class FW_CompReloadable_ReloadFrom : AmmoPatch
        {
        public static bool Prefix(Thing ammo, CompReloadable __instance)
            {
            if (__instance is CompAmmo comp && comp.NeedsReload(true) && ammo.stackCount < (__instance.Props.ammoCountToRefill != 0 ? __instance.Props.ammoCountToRefill : __instance.Props.ammoCountPerCharge)) 
                {
                ThingStuffPairWithQuality ts = new(ammo.def, ammo.Stuff, QualityCategory.Normal);
                if (ts != comp.currentAmmoThing) comp.unloadAmmo();
                comp.currentAmmoThing = ts;
                }
            return true;
            }
        }

    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
    public class ThingDefGenerator_Ammo : AlwaysPatch
        {
        public static void Postfix()
            {
            foreach (var def in generateStockGroup())
                DefGenerator.AddImpliedDef(def);

            if (Settings.ammo) Ammo.OnEarlyDefsLoaded();
            }

        public static InventoryStockGroupDef[] generateStockGroup()
            {
            var def = new InventoryStockGroupDef
                {
                defName = "FW_Ammo",
                min = 0,
                max = 8,
                thingDefs = new List<ThingDef>()
                };

            foreach (ThingDef ammoDef in from t in DefDatabase<ThingDef>.AllDefs
                                         where t.thingCategories?.Contains(DefOf.ThingCategory_Ammo) ?? false
                                         select t)
                def.thingDefs.Add(ammoDef);
            def.modExtensions = new List<DefModExtension> { new DefExtension_Ammo() };
            def.modContentPack = DefOf.StatCategory_Ammo.modContentPack;

            return new[] { def };
            }
        }
    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PostResolve))]
    public class ThingDefGenerator_AmmoLate : AmmoPatch
        {
        public static void Postfix()
            {
            Ammo.OnLateDefsLoaded();
            }
        }

    [HarmonyPatch(typeof(ReloadableUtility), nameof(ReloadableUtility.WearerOf))]
    public class FW_ReloadableUtility_WearerOf : AmmoPatch
        {
        public static Pawn Postfix(Pawn __result, CompReloadable comp) 
            {
            if (__result == null) 
                {
                return (comp.ParentHolder as Pawn_EquipmentTracker)?.pawn ?? (comp.ParentHolder as Pawn_InventoryTracker)?.pawn;
                }
            return __result;
            }
        }

    [HarmonyPatch(typeof(ReloadableUtility), nameof(ReloadableUtility.FindSomeReloadableComponent))]
    public class FW_ReloadableUtility_FindSomeReloadableComponent : AmmoPatch
        {
        public static CompReloadable Postfix(CompReloadable __result, Pawn pawn, bool allowForcedReload)
            {
            if (__result == null) 
                {
                CompReloadable comp = pawn.getWeapon()?.TryGetComp<CompAmmo>();

                if (comp != null && comp.NeedsReload(allowForcedReload))
                    {
                    return comp;
                    }
                }
            return __result;
            }
        }

    [HarmonyPatch(typeof(ReloadableUtility), nameof(ReloadableUtility.FindEnoughAmmo))]
    public class FW_ReloadableUtility_FindEnoughAmmo : AmmoPatch
        {
        public static bool Prefix(ref List<Thing> __result, Pawn pawn, IntVec3 rootCell, CompReloadable comp, bool forceReload)
            {
            if (comp is CompAmmo)
                {
                IntRange desiredQuantity = new(comp.MinAmmoNeeded(forceReload), comp.MaxAmmoNeeded(forceReload));
                if (desiredQuantity.max == 0) return true;
                pawn.inventory.DropCount(comp.AmmoDef, desiredQuantity.max);
                }
            return true;
            }
        }
    [HarmonyPatch(typeof(ReloadableUtility), nameof(ReloadableUtility.FindPotentiallyReloadableGear))]
    public class FW_ReloadableUtility_FindPotentiallyReloadableGear : AmmoPatch
        {
        public static IEnumerable<Pair<CompReloadable, Thing>> Postfix(IEnumerable<Pair<CompReloadable, Thing>> __result, Pawn pawn, List<Thing> potentialAmmo)
            {
            if (__result.EnumerableNullOrEmpty())
                {
                CompAmmo comp = pawn.getWeapon()?.TryGetComp<CompAmmo>();

                if (comp?.AmmoDef != null)
                    {
                    foreach (Thing thing in potentialAmmo.Where(thing => thing.def == comp.AmmoDef))
                        {
                        yield return new Pair<CompReloadable, Thing>(comp, thing);
                        }
                    }
                }
            foreach (var ret in __result) yield return ret;
            }
        }
    // Patched in manually.
    public class FW_Pawn_EquipmentTracker_YieldGizmos
        {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, ThingWithComps eq, KeyBindingDef preferredHotKey)
            {
            foreach (Gizmo g in __result) yield return g;

            CompAmmo comp = eq.TryGetComp<CompAmmo>();
            if (comp == null) yield break;
            foreach (Gizmo gi in comp.GetAmmoGizmos()) yield return gi;
            } 
        }
    [HarmonyPatch(typeof(JobGiver_TakeForInventoryStock), "TryGiveJob")]
    public class FW_JobGiver_TakeForInventoryStock_TryGiveJob : AmmoPatch
        {
        public static Job Postfix(Job __result, Pawn pawn, JobGiver_TakeForInventoryStock __instance)
            {
            var findThingFor = AccessTools.MethodDelegate<Func<Pawn, ThingDef, Thing>>(AccessTools.Method(typeof(JobGiver_TakeForInventoryStock), "FindThingFor"), __instance);

            if (__result == null)
                {
                Thing weapon = pawn.getWeapon();
                var comp = weapon?.TryGetComp<CompAmmo>();
                if (comp == null)
                    return null;
                if (pawn.inventory.Count(comp.AmmoDef) > 0 && (pawn.CurJobDef?.driverClass == typeof(JobDriver_Hunt) || pawn.CurJobDef?.driverClass == typeof(JobDriver_Kill)))
                    return null;
                Thing thing = findThingFor(pawn, comp.AmmoDef);
                if (thing != null && comp.MaxCharges * pawn.inventoryStock.GetDesiredCountForGroup(DefOf.InventoryStockGroup_Ammo) > pawn.inventory.Count(thing.def))
                    {
                    Job job = JobMaker.MakeJob(JobDefOf.TakeCountToInventory, thing);
                    job.count = Mathf.Min(comp.MaxCharges * pawn.inventoryStock.GetDesiredCountForGroup(DefOf.InventoryStockGroup_Ammo) - pawn.inventory.Count(thing.def), thing.stackCount);
                    return job;
                    }
                }
            return __result;
            }
        }
    [HarmonyPatch(typeof(Pawn_InventoryStockTracker), nameof(Pawn_InventoryStockTracker.AnyThingsRequiredNow))]
    public class FW_Pawn_InventoryStockTracker_AnyThingsRequiredNow : AmmoPatch
        {
        public static bool Postfix(bool __result, Pawn_InventoryStockTracker __instance)
            {
            foreach ((InventoryStockGroupDef key, InventoryStockEntry value) in __instance.stockEntries)
                {
                if (!key.HasModExtension<DefExtension_Ammo>() && __instance.pawn.inventory.Count(value.thingDef) < value.count)
                    return true;
                Thing gun = __instance.pawn.getWeapon();
                var comp = gun?.TryGetComp<CompAmmo>();
                if (comp != null && __instance.pawn.inventory.Count(comp.AmmoDef) < value.count * comp.MaxCharges)
                    return true;
                }
            return __result;
            }
        }

    public class DefExtension_Ammo : DefModExtension
    {
    // ReSharper disable once EmptyConstructor
    // Weird vanilla glitch
    public DefExtension_Ammo() { }
    }

    public class PawnColumnWorker_CarryAmmo : PawnColumnWorker_Carry 
        {
        public static InventoryStockGroupDef Group => DefOf.InventoryStockGroup_Ammo;
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
            {
            if (pawn.inventoryStock != null)
                {
                Widgets.Dropdown(new Rect(rect.x, rect.y + 2f, rect.width - 4f, rect.height - 4f), pawn,
                    p => p.inventoryStock.GetDesiredCountForGroup(PawnColumnWorker_CarryAmmo.Group), p => PawnColumnWorker_CarryAmmo.GenerateCountButtons(p, PawnColumnWorker_CarryAmmo.Group),
                    pawn.inventoryStock.GetDesiredCountForGroup(PawnColumnWorker_CarryAmmo.Group).ToString(), null, null, null, null, paintable: true);
                }
            }
        private static IEnumerable<Widgets.DropdownMenuElement<int>> GenerateCountButtons(Pawn pawn, InventoryStockGroupDef group)
            {
            for (int i = group.min; i <= group.max; i++)
                {
                int localI = i;
                yield return new Widgets.DropdownMenuElement<int>
                    {
                    option = new FloatMenuOption(i.ToString(), delegate
                        {
                        pawn.inventoryStock.SetCountForGroup(group, localI);
                        }),
                    payload = i
                    };
                }
            }

        public override int Compare(Pawn a, Pawn b)
            {
            return PawnColumnWorker_CarryAmmo.GetValueToCompare(a).CompareTo(PawnColumnWorker_CarryAmmo.GetValueToCompare(b));
            }

        private static int GetValueToCompare(Pawn pawn) => pawn.inventoryStock?.GetDesiredCountForGroup(PawnColumnWorker_CarryAmmo.Group) ?? int.MinValue;

        public override int GetMinWidth(PawnTable table)
            {
            if (!def.label.NullOrEmpty())
                {
                Text.Font = DefaultHeaderFont;
                int result = Mathf.CeilToInt(Text.CalcSize(def.LabelCap).x);
                Text.Font = GameFont.Small;
                return result;
                }

            return def.HeaderIcon != null ? Mathf.CeilToInt(def.HeaderIconSize.x) : 1;
            }

        public override int GetOptimalWidth(PawnTable table)
            {
            return GetMinWidth(table);
            }

        public override int GetMinHeaderHeight(PawnTable table)
            {
            if (!def.label.NullOrEmpty())
                {
                Text.Font = DefaultHeaderFont;
                int result = Mathf.CeilToInt(Text.CalcSize(def.LabelCap).y);
                Text.Font = GameFont.Small;
                return result;
                }

            return def.HeaderIcon != null ? Mathf.CeilToInt(def.HeaderIconSize.y) : 0;
            }
        }
    }
