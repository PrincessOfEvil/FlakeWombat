using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace FlakeWombat
    {
    [RimWorld.DefOf]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public static class DefOf 
        {
        [DefAlias("FW_Ammo")]
        public static StatCategoryDef StatCategory_Ammo;
        [DefAlias("FW_Ammo")]
        public static ThingCategoryDef ThingCategory_Ammo;
        [DefAlias("FW_Ammo")]
        public static InventoryStockGroupDef InventoryStockGroup_Ammo;

        [DefAlias("Heat")]
        public static DamageArmorCategoryDef DamageArmorCategory_Heat;

        public static AmmoSubTypeDef basic;
        }

    public static class Util
        {
        // ReSharper disable once IdentifierTypo
        private static int iiiii = 0;
        public static void M(bool reset = false, string mod = "")
            {
            if (reset)
                iiiii = 0;
            Log.Message("ctr:" + iiiii++ + ":" + mod);
            }
        }
    public static class Extensions
        {
        public static string log(this string s) { Log.Message(s); return s; }
        public static Thing getWeapon(this Pawn pawn)
            {
            Thing ret = pawn.equipment?.Primary;
            // partial simple sidearms support
            if (ret == null || !ret.def.isAmmoWeapon())
                ret = pawn.inventory.innerContainer.FirstOrDefault(thing => thing.def.isAmmoWeapon());
            return ret;
            }
        public static StatDrawEntry withCategory(this StatDrawEntry entry, StatCategoryDef category)
            {
            entry.category = category;
            return entry;
            }
        public static bool isHeavyWeapon(this ThingDef def) => def.Verbs[0].defaultProjectile.projectile.GetDamageAmount(def, null) >= Ammo.heavyCutoff;
        public static bool isSuperheavyWeapon(this ThingDef def) => def.Verbs[0].defaultProjectile.projectile.GetDamageAmount(def, null) >= Ammo.superHeavyCutoff;

        public static int ammoPerShot(this ThingDef def)
            {
            int damage = def.Verbs[0].defaultProjectile.projectile.GetDamageAmount(def, null);
            return
                Math.Max(damage == DamageDefOf.Extinguish.defaultDamage? 1 :
                def.isEnergy() ? damage / Ammo.damagePerCell :
                def.isExplosive() ? damage / Ammo.damagePerNade :
                def.isShotgun() ? damage / Ammo.damagePerShell :
                1, 1);
            }

        public static int shotsPerMag(this ThingDef def) => def.Verbs[0].burstShotCount *
            (def.isSuperheavyWeapon() ? Ammo.superHeavyAmmo
            : def.isHeavyWeapon() ? def.techLevel < TechLevel.Industrial ? Ammo.neolithicHeavyAmmo : Ammo.heavyAmmo
            : def.label.ToLower().Contains("revolver") ? Ammo.revolverAmmo
            : def.techLevel < TechLevel.Industrial? Ammo.neolithicAmmo : Ammo.ammo);

        public static bool isAmmoWeapon(this ThingDef t) =>
                                        t.IsRangedWeapon &&
                                        t.weaponTags != null &&
                                        !(t.weaponTags.Contains("TurretGun") || t.weaponTags.Contains("Artillery")) &&
                                        t.Verbs != null &&
                                        t.Verbs.Count >= 1 &&
                                        t.Verbs[0].verbClass != null &&
                                        t.Verbs[0].verbClass != typeof(Verb_ShootOneUse) &&
                                        t.Verbs[0].consumeFuelPerShot <= 0f &&
                                        t.Verbs[0].defaultProjectile?.projectile.damageDef != null;

        public static float ammoPerSecond(this ThingDef thing)
            {
            var verb = thing.Verbs[0];
            // Fun fact: every mod i've checked forgets that the gun only takes two cooldowns per three bullets.
            return verb.burstShotCount / (verb.warmupTime + thing.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown) + (verb.burstShotCount - 1) * verb.ticksBetweenBurstShots / (float)GenTicks.TicksPerRealSecond);
            }

        private static Dictionary<ThingDef, string> labels;
        public static void initLabels()
            {
            labels = new Dictionary<ThingDef, string>();
            foreach (ThingDef def in from t in DefDatabase<ThingDef>.AllDefsListForReading
                                     where t.isAmmoWeapon()
                                     select t)
                labels.Add(def, def.label);
            }
        public static string baseLabel(this ThingDef thing)
            {
            return labels.TryGetValue(thing, thing.label);
            }

        public static string ToStringSafeDef(this object obj)
            {
            if (obj is Def def)
                return def.label ?? def.defName;
            else return obj.ToStringSafe();
            }
        }
    }
