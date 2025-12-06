using HarmonyLib;
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace The_Memories_Of_Phantasm
{
    /// 一个空的 DefModExtension，仅用作“破盾”能力的标记。
    public class DefModExtension_ShieldBreaker : DefModExtension { }
    [StaticConstructorOnStartup]
    public static class ShieldBreaker_HarmonyPatches
    {
        static ShieldBreaker_HarmonyPatches()
        {
            // 建议将 "com.yourname" 替换为你的作者名或Mod的唯一ID
            var harmony = new Harmony("com.burningship.thememoriesofphantasm.shieldbreaker");

            // 补丁 1: 拦截护盾 (如机械蜈蚣的 CompProjectileInterceptor)
            harmony.Patch(AccessTools.Method(typeof(CompProjectileInterceptor), nameof(CompProjectileInterceptor.CheckIntercept)),
                prefix: new HarmonyMethod(typeof(ShieldBreaker_HarmonyPatches), nameof(Prefix_CheckIntercept)));

            // 补丁 2: 装备护盾 (如护盾腰带的 CompShield)
            harmony.Patch(AccessTools.Method(typeof(CompShield), nameof(CompShield.PostPreApplyDamage)),
                prefix: new HarmonyMethod(typeof(ShieldBreaker_HarmonyPatches), nameof(Prefix_PostPreApplyDamage)));

            // 补丁 3: VEF 框架护盾 (可选，自动检测)
            TryPatchVEFShields(harmony);

            Log.Message("[Touhou Armory] Applied Shield-Breaker patches.");
        }

        // --- 核心检查逻辑 ---

        /// <summary>
        /// 检查一个投射物是否定义为“破盾”弹药。
        /// </summary>
        private static bool IsShieldBreaker(Projectile proj)
        {
            return proj.def.HasModExtension<DefModExtension_ShieldBreaker>();
        }

        /// <summary>
        /// 检查一个伤害信息是否来源于装备了“破盾”弹药的武器。
        /// </summary>
        private static bool IsShieldBreaker(DamageInfo dinfo)
        {
            // 检查伤害来源武器的任何一个开火模式(verb)的子弹是否是破盾弹
            return dinfo.Weapon?.Verbs?.Any(v => v.defaultProjectile?.HasModExtension<DefModExtension_ShieldBreaker>() ?? false) ?? false;
        }


        // --- 补丁实现 ---

        // 拦截护盾逻辑
        public static bool Prefix_CheckIntercept(CompProjectileInterceptor __instance, Projectile projectile)
        {
            // 如果护盾激活，且子弹是破盾弹
            if (__instance.Active && IsShieldBreaker(projectile))
            {
                // 对护盾产生器造成EMP伤害，使其失效
                // 注意：这里可以根据需要调整伤害类型和数值
                __instance.parent.TakeDamage(new DamageInfo(DamageDefOf.EMP, 50f, 999f, -1f, projectile.Launcher));
                return false; // 返回 false 以阻止原版拦截方法的执行，让子弹穿过去
            }
            return true; // 否则，执行原版逻辑
        }

        // 装备护盾逻辑
        public static bool Prefix_PostPreApplyDamage(CompShield __instance, ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            // 如果护盾激活，且伤害来源于破盾武器
            if (__instance.ShieldState == ShieldState.Active && IsShieldBreaker(dinfo))
            {
                // 直接击破护盾
                var breakMethod = AccessTools.Method(typeof(CompShield), "Break");
                breakMethod?.Invoke(__instance, null);

                // 不吸收伤害，让伤害继续传递
                return true; // 返回 true 继续执行原方法（但护盾已破，不会再吸收伤害）
            }
            return true;
        }

        // --- VEF 兼容补丁 ---

        private static void TryPatchVEFShields(Harmony harmony)
        {
            // 通过名称安全地查找VEF护盾的类型
            Type vefShieldType = AccessTools.TypeByName("VEF.Hediffs.HediffComp_Shield");
            if (vefShieldType != null)
            {
                var originalMethod = AccessTools.Method(vefShieldType, "PreAbsorbDamage");
                if (originalMethod != null)
                {
                    harmony.Patch(originalMethod, prefix: new HarmonyMethod(typeof(ShieldBreaker_HarmonyPatches), nameof(Prefix_VEF_PreAbsorbDamage)));
                    Log.Message("[Touhou Armory] Successfully patched VEF shields for Shield-Breaker compatibility.");
                }
            }
        }

        // VEF 护盾逻辑
        public static bool Prefix_VEF_PreAbsorbDamage(object __instance, DamageInfo dinfo, ref bool __result)
        {
            // 如果伤害来源于破盾武器
            if (IsShieldBreaker(dinfo))
            {
                // 通过反射调用Break方法
                var breakMethod = AccessTools.Method(__instance.GetType(), "Break");
                breakMethod?.Invoke(__instance, null);

                __result = false; // 表示伤害未被吸收
                return false;     // 阻止原方法执行
            }
            return true;
        }
    }
}


