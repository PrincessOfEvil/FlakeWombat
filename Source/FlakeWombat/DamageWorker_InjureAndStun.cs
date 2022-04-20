using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace FlakeWombat
    {
    public class DamageWorker_InjureAndStun : DamageWorker_AddInjury
		{
		public override DamageResult Apply(DamageInfo dinfo, Thing victim)
			{
			DamageResult damageResult = base.Apply(dinfo, victim);
			damageResult.stunned = true;
			return damageResult;
			}
		}
    }
