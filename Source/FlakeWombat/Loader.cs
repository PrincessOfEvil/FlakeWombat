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

namespace FlakeWombat
    {
    public class Settings : ModSettings
        {
        public static bool accuracy = true;
        public static bool accuracyUnlimited = true;

        public static bool cooldown = true;

        public static bool armor = true;
        public static bool buildingArmor = true;

        public static bool ammo = true;
        public static bool ammoStatic = false;
        public static bool ammoSpawn = true;
        public override void ExposeData()
            {
            Scribe_Values.Look<bool>(ref accuracy, "FW_Accuracy", true);
            Scribe_Values.Look<bool>(ref accuracyUnlimited, "FW_AccuracyUnlimited", true);

            Scribe_Values.Look<bool>(ref cooldown, "FW_Cooldown", true);

            Scribe_Values.Look<bool>(ref armor, "FW_Armor", true);
            Scribe_Values.Look<bool>(ref buildingArmor, "FW_BuildingArmor", true);

            Scribe_Values.Look<bool>(ref ammo, "FW_AmmoSystem", true);
            Scribe_Values.Look<bool>(ref ammoStatic, "FW_AmmoStatic", false);
            Scribe_Values.Look<bool>(ref ammoSpawn, "FW_AmmoSpawn", true);
            }

        public bool[] listAll()
            {
            return this.GetType().GetFields().Where(f => f.FieldType == typeof(bool)).Select(f => (bool)f.GetValue(this)).ToArray();
            }
        }

    public class EarlyLoader : Mod
        {
        public Settings settings;

        private static readonly float length = 192f;
        private static readonly float height = 32f;
        public override string SettingsCategory()
            {
            return "FlakeWombat";
            }
        public override void DoSettingsWindowContents(Rect inRect)
            {
            float curY = height * 2;
            Widgets.CheckboxLabeled(new Rect(0, curY, length, height), "accuracy", ref Settings.accuracy);
            curY += height;
            Widgets.CheckboxLabeled(new Rect(0, curY, length, height), "accuracyUnlimited", ref Settings.accuracyUnlimited);
            curY += height;

            Widgets.CheckboxLabeled(new Rect(0, curY, length, height), "armor", ref Settings.armor);
            curY += height;
            Widgets.CheckboxLabeled(new Rect(0, curY, length, height), "buildingArmor", ref Settings.buildingArmor);
            curY += height;

            Widgets.CheckboxLabeled(new Rect(0, curY, length, height), "ammo", ref Settings.ammo);
            curY += height;
            Widgets.CheckboxLabeled(new Rect(0, curY, length, height), "ammoStatic", ref Settings.ammoStatic);
            curY += height;
            Widgets.CheckboxLabeled(new Rect(0, curY, length, height), "ammoSpawn", ref Settings.ammoSpawn);
            }
        public override void WriteSettings()
            {
            base.WriteSettings();

            Find.WindowStack.Add(new Dialog_MessageBox("FW_RequiredRestart".Translate(), buttonAAction: delegate
                {
                    GenCommandLine.Restart();
                    }, buttonBText: "CloseButton".Translate(), buttonADestructive: true));
            }
        public EarlyLoader(ModContentPack content) : base(content)
            {
            Extensions.initLabels();

            settings = this.GetSettings<Settings>();
            var harmony = new Harmony("princess.FlakeWombat");
            Log.Message("Flake Wombat has got it going on!");
            // harmony.PatchAll(Assembly.GetExecutingAssembly());

            new MultiClassProcessor(harmony, typeof(AlwaysPatch)).Patch();
            
            if (Settings.accuracy)
                new MultiClassProcessor(harmony, typeof(AccuracyPatch)).Patch();
            if (Settings.accuracyUnlimited)
                new MultiClassProcessor(harmony, typeof(AccuracyUnlimitedPatch)).Patch();

            if (Settings.cooldown)
                new MultiClassProcessor(harmony, typeof(CooldownPatch)).Patch();

            if (Settings.armor)
                new MultiClassProcessor(harmony, typeof(ArmorPatch)).Patch();
            if (Settings.buildingArmor)
                new MultiClassProcessor(harmony, typeof(ArmorBuildingPatch)).Patch();

            if (Settings.ammo)
                {
                new MultiClassProcessor(harmony, typeof(AmmoPatch)).Patch();
                harmony.Patch(AccessTools.FirstMethod(typeof(Pawn_EquipmentTracker), method => method.Name.Contains("YieldGizmos")), postfix: new HarmonyMethod(typeof(FW_Pawn_EquipmentTracker_YieldGizmos), "Postfix"));
                }
            if (Settings.ammoStatic)
                new MultiClassProcessor(harmony, typeof(AmmoStaticPatch)).Patch();
            if (Settings.ammoSpawn)
                new MultiClassProcessor(harmony, typeof(AmmoSpawnPatch)).Patch();

            ModCompat.Patch(harmony);

            /*
            var pf = new HarmonyMethod(typeof(Toil_Reload), "Postfix");
            Type[] driversToReload = new Type[] { 
                typeof(JobDriver_Hunt),
                typeof(JobDriver_Kill),
                typeof(JobDriver_AttackStatic)
                };
            foreach (var driver in driversToReload)
                harmony.Patch(AccessTools.Method(driver, "MakeNewToils"), postfix: pf);

            var PF = new HarmonyMethod(typeof(FW_D), "PF");
            foreach (var method in AccessTools.AllTypes().SelectMany(t => t.GetMethods().Where(m => m.ReturnType == typeof(Toil) && m.IsDeclaredMember() && !m.IsAbstract)))
                harmony.Patch(method, postfix:PF);*/
            }
        }

    // Public to allow extensions, if anyone cares enough to add one.
    public abstract class AlwaysPatch { }
    public abstract class AccuracyPatch { }
    public abstract class AccuracyUnlimitedPatch { }
    public abstract class CooldownPatch { }
    public abstract class ArmorPatch { }
    public abstract class ArmorBuildingPatch { }
    public abstract class AmmoPatch { }
    public abstract class AmmoStaticPatch { }
    public abstract class AmmoSpawnPatch { }

    static class FW_D
        {
        public static void PF(MethodBase __originalMethod)
            {
            Log.Message(__originalMethod.DeclaringType.Name + "::" + __originalMethod.Name);
            }
        }


    public struct MultiClassProcessor
        {
        Harmony harmony;
        Type type;
        public MultiClassProcessor(Harmony harmony, Type type)
            {
            this.harmony = harmony;
            this.type = type;
            }

        public void Patch()
            {
            type.AllSubclassesNonAbstract().ForEach(CreateClassProcessor);
            }

        private void CreateClassProcessor(Type type)
            {
            harmony.CreateClassProcessor(type).Patch();
            }
        }
    }
