using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace The_Memories_Of_Phantasm
{
    public class Projectile_IllusionLaser : Projectile
    {
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            // The base.Launch() method is called first. It's crucial because it calculates and sets 
            // properties like this.DamageAmount and this.ArmorPenetration based on the launcher's stats (including quality).
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

            if (!usedTarget.IsValid || this.Map == null)
            {
                this.Destroy(DestroyMode.Vanish);
                return;
            }

            var comp = (equipment as ThingWithComps)?.GetComp<Comp_MultiVerbSwitcher>();
            WeaponMode currentMode = comp?.GetCurrentMode();
            if (currentMode == null)
            {
                Log.ErrorOnce("[Touhou Armory] IllusionLaser fired but couldn't get current WeaponMode.", this.thingIDNumber);
                this.Destroy(DestroyMode.Vanish);
                return;
            }

            // --- Step 1: Determine the Visual End Point ---
            Vector3 visualStartPos = launcher.DrawPos;
            Vector3 visualDirection = (usedTarget.CenterVector3 - visualStartPos).normalized;
            Vector3 finalEndPos = visualStartPos + visualDirection * 200f;

            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(visualStartPos.ToIntVec3(), finalEndPos.ToIntVec3()))
            {
                if (!cell.InBounds(this.Map))
                {
                    finalEndPos = cell.ToVector3Shifted();
                    break;
                }
                if (this.Map.thingGrid.ThingsListAt(cell).Any(t => t.def.Fillage == FillCategory.Full))
                {
                    finalEndPos = cell.ToVector3Shifted();
                    break;
                }
            }

            // --- Step 2: Calculate Damage Along the Determined Path ---
            HashSet<Thing> targetsHit = new HashSet<Thing>();

            bool preventFriendly = preventFriendlyFire;

            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(visualStartPos.ToIntVec3(), finalEndPos.ToIntVec3()))
            {
                if (!cell.InBounds(this.Map)) break;

                List<Thing> thingsInCell = this.Map.thingGrid.ThingsListAt(cell);
                for (int i = 0; i < thingsInCell.Count; i++)
                {
                    Thing thing = thingsInCell[i];
                    if (thing != null && thing != launcher)
                    {
                        if (preventFriendly && thing.Faction == launcher.Faction && thing is Pawn)
                        {
                            continue;
                        }
                        if (thing.def.category == ThingCategory.Pawn || thing.def.category == ThingCategory.Building)
                        {
                            targetsHit.Add(thing);
                        }
                    }
                }
            }

            foreach (var target in targetsHit)
            {
                // [BUG FIX]
                // Replaced this.def.projectile.GetDamageAmount(launcher) with this.DamageAmount
                // and this.def.projectile.GetArmorPenetration(launcher) with this.ArmorPenetration.
                // The `base.Launch()` call at the start of the method has already calculated the correct values,
                // taking weapon quality and other stats into account, and stored them in these properties.
                // Using them directly ensures the damage scales correctly.
                var dinfo = new DamageInfo(this.def.projectile.damageDef, this.DamageAmount, this.ArmorPenetration, -1f, launcher, null, equipment?.def);
                target.TakeDamage(dinfo);
            }

            // --- Step 3: Register the Beam for Rendering ---
            if (currentMode.beamMoteDef != null)
            {
                BeamDraw beamDraw = this.Map.GetComponent<BeamDraw>();
                if (beamDraw != null)
                {
                    float beamWidth = currentMode.beamWidth.GetValueOrDefault(1.0f);
                    int beamDurationTicks = currentMode.beamDurationTicks.GetValueOrDefault(15);

                    Beam beam = new Beam(
                        visualStartPos,
                        finalEndPos,
                        beamWidth,
                        beamDurationTicks,
                        currentMode.beamMoteDef.graphicData.Graphic.MatSingle
                    );
                    beamDraw.RegisterBeam(beam);
                }
            }

            this.Destroy(DestroyMode.Vanish);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false) { }
        protected override void Tick() { }
    }
}
