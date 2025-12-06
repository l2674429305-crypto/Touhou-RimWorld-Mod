using RimWorld;
using System.Security.Cryptography;
using Verse;

namespace The_Memories_Of_Phantasm
{
    public class SilverBladeModExtension : DefModExtension
    {
        public HediffDef hediffToApply;
    }

    /// <summary>
    /// 【已重构】
    /// 这个类现在继承自我们新的框架基类 Projectile_WithDynamicDamage。
    /// 它不再需要自己计算伤害，只负责在命中后施加或刷新Hediff这一独特行为。
    /// </summary>
    public class Projectile_SilverBlade : Projectile_WithDynamicDamage
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 1. 调用基类的Impact方法。
            //    这会触发动机枪伤害计算框架，并对目标造成伤害。
            base.Impact(hitThing, blockedByShield);

            // 2. 执行这个抛射体独特的逻辑：施加/刷新Hediff。
            //    确保我们命中了目标且目标是生物。
            if (hitThing != null && hitThing is Pawn pawn)
            {
                // （这个ModExtension现在只用于获取要施加的Hediff，不再参与伤害计算）
                var modExt = def.GetModExtension<SilverBladeModExtension>();
                if (modExt == null || modExt.hediffToApply == null)
                {
                    Log.ErrorOnce("[Touhou Armory] Projectile_SilverBlade is missing SilverBladeModExtension or hediffToApply is not set in XML.", this.def.GetHashCode() + 12345);
                    return;
                }

                // 添加/更新Hediff
                pawn.health.AddHediff(modExt.hediffToApply);

                // 获取刚刚添加的Hediff实例
                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(modExt.hediffToApply) as Hediff_SilverBladeMark;
                if (hediff != null)
                {
                    // 刷新计时器并传递攻击者信息
                    hediff.ResetTimer();
                    hediff.SetInstigator(this.launcher);
                    hediff.SetWeaponDef(this.equipmentDef);
                }
            }
        }
    }
}
