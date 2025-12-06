using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq; // 需要引入Linq来使用一些便捷的方法
using System.Reflection;
using Verse;
using VFECore.Shields;


namespace The_Memories_Of_Phantasm
{
    //public class Projectile_MindWeb : Projectile
    //{
    //    // 核心改动：移除了在飞行中检查的Tick方法，因为它不可靠。
    //    // 所有对拦截护盾（如百夫长护盾）的处理都已转移到TryBreakAllShields.cs的Harmony补丁中。

    //    /// <summary>
    //    /// 重写Impact方法，现在它只负责处理直接命中目标时的逻辑，
    //    /// 特别是处理目标身上穿戴的护盾腰带。
    //    /// </summary>
    //    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    //    {
    //        if (hitThing != null)
    //        {
    //            Log.Message($"[MindWeb] Impacted {hitThing.LabelCap}. Checking for worn shield belts.");

    //            if (hitThing is Pawn pawn)
    //            {
    //                // 检查pawn的穿戴物中是否有护盾腰带
    //                if (pawn.apparel != null)
    //                {
    //                    List<Apparel> wornApparel = pawn.apparel.WornApparel;
    //                    for (int i = 0; i < wornApparel.Count; i++)
    //                    {
    //                        CompShield shieldComp = wornApparel[i].GetComp<CompShield>();
    //                        if (shieldComp != null && shieldComp.ShieldState == ShieldState.Active)
    //                        {
    //                            Log.Message($"[MindWeb] Found active shield belt ({wornApparel[i].LabelCap}). Breaking it.");
    //                            // 使用反射调用私有的Break方法来击破护盾
    //                            AccessTools.Method(typeof(CompShield), "Break")?.Invoke(shieldComp, null);
    //                        }
    //                    }
    //                }
    //            }

    //            // 在处理完护盾腰带后，对目标造成伤害
    //            Log.Message($"[MindWeb] Applying damage to {hitThing.LabelCap}.");
    //            var damageInfo = new DamageInfo(
    //                this.def.projectile.damageDef,
    //                this.DamageAmount,
    //                this.ArmorPenetration,
    //                this.ExactRotation.eulerAngles.y,
    //                this.launcher,
    //                null,
    //                this.equipmentDef
    //            );
    //            hitThing.TakeDamage(damageInfo);
    //        }

    //        // 调用基类的Impact来处理音效、特效和摧毁自身
    //        // 注意：我们将blockedByShield强制设为false，以确保即使有什么意外情况，也能播放正确的命中效果
    //        base.Impact(hitThing, blockedByShield: false);
    //    }
    //}



    // ====================================================================
    // 3. 弹丸类定义
    // ====================================================================

    /// <summary>
    /// 用于在XML中定义狂气弹的属性
    /// </summary>
    public class LunaticBulletProperties : DefModExtension
    {
        // 要施加的精神状态
        public MentalStateDef mentalState;
    }

    /// <summary>
    /// 狂气弹的抛射体逻辑
    /// </summary>
    public class Projectile_LunaticBullet : Projectile
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 首先，执行基类的Impact，造成基础伤害
            base.Impact(hitThing, blockedByShield);

            // 从XML中获取属性
            var props = def.GetModExtension<LunaticBulletProperties>();
            if (props == null)
            {
                Log.ErrorOnce("[Touhou Armory] Projectile_LunaticBullet is missing LunaticBulletProperties extension in XML.", this.def.GetHashCode() + 654321);
                return;
            }

            // 检查逻辑与原版心灵武器保持一致
            if (hitThing is Pawn pawn && !pawn.Downed && pawn.mindState?.mentalStateHandler != null)
            {
                // 获取目标的心灵敏感度
                float psychicSensitivity = pawn.GetStatValue(StatDefOf.PsychicSensitivity);

                // 成功率现在完全由目标的心灵敏感度决定，与原版心灵武器一致
                if (psychicSensitivity > float.Epsilon && Rand.Value <= psychicSensitivity)
                {
                    // 施加在XML中定义的精神状态
                    pawn.mindState.mentalStateHandler.TryStartMentalState(props.mentalState, "ShotByLunaticGun".Translate(), forceWake: true);
                }
            }
        }
    }
}
