using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace The_Memories_Of_Phantasm
{
    // ====================================================================
    // 框架第1部分: XML接口 (DefModExtension)
    // ====================================================================

    /// <summary>
    /// 定义了动态伤害计算的来源类型。
    /// </summary>
    public enum FactorSource
    {
        TargetHediffSeverity,   // 根据目标身上的Hediff严重性
        TargetBodySize,         // 根据目标的体型
        DistanceToCaster        // 根据目标与发射者的距离
    }

    /// <summary>
    /// 定义了一个独立的伤害计算因子。
    /// 这个结构现在可以同时用于伤害和穿甲。
    /// </summary>
    public class DamageFactor
    {
        // 伤害来源
        public FactorSource source;

        // [仅当source为TargetHediffSeverity时需要] 指定是哪个Hediff
        public HediffDef hediffDef;

        // 乘数 (将来源值乘以这个数)
        public float multiplier = 1f;

        // 加数 (最后再加上这个数)
        public float addend = 0f;

        // （可选）使用曲线来定义非线性的数值关系
        public SimpleCurve curve;
    }

    /// <summary>
    /// 【已修改】挂载在Projectile的ThingDef上的主配置入口。
    /// 移除了 baseDamage 属性，并为穿甲添加了动态计算因子。
    /// </summary>
    public class DefModExtension_DynamicDamage : DefModExtension
    {
        // 伤害计算因子列表
        public List<DamageFactor> damageFactors = new List<DamageFactor>();

        // (可选) 穿甲计算因子列表
        public List<DamageFactor> armorPenetrationFactors = new List<DamageFactor>();
    }


    // ====================================================================
    // 框架第2部分: C#逻辑 (新的抛射体基类)
    // ====================================================================

    /// <summary>
    /// 一个通用的抛射体基类，所有希望使用动态伤害系统的抛射体都应继承它。
    /// </summary>
    public class Projectile_WithDynamicDamage : Projectile
    {
        // 缓存ModExtension以提高性能
        private DefModExtension_DynamicDamage modExt;

        public DefModExtension_DynamicDamage ModExt
        {
            get
            {
                if (modExt == null)
                {
                    modExt = def.GetModExtension<DefModExtension_DynamicDamage>();
                }
                return modExt;
            }
        }

        /// <summary>
        /// 【核心修改】根据XML配置计算最终伤害。
        /// </summary>
        protected virtual float CalculateDynamicDamage(Pawn caster, Thing hitThing)
        {
            // 如果没有配置、没有伤害因子或目标不是生物，直接返回弹丸的原始伤害
            if (ModExt == null || ModExt.damageFactors.NullOrEmpty() || !(hitThing is Pawn target))
            {
                return base.DamageAmount;
            }

            // --- 最终版计算逻辑 ---

            // 1. 获取包含品质等所有加成后的基础伤害
            float baseDamageWithQuality = base.DamageAmount;

            // 2. 计算纯粹的动态伤害倍率 (例如, 3层hediff, multiplier为1, 则返回3)
            float dynamicBonus = CalculateFactorBonus(ModExt.damageFactors, caster, target);

            // 3. (关键改动) 将动态加成作为总伤害的乘数。
            //    公式为：(基础伤害) * (1 + 动态倍率)
            //    品质倍率已经包含在 baseDamageWithQuality 中，因此不再需要单独计算。
            float finalDamage = baseDamageWithQuality * (1f + dynamicBonus);

            return Mathf.Max(0, finalDamage); // 确保伤害不为负
        }

        /// <summary>
        /// 【新增方法】根据XML配置计算最终穿甲值。
        /// </summary>
        protected virtual float CalculateDynamicArmorPenetration(Pawn caster, Thing hitThing)
        {
            float baseArmorPen = this.def.projectile.GetArmorPenetration(this.launcher);

            // 如果没有配置、没有穿甲因子或目标不是生物，直接返回弹丸的原始穿甲
            if (ModExt == null || ModExt.armorPenetrationFactors.NullOrEmpty() || !(hitThing is Pawn target))
            {
                return baseArmorPen;
            }

            // 从原始穿甲值开始计算
            float finalArmorPen = baseArmorPen;

            // 应用所有穿甲因子
            finalArmorPen += CalculateFactorBonus(ModExt.armorPenetrationFactors, caster, target);

            return Mathf.Max(0, finalArmorPen); // 确保穿甲不为负
        }

        /// <summary>
        /// 【新增辅助方法】用于计算一组因子的总加成值，避免代码重复。
        /// </summary>
        private float CalculateFactorBonus(List<DamageFactor> factors, Pawn caster, Pawn target)
        {
            float totalBonus = 0f;

            foreach (var factor in factors)
            {
                float sourceValue = 0f;

                // 1. 获取来源值
                switch (factor.source)
                {
                    case FactorSource.TargetHediffSeverity:
                        if (factor.hediffDef != null)
                        {
                            Hediff hediff = target.health.hediffSet.GetFirstHediffOfDef(factor.hediffDef);
                            if (hediff != null)
                            {
                                sourceValue = hediff.Severity;
                            }
                        }
                        break;
                    case FactorSource.TargetBodySize:
                        sourceValue = target.BodySize;
                        break;
                    case FactorSource.DistanceToCaster:
                        if (caster != null)
                        {
                            sourceValue = caster.Position.DistanceTo(target.Position);
                        }
                        break;
                }

                // 2. 计算单个因子的加成值
                float factorBonus;
                if (factor.curve != null)
                {
                    // 如果有曲线，优先使用曲线
                    factorBonus = factor.curve.Evaluate(sourceValue);
                }
                else
                {
                    // 否则使用乘数和加数
                    factorBonus = sourceValue * factor.multiplier + factor.addend;
                }

                // 3. 累加到总加成中
                totalBonus += factorBonus;
            }

            return totalBonus;
        }

        /// <summary>
        /// 【已修改】重写 Impact 方法以应用动态计算出的伤害和穿甲。
        /// </summary>
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 如果没有配置动态伤害，则执行原版逻辑
            if (ModExt == null)
            {
                base.Impact(hitThing, blockedByShield);
                return;
            }

            // --- 动态数值计算 ---
            var caster = this.launcher as Pawn;
            float dynamicDamage = CalculateDynamicDamage(caster, hitThing);
            float dynamicArmorPen = CalculateDynamicArmorPenetration(caster, hitThing);

            // --- 创建新的 DamageInfo ---
            var dinfo = new DamageInfo(
        this.def.projectile.damageDef,
        dynamicDamage,
        dynamicArmorPen, // 使用动态计算的穿甲
                -1f,
        this.launcher,
        null,
        this.equipmentDef,
        DamageInfo.SourceCategory.ThingOrUnknown,
        this.intendedTarget.Thing // 确保正确传递目标
            );

            // --- 应用伤害和效果 ---
            if (hitThing != null)
            {
                // 【代码修正 & 注释修正】设置攻击角度。
                // 这个角度对于原版的护甲偏转计算至关重要。
                // 我们需要手动计算从抛射体到目标的2D方向向量并获取其角度。
                Vector3 direction = hitThing.DrawPos - this.ExactPosition;
                dinfo.SetAngle(direction);
                hitThing.TakeDamage(dinfo);
            }

            // 播放命中效果等，即使没有命中实体也要播放
            // 调用 base.Impact(null) 可以安全地处理爆炸、声音等效果，而不会造成二次伤害
            base.Impact(null, blockedByShield);
        }
    }
}


