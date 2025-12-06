using RimWorld;
using System.Collections.Generic;
using Verse;

namespace The_Memories_Of_Phantasm
{
    // ==================== HediffComp部分：负责超时和模式切换引爆 ====================

    public class HediffCompProperties_SilverBladeTimeout : HediffCompProperties
    {
        public int disappearAfterTicks = 180;
        public float explosionRadius = 1.5f;
        public DamageDef explosionDamageDef;
        public int explosionDamageAmount = 5;

        public HediffCompProperties_SilverBladeTimeout()
        {
            this.compClass = typeof(HediffComp_SilverBladeTimeout);
        }
    }

    public class HediffComp_SilverBladeTimeout : HediffComp
    {
        private int lastAppliedTick;
        private Thing instigator;
        private ThingDef weaponDef;

        public HediffCompProperties_SilverBladeTimeout Props => (HediffCompProperties_SilverBladeTimeout)this.props;

        public override void CompPostMake()
        {
            base.CompPostMake();
            ResetTimer();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref lastAppliedTick, "lastAppliedTick", 0);
            Scribe_References.Look(ref instigator, "instigator");
            Scribe_Defs.Look(ref weaponDef, "weaponDef");
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            // 【核心修改 - "拉"模式安全网】
            // 每秒检查一次，作为处理边缘情况的保险机制。
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                // 检查1: 是否超时。
                if (Find.TickManager.TicksGame > lastAppliedTick + Props.disappearAfterTicks)
                {
                    Detonate();
                    return;
                }

                // 检查2: 攻击者是否还合法（例如死亡、离开地图等）。
                // 这是“推”模式无法处理的边缘情况。
                if (this.instigator == null || this.instigator.Destroyed || !this.instigator.Spawned)
                {
                    Detonate();
                    return;
                }
            }
        }

        public void ResetTimer()
        {
            lastAppliedTick = Find.TickManager.TicksGame;
        }

        public void SetInstigator(Thing newInstigator)
        {
            this.instigator = newInstigator;
        }

        public void SetWeaponDef(ThingDef newWeaponDef)
        {
            this.weaponDef = newWeaponDef;
        }

        /// <summary>
        /// 【新增】返回攻击者。
        /// </summary>
        public Thing GetInstigator()
        {
            return this.instigator;
        }

        /// <summary>
        /// 【修改】从private改为public，以便被外部（如MultiVerbSwitcher）调用。
        /// </summary>
        public void Detonate()
        {
            // 增加一个Pawn.Destroyed的检查，防止重复引爆时出错。
            if (this.Pawn == null || this.Pawn.Destroyed || !this.Pawn.Spawned || this.Pawn.Map == null) return;

            GenExplosion.DoExplosion(
                center: this.Pawn.Position,
                map: this.Pawn.Map,
                radius: Props.explosionRadius,
                damType: Props.explosionDamageDef,
                instigator: this.instigator,
                damAmount: Props.explosionDamageAmount,
                armorPenetration: 0f,
                weapon: this.weaponDef
            );

            // 确保Hediff在引爆后被移除。
            this.parent.Severity = 0;
        }
    }

    // ==================== Hediff部分：负责提供接口 ====================
    public class Hediff_SilverBladeMark : HediffWithComps
    {
        public override string SeverityLabel => this.Severity.ToString("F0");

        public void ResetTimer()
        {
            this.TryGetComp<HediffComp_SilverBladeTimeout>()?.ResetTimer();
        }

        public void SetInstigator(Thing instigator)
        {
            this.TryGetComp<HediffComp_SilverBladeTimeout>()?.SetInstigator(instigator);
        }

        public void SetWeaponDef(ThingDef weaponDef)
        {
            this.TryGetComp<HediffComp_SilverBladeTimeout>()?.SetWeaponDef(weaponDef);
        }

        /// <summary>
        /// 【新增】公开接口，用于获取攻击者。
        /// </summary>
        public Thing GetInstigator()
        {
            return this.TryGetComp<HediffComp_SilverBladeTimeout>()?.GetInstigator();
        }

        /// <summary>
        /// 【新增】公开接口，用于命令引爆。
        /// </summary>
        public void Detonate()
        {
            this.TryGetComp<HediffComp_SilverBladeTimeout>()?.Detonate();
        }
    }
}
