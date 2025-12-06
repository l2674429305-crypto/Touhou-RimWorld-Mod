using RimWorld;
using UnityEngine;
using Verse;

namespace The_Memories_Of_Phantasm
{
    // ==================== 1. 定义XML属性容器 ====================
    // 这个类用于在XML中配置集束炸弹的参数
    public class THF_ClusterBombProperties : DefModExtension
    {
        public ThingDef submunition;        // 子弹药的ThingDef
        public int submunitionCount = 8;     // 散射出的子弹药数量
        public float scatterRadius = 5f;     // 子弹药的散布半径
    }

    // ==================== 2. 集束炸弹母弹的主逻辑 ====================
    public class THF_Projectile_Cluster : Projectile
    {
        // 方便地获取XML中定义的属性
        private THF_ClusterBombProperties Props => this.def.GetModExtension<THF_ClusterBombProperties>();

        // 【核心修正】: 添加一个私有字段，用于在发射时存储武器实例
        private Thing weaponInt;

        /// <summary>
        /// 重写原版的发射方法，目的是在母弹发射时捕获并发射它的武器(equipment)实例
        /// </summary>
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            // 首先，调用基类的方法，让子弹能正常发射
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            // 然后，将传入的equipment参数存到我们自己的字段里，以便之后使用
            this.weaponInt = equipment;
        }


        /// <summary>
        /// 当母弹命中目标或地面时调用此方法
        /// </summary>
        /// <param name="hitThing">被命中的物体，可能为null</param>
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 安全检查，确保XML配置正确
            if (Props == null || Props.submunition == null)
            {
                Log.Error($"[Touhou Fantasy] THF_Projectile_Cluster {this.def.defName} is missing THF_ClusterBombProperties or submunition is not defined in XML.");
                base.Impact(hitThing, blockedByShield); // 如果配置错误，则执行默认的命中逻辑
                return;
            }

            // 在母弹撞击点产生一个无害的烟雾效果，表示“起爆”
            GenExplosion.DoExplosion(
                center: this.Position,
                map: this.Map,
                radius: 0.8f,
                damType: DamageDefOf.Smoke,
                instigator: this.launcher,
                postExplosionSpawnThingDef: null,
                postExplosionSpawnChance: 0f,
                postExplosionSpawnThingCount: 0
                );

            // 循环生成并发射所有子弹药
            for (int i = 0; i < Props.submunitionCount; i++)
            {
                // 计算一个在散布半径内的随机目标点
                float angle = Rand.Range(0f, 360f); // 随机角度
                float radius = Rand.Range(0.5f, Props.scatterRadius); // 随机距离
                Vector3 offset = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
                IntVec3 targetCell = this.Position + offset.ToIntVec3();

                // 确保目标点在地图范围内并且可站立，否则使用母弹的中心点
                if (!targetCell.InBounds(this.Map) || !targetCell.Standable(this.Map))
                {
                    targetCell = this.Position;
                }

                // 生成一枚子弹药
                Projectile submunition = (Projectile)GenSpawn.Spawn(Props.submunition, this.Position, this.Map, WipeMode.Vanish);

                // 发射子弹药
                submunition.Launch(
                    launcher: this.launcher,             // 发射者
                    origin: this.ExactPosition,          // 起始位置（母弹撞击点）
                    usedTarget: new LocalTargetInfo(targetCell), // 目标位置（随机散射点）
                    intendedTarget: LocalTargetInfo.Invalid, // 【核心修正】: 子弹药是无特定目标的，飞向随机散射点，而不是继承母弹的目标
                    hitFlags: ProjectileHitFlags.All,    // 可命中所有东西
                    equipment: this.weaponInt         // 发射武器
                );
            }

            // 执行基类的Impact方法，这会处理母弹自身的销毁等逻辑
            base.Impact(hitThing, blockedByShield);
        }
    }
}