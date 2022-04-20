using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace FlakeWombat
    {
    public class AmmoSubTypeDef : Def
        {
        // Wild card value
        public TechLevel level = TechLevel.Undefined;
        public AmmoType type = AmmoType.All;

        public float damageMult = 1;
        public float? armorPenMult;

        // Requires a projectile with 1 damage
        public ThingDef projectileOverride;

        // Trinary bool: true, false or null
        public bool? preventFriendlyFire = null;
        public ProjectileHitFlags? hitFlags = null;

        public bool isBasic => defName == "basic";
        }

    public class CompProjArmorPierce : ThingComp
        {
        public float armorPenMult;
        }


    [HarmonyPatch(typeof(Verb_LaunchProjectile), "Projectile", MethodType.Getter)]
    public class FW_Verb_LaunchProjectile_Projectile : AmmoPatch
        {
        public static ThingDef Postfix(ThingDef ret, Verb_LaunchProjectile __instance)
            {
            if (__instance.EquipmentSource?.tryGetAmmo()?.projectileOverride != null)
                return __instance.EquipmentSource.tryGetAmmo().projectileOverride;
            return ret;
            }
        }
    [HarmonyPatch(typeof(Projectile), "Launch", new[] { typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef)  })]
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
            if (ammo.armorPenMult != null)
                {
                if (___comps == null) ___comps = new();
                ___comps.Add(new CompProjArmorPierce() { armorPenMult = (float)ammo.armorPenMult });
                }
            }
        }
    [HarmonyPatch(typeof(Projectile), "ArmorPenetration", MethodType.Getter)]
    public class FW_Projectile_ArmorPenetration : AmmoPatch
        {
        public static float Postfix(float pen, Projectile __instance)
            {
            return pen * __instance.TryGetComp<CompProjArmorPierce>()?.armorPenMult ?? 1;
            }
        }
    [HarmonyPatch(typeof(CompReloadable), "AmmoDef", MethodType.Getter)]
    public class FW_CompReloadable_AmmoDef : AmmoPatch
        {
        public static ThingDef Postfix(ThingDef ret, CompReloadable __instance)
            {
            if (__instance is CompAmmo ammo)
                return ammo.realAmmo;
            return ret;
            }
        }
    }