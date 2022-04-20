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

namespace FlakeWombat
    {
    public class Ammo
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
        protected FieldRef<CompReloadable, int> remainingCharges;
        public AmmoSubTypeDef currentAmmo;
        protected List<AmmoSubTypeDef> ammoTypesCached;

        public ThingStuffPair? currentAmmoThing;

        public ThingDef realAmmo => parent.def.ammoDef(currentAmmo);

        public CompAmmo() : base() 
            {
            remainingCharges = AccessTools.FieldRefAccess<int>(typeof(CompReloadable), "remainingCharges");
            }
        public override string CompInspectStringExtra()
            {
            return
                "Ammo: " + this.realAmmo.label + "\n" +
                "Ammo per second: " + parent.def.ammoPerSecond().ToString() + "\n" +
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
            if (Wearer.IsColonistPlayerControlled && Wearer.drafter.Drafted)
                {
                if (!Settings.ammoStatic)
                    {
                    Command_Action reload = new Command_Action();

                    reload.defaultLabel = "FW_ReloadShort".Translate();
                    reload.defaultDesc = "Reload".Translate(parent.Named("GEAR"), AmmoDef.Named("AMMO"));
                    reload.icon = ContentFinder<Texture2D>.Get("UI/Widgets/RotRight");
                    reload.action = forceReload;
                    yield return reload;
                    }

                Command_Action changeAmmo = new Command_Action();

                changeAmmo.defaultLabel = "RW_ChangeAmmo".Translate();
                changeAmmo.defaultDesc = "RW_ChangeAmmo".Translate();
                changeAmmo.icon = ContentFinder<Texture2D>.Get("UI/Widgets/RotLeft");
                changeAmmo.action = delegate
                    {
                        if (ammoTypesCached.EnumerableNullOrEmpty()) initAmmoCache();
                        List<FloatMenuOption> list = new List<FloatMenuOption>();
                        foreach (var ammo in ammoTypesCached)
                            {
                            list.Add(new FloatMenuOption(ammo.LabelCap, delegate
                                {
                                    unloadAmmo();
                                    currentAmmo = ammo;
                                    }));
                            }
                        FloatMenu floatMenu = new FloatMenu(list);
                        floatMenu.vanishIfMouseDistant = true;
                        Find.WindowStack.Add(floatMenu);
                        };
                yield return changeAmmo;
                

                if (Prefs.DevMode)
                    {
                    Command_Action devReload = new Command_Action();
                    devReload.defaultLabel = "Debug: Reload to full";
                    devReload.action = delegate
                        {
                        remainingCharges(this) = MaxCharges;
                        };
                    yield return devReload;
                    }
                }
            else yield break;
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
            var a = ThingMaker.MakeThing(realAmmo, realAmmo.MadeFromStuff ? currentAmmoThing?.stuff ?? realAmmo.defaultStuff : null); ;
            a.stackCount = RemainingCharges;
            Wearer.inventory.TryAddAndUnforbid(a);
            if (!Wearer.inventory.Contains(a) && a.stackCount > 0) GenSpawn.Spawn(a, Wearer.Position, Wearer.MapHeld);
            remainingCharges(this) = 0;
            }

        protected void initAmmoCache() 
            {
            ammoTypesCached = new();
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
            foreach (var stat in base.SpecialDisplayStats(req))
                yield return stat.withCategory(DefOf.StatCategory_Ammo);
            }
        }
    [HarmonyPatch(typeof(CompReloadable), "ReloadFrom")]
    public class FW_CompReloadable_ReloadFrom : AmmoPatch
        {
        public static bool Prefix(Thing ammo, CompReloadable __instance)
            {
            if (__instance is CompAmmo comp && comp.NeedsReload(true) && ammo.stackCount < (__instance.Props.ammoCountToRefill != 0 ? __instance.Props.ammoCountToRefill : __instance.Props.ammoCountPerCharge)) 
                {
                ThingStuffPair ts = new(ammo.def, ammo.Stuff);
                if (ts != comp.currentAmmoThing) comp.unloadAmmo();
                comp.currentAmmoThing = ts;
                }
            return true;
            }
        }

    [HarmonyPatch(typeof(DefGenerator), "GenerateImpliedDefs_PreResolve")]
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
            var def = new InventoryStockGroupDef();
            def.defName = "FW_Ammo";
            def.min = 0;
            def.max = 8;

            def.thingDefs = new();
            foreach (ThingDef ammoDef in from t in DefDatabase<ThingDef>.AllDefs
                                         where t.thingCategories?.Contains(DefOf.ThingCategory_Ammo) ?? false
                                         select t)
                def.thingDefs.Add(ammoDef);
            def.modExtensions = new List<DefModExtension>() { new DefExtension_Ammo() };
            def.modContentPack = DefOf.StatCategory_Ammo.modContentPack;

            return new[] { def };
            }
        }
    [HarmonyPatch(typeof(DefGenerator), "GenerateImpliedDefs_PostResolve")]
    public class ThingDefGenerator_AmmoLate : AmmoPatch
        {
        public static void Postfix()
            {
            Ammo.OnLateDefsLoaded();
            }
        }

    [HarmonyPatch(typeof(ReloadableUtility), "WearerOf")]
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

    [HarmonyPatch(typeof(ReloadableUtility), "FindSomeReloadableComponent")]
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

    [HarmonyPatch(typeof(ReloadableUtility), "FindEnoughAmmo")]
    public class FW_ReloadableUtility_FindEnoughAmmo : AmmoPatch
        {
        public static bool Prefix(ref List<Thing> __result, Pawn pawn, IntVec3 rootCell, CompReloadable comp, bool forceReload)
            {
            if (comp is CompAmmo)
                {
                IntRange desiredQuantity = new IntRange(comp.MinAmmoNeeded(forceReload), comp.MaxAmmoNeeded(forceReload));
                if (desiredQuantity.max == 0) return true;
                pawn.inventory.DropCount(comp.AmmoDef, desiredQuantity.max);
                }
            return true;
            }
        }
    [HarmonyPatch(typeof(ReloadableUtility), "FindPotentiallyReloadableGear")]
    public class FW_ReloadableUtility_FindPotentiallyReloadableGear : AmmoPatch
        {
        public static IEnumerable<Pair<CompReloadable, Thing>> Postfix(IEnumerable<Pair<CompReloadable, Thing>> __result, Pawn pawn, List<Thing> potentialAmmo)
            {
            if (__result.EnumerableNullOrEmpty())
                {
                CompAmmo comp = pawn.getWeapon()?.TryGetComp<CompAmmo>();

                if (comp?.AmmoDef != null)
                    {
                    for (int j = 0; j < potentialAmmo.Count; j++)
                        {
                        Thing thing = potentialAmmo[j];
                        if (thing.def == comp.AmmoDef)
                            {
                            yield return new Pair<CompReloadable, Thing>(comp, thing);
                            }
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
            var FindThingFor = AccessTools.MethodDelegate<Func<Pawn, ThingDef, Thing>>(AccessTools.Method(typeof(JobGiver_TakeForInventoryStock), "FindThingFor"), __instance);

            if (__result == null)
                {
                Thing weapon = pawn.getWeapon();
                if (weapon == null)
                    return null;
                var comp = weapon.TryGetComp<CompAmmo>();
                if (comp == null)
                    return null;
                if (pawn.inventory.Count(comp.AmmoDef) > 0 && (pawn.CurJobDef?.driverClass == typeof(JobDriver_Hunt) || pawn.CurJobDef?.driverClass == typeof(JobDriver_Kill)))
                    return null;
                Thing thing = FindThingFor(pawn, comp.AmmoDef);
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
    [HarmonyPatch(typeof(Pawn_InventoryStockTracker), "AnyThingsRequiredNow")]
    public class FW_Pawn_InventoryStockTracker_AnyThingsRequiredNow : AmmoPatch
        {
        public static bool Postfix(bool __result, Pawn_InventoryStockTracker __instance)
            {
            foreach (KeyValuePair<InventoryStockGroupDef, InventoryStockEntry> stockEntry in __instance.stockEntries)
                {
                if (!stockEntry.Key.HasModExtension<DefExtension_Ammo>() && __instance.pawn.inventory.Count(stockEntry.Value.thingDef) < stockEntry.Value.count)
                    return true;
                Thing gun = __instance.pawn.getWeapon();
                if (gun == null)
                    continue;
                var comp = gun.TryGetComp<CompAmmo>();
                if (comp != null && __instance.pawn.inventory.Count(comp.AmmoDef) < stockEntry.Value.count * comp.MaxCharges)
                    return true;
                }
            return __result;
            }
        }

    public class DefExtension_Ammo : DefModExtension
    {
    public DefExtension_Ammo() { }
    }

    public class PawnColumnWorker_CarryAmmo : PawnColumnWorker_Carry 
        {
        public static InventoryStockGroupDef group => DefOf.InventoryStockGroup_Ammo;
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
            {
            if (pawn.inventoryStock != null)
                {
                Widgets.Dropdown(new Rect(rect.x, rect.y + 2f, rect.width - 4f, rect.height - 4f), pawn,
                    (Pawn p) => p.inventoryStock.GetDesiredCountForGroup(group), (Pawn p) => this.GenerateCountButtons(p, group),
                    pawn.inventoryStock.GetDesiredCountForGroup(group).ToString(), null, null, null, null, paintable: true);
                }
            }
        private IEnumerable<Widgets.DropdownMenuElement<int>> GenerateCountButtons(Pawn pawn, InventoryStockGroupDef group)
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
            return this.GetValueToCompare(a).CompareTo(this.GetValueToCompare(b));
            }

        private int GetValueToCompare(Pawn pawn)
            {
            if (pawn.inventoryStock != null)
                {
                return pawn.inventoryStock.GetDesiredCountForGroup(group);
                }
            return int.MinValue;
            }
        public override int GetMinWidth(PawnTable table)
            {
            if (!def.label.NullOrEmpty())
                {
                Text.Font = DefaultHeaderFont;
                int result = Mathf.CeilToInt(Text.CalcSize(def.LabelCap).x);
                Text.Font = GameFont.Small;
                return result;
                }

            if (def.HeaderIcon != null)
                {
                return Mathf.CeilToInt(def.HeaderIconSize.x);
                }

            return 1;
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

            if (def.HeaderIcon != null)
                {
                return Mathf.CeilToInt(def.HeaderIconSize.y);
                }

            return 0;
            }
        }
    }
