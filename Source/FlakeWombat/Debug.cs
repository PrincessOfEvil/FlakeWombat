using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
// ReSharper disable UnusedMember.Local

namespace FlakeWombat
    {
    public static class Debug
		{
		[DebugAction("Spawning", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SpawnAllWeapons()
			{
			foreach (ThingDef item in from def in DefDatabase<ThingDef>.AllDefs
									  where def.isAmmoWeapon()
									  select def)
				{
				ThingDef stuff = GenStuff.RandomStuffFor(item);
				Thing thing = ThingMaker.MakeThing(item, stuff);

				thing.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);
				if (thing.def.Minifiable)
					{
					thing = thing.MakeMinified();
					}
				GenPlace.TryPlaceThing(thing, UI.MouseCell(), Find.CurrentMap, ThingPlaceMode.Near);
				}
			}

		[DebugAction("Spawning", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SpawnAllWeaponsLegendary()
			{
			foreach (ThingDef item in from def in DefDatabase<ThingDef>.AllDefs
										where def.isAmmoWeapon()
										select def)
				{
				ThingDef stuff = GenStuff.RandomStuffFor(item);
				Thing thing = ThingMaker.MakeThing(item, stuff);

				thing.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Colony);
				if (thing.def.Minifiable)
					{
					thing = thing.MakeMinified();
					}
				GenPlace.TryPlaceThing(thing, UI.MouseCell(), Find.CurrentMap, ThingPlaceMode.Near);
				}
			}
		[DebugAction("Spawning" , actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SpawnAllAmmo()
			{
			foreach (ThingDef item in from def in DefDatabase<ThingDef>.AllDefs
										where def.thingCategories?.Contains(DefOf.ThingCategory_Ammo) ?? false
									  select def)
				{
				DebugThingPlaceHelper.DebugSpawn(item, UI.MouseCell());
				}
			}
		}
    }
