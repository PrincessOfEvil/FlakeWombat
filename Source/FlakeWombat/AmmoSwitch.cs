using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace FlakeWombat
    {
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
    [SuppressMessage("ReSharper", "ConvertToConstant.Global")]
    [SuppressMessage("ReSharper", "UnassignedField.Global")]
    public class AmmoSubTypeDef : Def
        {
        // Wild card value
        public TechLevel level = TechLevel.Undefined;
        public AmmoType type = AmmoType.All;

        public float damageMult = 1f;
        public float? armorPenMult;

        // Requires a projectile with 1 damage
        public ThingDef projectileOverride;

        // Trinary bool: true, false or null
        public bool? preventFriendlyFire = null;
        public ProjectileHitFlags? hitFlags = null;

        public bool IsBasic => defName == "basic";

        public virtual IEnumerable<StatDrawEntry> SpecialDisplayStats()
            {
            //Chosen by a fair dice roll, guaranteed to be random.
            var lvl = level != TechLevel.Undefined ? level.ToStringSafeDef() : "All";
            yield return statFor(lvl,                       "level",                3800);
            yield return statFor(type,                      "type",                 3800 - 1);

            if (!Mathf.Approximately(damageMult, 1f))
                yield return statFor(damageMult,            "damageMult",           3800 - 2);
            if (armorPenMult != null)
                yield return statFor(armorPenMult,          "armorPenMult",         3800 - 3);
            if (projectileOverride != null)
                yield return statFor(projectileOverride,    "projectileOverride",   3800 - 4);
            if (preventFriendlyFire != null)
                yield return statFor(preventFriendlyFire,   "preventFriendlyFire",  3800 - 5);
            if (hitFlags != null)
                yield return statFor(hitFlags,              "hitFlags",             3800 - 6);
            }

        protected static StatDrawEntry statFor(object obj, string name, int id) 
            {
            return new StatDrawEntry(StatCategoryDefOf.Weapon_Ranged, ("FW." + name).Translate(), obj.ToStringSafeDef().CapitalizeFirst(), ("FW." + name + ".desc").Translate(), id);
            }
        }

    public class CompAmmoData : ThingComp
        {
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
            {
            var bas = base.SpecialDisplayStats();
            if (bas != null)
                foreach (StatDrawEntry stat in bas)
                    yield return stat;

            AmmoSubTypeDef ammoDef = parent.def.tryGetAmmoFromAmmo();
            if (ammoDef == null) yield break;
            bas = ammoDef.SpecialDisplayStats();
            if (bas != null)
                foreach (StatDrawEntry stat in bas)
                    yield return stat;
            }
        }

    public class CompProjArmorPierce : ThingComp
        {
        public float armorPenMult;
        }


    [HarmonyPatch(typeof(Verb_LaunchProjectile), nameof(Verb_LaunchProjectile.Projectile), MethodType.Getter)]
    public class FW_Verb_LaunchProjectile_Projectile : AmmoPatch
        {
        public static ThingDef Postfix(ThingDef ret, Verb_LaunchProjectile __instance)
            {
            return __instance.EquipmentSource?.tryGetAmmo()?.projectileOverride != null ? __instance.EquipmentSource.tryGetAmmo().projectileOverride : ret;
            }
        }
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch), typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef))]
    public class FW_Projectile_Launch : AmmoPatch
        {
        public static void Postfix(Thing equipment, ref float ___weaponDamageMultiplier, ref List<ThingComp> ___comps, ref bool ___preventFriendlyFire, ref ProjectileHitFlags ___desiredHitFlags)
            {
            AmmoSubTypeDef ammo = equipment.tryGetAmmo();

            if (ammo == null) return;

            ___weaponDamageMultiplier *= ammo.damageMult;
            ___preventFriendlyFire = ammo.preventFriendlyFire ?? ___preventFriendlyFire;
            ___desiredHitFlags = ammo.hitFlags ?? ___desiredHitFlags;

            if (ammo.projectileOverride != null)
                {
                ___weaponDamageMultiplier *= equipment.def.Verbs[0].defaultProjectile.projectile.GetDamageAmount(equipment.def, null);
                }

            if (ammo.armorPenMult == null) return;
            ___comps ??= new List<ThingComp>();
            ___comps.Add(new CompProjArmorPierce() { armorPenMult = (float)ammo.armorPenMult });
            }
        }
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.ArmorPenetration), MethodType.Getter)]
    public class FW_Projectile_ArmorPenetration : AmmoPatch
        {
        public static float Postfix(float pen, Projectile __instance)
            {
            return pen * __instance.TryGetComp<CompProjArmorPierce>()?.armorPenMult ?? 1;
            }
        }
    [HarmonyPatch(typeof(CompReloadable), nameof(CompReloadable.AmmoDef), MethodType.Getter)]
    public class FW_CompReloadable_AmmoDef : AmmoPatch
        {
        public static ThingDef Postfix(ThingDef ret, CompReloadable __instance)
            {
            if (__instance is CompAmmo ammo)
                return ammo.RealAmmo;
            return ret;
            }
        }
    }