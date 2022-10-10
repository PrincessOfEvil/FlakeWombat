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

    [HarmonyPatch(typeof(Thing), nameof(Thing.SpecialDisplayStats))]
    public class FlakeWombat_Thing_SpecialDisplayStats : ArmorBuildingPatch
        {
        public static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> input, Thing __instance)
            {
            foreach (StatDrawEntry stat in input) yield return stat;

            if (__instance.def.IsApparel || __instance.def.IsStuff || StatDefOf.ArmorRating_Sharp.Worker.ShouldShowFor(StatRequest.For(__instance))
                || StatDefOf.ArmorRating_Blunt.Worker.ShouldShowFor(StatRequest.For(__instance))
                || StatDefOf.ArmorRating_Heat.Worker.ShouldShowFor(StatRequest.For(__instance))) yield break;

            ThingDef stuff = __instance.Stuff ?? __instance.def.CostList?.MaxBy(tdcc => tdcc.count).thingDef;

            if (stuff == null) yield break;
            yield return new StatDrawEntry(StatCategoryDefOf.Basics, StatDefOf.ArmorRating_Sharp, FlakeWombat_Thing_PreApplyDamage.ARMOR_MULT.Evaluate(__instance.def.GetStatValueAbstract(StatDefOf.MaxHitPoints)) * stuff.GetStatValueAbstract(StatDefOf.StuffPower_Armor_Sharp), StatRequest.ForEmpty());
            yield return new StatDrawEntry(StatCategoryDefOf.Basics, StatDefOf.ArmorRating_Blunt, FlakeWombat_Thing_PreApplyDamage.ARMOR_MULT.Evaluate(__instance.def.GetStatValueAbstract(StatDefOf.MaxHitPoints)) * stuff.GetStatValueAbstract(StatDefOf.StuffPower_Armor_Blunt), StatRequest.ForEmpty());
            yield return new StatDrawEntry(StatCategoryDefOf.Basics, StatDefOf.ArmorRating_Heat, FlakeWombat_Thing_PreApplyDamage.ARMOR_MULT.Evaluate(__instance.def.GetStatValueAbstract(StatDefOf.MaxHitPoints)) * stuff.GetStatValueAbstract(StatDefOf.StuffPower_Armor_Heat), StatRequest.ForEmpty());
            }
        }

    [HarmonyPatch(typeof(Thing), nameof(Thing.PreApplyDamage))]
    public class FlakeWombat_Thing_PreApplyDamage : ArmorBuildingPatch
        {
        public static readonly SimpleCurve ARMOR_MULT = new SimpleCurve(new[] { new CurvePoint(0, 0), new CurvePoint(250, 1) });

        private delegate void ApplyArmorDelegate(ref float damAmount, float armorPenetration, float armorRating, Thing armorThing, ref DamageDef damageDef, Pawn pawn, out bool metalArmor);
        private static readonly ApplyArmorDelegate APPLY_ARMOR = AccessTools.MethodDelegate<ApplyArmorDelegate>(AccessTools.Method(typeof(ArmorUtility), "ApplyArmor"));

        public static void Postfix(ref DamageInfo dinfo, ref bool absorbed, Thing __instance)
            {
            if (dinfo.Def.armorCategory == null) return;

            ThingDef stuff = __instance.Stuff ?? __instance.def.CostList?.MaxBy(tdcc => tdcc.count).thingDef;
            if (stuff == null) return;

            float damageAmount = dinfo.Amount;
            DamageDef damageDef = dinfo.Def;
            EffecterDef effecterDef;
            APPLY_ARMOR(ref damageAmount, dinfo.ArmorPenetrationInt,
                                                         FlakeWombat_Thing_PreApplyDamage.ARMOR_MULT.Evaluate(__instance.def.GetStatValueAbstract(StatDefOf.MaxHitPoints)) * stuff.GetStatValueAbstract(damageDef.armorCategory.armorRatingStat.GetStatPart<StatPart_Stuff>().stuffPowerStat), null,
                                                         ref damageDef, /* Nobody cares. */ Find.WorldPawns.AllPawnsAlive[0], out bool _);
            if (damageAmount < 0.001f)
                {
                absorbed = true;
                if (stuff.IsMetal && dinfo.Def.canUseDeflectMetalEffect)
                    effecterDef = (dinfo.Def != DamageDefOf.Bullet) ? EffecterDefOf.Deflect_Metal : EffecterDefOf.Deflect_Metal_Bullet;
                else
                    effecterDef = (dinfo.Def != DamageDefOf.Bullet) ? EffecterDefOf.Deflect_General : EffecterDefOf.Deflect_General_Bullet;
                effecterDef.Spawn().Trigger(__instance, dinfo.Instigator ?? __instance);
                return;
                }
            if (dinfo.Amount > damageAmount)
                {
                dinfo.SetAmount(damageAmount);
                dinfo.Def = damageDef;
                effecterDef = stuff.IsMetal ? EffecterDefOf.DamageDiminished_Metal : EffecterDefOf.DamageDiminished_General;
                effecterDef.Spawn().Trigger(__instance, dinfo.Instigator ?? __instance);
                return;
                }
            }
        }
    }
