using RimWorld;
using Verse;
using System.Linq;

namespace The_Memories_Of_Phantasm
{
    /// <summary>
    /// 一个静态工具类，用于处理由武器或技能触发的全局性事件。
    /// </summary>
    public static class VerbEvents
    {
        /// <summary>
        /// 触发“银色世界”的全局引爆效果。
        /// </summary>
        /// <param name="caster">施法者/攻击者</param>
        public static void TriggerSilverBladeDetonation(Pawn caster)
        {
            // 确保攻击者状态合法
            if (caster == null || !caster.Spawned || caster.Map == null) return;

            var hediffDef = HediffDef.Named("Touhou_Hediff_SilverBladeMark");
            if (hediffDef == null)
            {
                Log.ErrorOnce("[Touhou Armory] 无法找到HediffDef: Touhou_Hediff_SilverBladeMark", 998877);
                return;
            }

            // 【性能优化】
            // 原本遍历地图上所有Pawn。现在只筛选出对攻击者派系为敌对的单位。
            // 这会极大地缩小搜索范围，特别是当殖民地规模很大时。
            var potentialTargets = caster.Map.mapPawns.AllPawnsSpawned
                .Where(p => p.HostileTo(caster.Faction))
                .ToList();

            foreach (Pawn pawn in potentialTargets)
            {
                // 增加更多检查以确保安全
                if (pawn == null || pawn.Destroyed || pawn.health == null || pawn.health.hediffSet == null) continue;

                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) as Hediff_SilverBladeMark;

                // 确认Hediff是由当前攻击者施加的
                if (hediff != null && hediff.GetInstigator() == caster)
                {
                    hediff.Detonate();
                }
            }
        }
    }
}
