using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace FlakeWombat
    {
    [Flags]
    public enum AmmoType : byte
        {
        Energy = 0,
        Explosive = 1,
        Shotgun = 2,
        Pistol = 4,
        Rifle = 8,
        All = byte.MaxValue
        }

    public static class AmmoUtil
        {
        public static AmmoSubTypeDef tryGetAmmo(this Thing thing)
            {
            var comp = thing.TryGetComp<CompAmmo>();
            return comp?.currentAmmo;
            }

        public static ThingDef ammoDef(this ThingDef def, AmmoSubTypeDef ammo)
            {
            if (def.techLevel <= TechLevel.Medieval && def.isEnergy() && (ammo?.isBasic ?? true)) return ThingDefOf.Chemfuel;
            return DefDatabase<ThingDef>.GetNamedSilentFail("FW_Round" + def.ammoTechLevel() + def.ammoType() + (ammo?.isBasic ?? true ? "" : ammo.defName));
            }

        public static ThingDef ammoBaseDef(this ThingDef def)
            {
            return def.ammoDef(null);
            }

        public static AmmoType ammoType(this ThingDef def) =>
            def.isEnergy() ? AmmoType.Energy :
            def.isExplosive() ? AmmoType.Explosive : 
            def.isShotgun() ? AmmoType.Shotgun : 
            def.isPistol() ? AmmoType.Pistol : 
            AmmoType.Rifle;
        public static TechLevel ammoTechLevel(this ThingDef def)
            {
            if (def.techLevel >= TechLevel.Spacer) return TechLevel.Spacer;

            if (def.isShotgun()) return TechLevel.Industrial;

            if (def.techLevel <= TechLevel.Medieval) return TechLevel.Neolithic;

            return TechLevel.Industrial;
            }

        public static bool isEnergy(this ThingDef thing)
            {
            if (!thing.Verbs[0].defaultProjectile.projectile.damageDef.harmsHealth)
                return true;
            if (thing.Verbs[0].defaultProjectile.projectile.damageDef.armorCategory == DefOf.DamageArmorCategory_Heat)
                return true;

            return false;
            }
        public static bool isExplosive(this ThingDef thing)
            {
            if (thing.Verbs[0].defaultProjectile.projectile.damageDef.isExplosive)
                return true;

            return false;
            }

        public static bool isShotgun(this ThingDef thing)
            {
            if (thing.baseLabel().ToLower().Contains("shotgun"))
                return true;

            if (thing.Verbs[0].defaultProjectile.defName.ToLower().Contains("shotgun"))
                return true;

            return false;
            }
        public static bool isPistol(this ThingDef thing)
            {
            if (thing.isEnergy() || thing.isExplosive() || thing.isShotgun())
                return false;
            string label = thing.baseLabel().ToLower();
            // neolithic
            // Bows are neolithic pistols (the rifle ammo type is javelin)
            if (label.Contains("bow"))
                return true;

            // industrial
            if (label.Contains("pistol") || label.Contains("revolver") || label.Contains("handgun") || label.Contains("smg"))
                return true;
            if (label.Contains("rifle"))
                return false;

            // Come on. Minigun definitely fires pistol rounds.
            if (thing.ammoPerSecond() > Ammo.ammoPerSecondCutoff)
                return true;

            if (thing.Verbs[0].defaultProjectile.projectile.GetDamageAmount(thing, null) <= Ammo.mercyCutoff)
                return true;

            return false;
            }

        public static bool isRifle(this ThingDef thing)
            {
            return !(thing.isEnergy() || thing.isExplosive() || thing.isShotgun() || thing.isPistol());
            }
        }
    }
