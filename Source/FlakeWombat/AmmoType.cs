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

        private static readonly Dictionary<ThingDef, AmmoSubTypeDef> DICT = new();

        public static bool isAmmo(this ThingDef thing)
            {
            return thing.tryGetAmmoFromAmmo() != null;
            }

        public static AmmoSubTypeDef tryGetAmmoFromAmmo(this ThingDef thing)
            {
            if (DICT.TryGetValue(thing, out AmmoSubTypeDef output)) return output;

            if (thing == ThingDefOf.Chemfuel)
                {
                output = DefDatabase<AmmoSubTypeDef>.GetNamed("basic");
                DICT.Add(thing, output);
                return output;
                }

            string name = thing.defName;

            if (Prefs.DevMode) Log.Message(name);

            name = name.Replace("FW_Round", "");

            TechLevel level = TechLevel.Undefined;
            foreach (TechLevel tLev in Enum.GetValues(typeof(TechLevel)))
                if (name.Contains(tLev.ToString()))
                    {
                    level = tLev;
                    name = name.Replace(tLev.ToString(), "");
                    break;
                    }

            AmmoType type = AmmoType.All;
            foreach (AmmoType aType in Enum.GetValues(typeof(AmmoType)))
                if (name.Contains(aType.ToString()))
                    {
                    type = aType;
                    name = name.Replace(aType.ToString(), "");
                    break;
                    }

            if (Prefs.DevMode)
                { 
                Log.Message(level.ToStringSafeDef());
                Log.Message(type.ToStringSafeDef());
                Log.Message(name);
                }

            output = DefDatabase<AmmoSubTypeDef>.AllDefsListForReading.First(def => (def.level == TechLevel.Undefined || def.level == level) &&
                                                                                    (def.type == AmmoType.All || (def.type & type) == type) &&
                                                                                    (name.NullOrEmpty() && def.IsBasic || def.defName == name));
            DICT.Add(thing, output);
            return output;
            }

        public static ThingDef ammoDef(this ThingDef def, AmmoSubTypeDef ammo)
            {
            if (def.techLevel <= TechLevel.Medieval && def.isEnergy() && (ammo?.IsBasic ?? true)) return ThingDefOf.Chemfuel;
            return DefDatabase<ThingDef>.GetNamed("FW_Round" + def.ammoTechLevel() + def.ammoType() + (ammo?.IsBasic ?? true ? "" : ammo.defName));
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
            string label = thing.baseLabel().ToLower();
            if (label.Contains("las") || label.Contains("melta"))
                return true;

            return false;
            }
        public static bool isExplosive(this ThingDef thing)
            {
            return thing.Verbs[0].defaultProjectile.projectile.damageDef.isExplosive;
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
