using RimWorld;
using UnityEngine;
using Verse;
using System.Linq; // 引入Linq以使用OrderBy等高级查询功能

namespace The_Memories_Of_Phantasm
{
    // ==================== 1. 定义我们的XML属性容器 ====================
    public class GoheiHomingProperties : DefModExtension
    {
        public int homingStartDelayTicks = 0;
        public float turnRateDegreesPerTick = 5f;
        public float retargetingRadius = 0f;
    }

    // ==================== 2. 我们的主投射物逻辑 ====================
    public class Projectile_OfudaHoming : Projectile
    {
        private GoheiHomingProperties props;
        private int ticksAlive = 0;
        private const int MaxLifetimeTicks = 300;
        private const int UpdateFrequency = 2;

        public GoheiHomingProperties Props
        {
            get
            {
                if (props == null)
                {
                    props = this.def.GetModExtension<GoheiHomingProperties>() ?? new GoheiHomingProperties();
                }
                return props;
            }
        }

        protected bool IsTargetValid
        {
            get
            {
                if (this.intendedTarget.Thing == null || this.intendedTarget.Thing.Destroyed)
                    return false;
                if (this.intendedTarget.Thing is Pawn pawn)
                {
                    return !pawn.Dead && pawn.Spawned;
                }
                return this.intendedTarget.Thing.Spawned;
            }
        }

        public Projectile_OfudaHoming() { }

        protected override void Tick()
        {
            // --- 1. 超时自毁 ---
            this.ticksAlive++;
            if (this.ticksAlive > MaxLifetimeTicks)
            {
                this.Destroy(DestroyMode.Vanish);
                return;
            }

            // --- 2. 目标管理与重定向 ---
            if (!IsTargetValid)
            {
                if (!FindNewTarget())
                {
                    base.Tick();
                    return;
                }
            }

            // --- 3. 发射后硬直（制导延迟） ---
            if (ticksAlive < Props.homingStartDelayTicks)
            {
                base.Tick();
                return;
            }

            // --- 4. 手动命中判定 ---
            if (this.intendedTarget.Thing != null)
            {
                float distanceToTarget = (this.intendedTarget.Thing.DrawPos - this.ExactPosition).magnitude;
                if (distanceToTarget < 0.5f)
                {
                    DoImpact(this.intendedTarget.Thing);
                    return;
                }
            }

            // --- 5. 追踪逻辑 ---
            if (Find.TickManager.TicksGame % UpdateFrequency == 0 && this.intendedTarget.Thing != null)
            {

                Vector3 currentDirection = (this.destination - this.origin).normalized;
                // 直接计算从弹药到目标的向量
                Vector3 targetDirection = (this.intendedTarget.Thing.DrawPos - this.ExactPosition).normalized;

                float maxRadiansDelta = Props.turnRateDegreesPerTick * Mathf.Deg2Rad * UpdateFrequency;

                // 直接转向目标方向，不再需要负号
                Vector3 newDirection = Vector3.RotateTowards(currentDirection, targetDirection, maxRadiansDelta, 0f);

                this.origin = this.ExactPosition;
                this.destination = this.ExactPosition + newDirection * 100f;
                this.ticksToImpact = Mathf.CeilToInt(this.StartingTicksToImpact);
            }

            base.Tick();
        }

        protected virtual void DoImpact(Thing hitThing)
        {
            if (this.def.projectile.explosionRadius > 0f)
            {
                GenExplosion.DoExplosion(
                    center: this.Position, map: this.Map, radius: this.def.projectile.explosionRadius,
                    damType: this.def.projectile.damageDef, instigator: this.launcher,
                    // 【已修复】使用 projectile 实例的 DamageAmount 和 ArmorPenetration 属性
                    // 这两个值在发射时已经由武器计算好，包含了品质等所有加成
                    damAmount: this.DamageAmount,
                    armorPenetration: this.ArmorPenetration,
                    weapon: this.equipmentDef, projectile: this.def
                );
            }
            else if (hitThing != null)
            {
                // 【已修复】同样，使用 projectile 实例的属性来创建 DamageInfo
                DamageInfo dinfo = new DamageInfo(
                    this.def.projectile.damageDef,
                    this.DamageAmount,
                    this.ArmorPenetration,
                    -1f, this.launcher, null, this.equipmentDef);
                hitThing.TakeDamage(dinfo);
            }
            this.Destroy(DestroyMode.Vanish);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            DoImpact(hitThing);
        }

        private bool FindNewTarget()
        {
            if (Props.retargetingRadius <= 0f) return false;

            // 【已修正】将函数名改回兼容性更好的旧版本
            var potentialTargets = GenRadial.RadialDistinctThingsAround(this.Position, this.Map, Props.retargetingRadius, true);
            Thing newTarget = potentialTargets
               .OfType<Pawn>()
               .Where(p => p != this.launcher && !p.Dead && !p.Downed && p.HostileTo(this.launcher.Faction))
               .OrderBy(p => p.Position.DistanceTo(this.Position))
               .FirstOrDefault();

            if (newTarget != null)
            {
                this.intendedTarget = new LocalTargetInfo(newTarget);
                return true;
            }
            return false;
        }
    }
}