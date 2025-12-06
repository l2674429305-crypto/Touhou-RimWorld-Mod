using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using HarmonyLib;
using System.Reflection;
using System.Linq;

namespace The_Memories_Of_Phantasm
{
    // ====================================================================
    // 部分 A: Harmony 补丁
    // ====================================================================
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("com.burningship.thememoriesofphantasm");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    // Gizmo补丁: 确保UI切换按钮能显示
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Pawn_GetGizmos_Patch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var gizmo in __result)
            {
                yield return gizmo;
            }
            if (__instance.equipment?.Primary != null)
            {
                var comp = __instance.equipment.Primary.GetComp<Comp_MultiVerbSwitcher>();
                if (comp != null)
                {
                    foreach (var compGizmo in comp.CompGetGizmosExtra())
                    {
                        yield return compGizmo;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(MapInterface), nameof(MapInterface.MapInterfaceUpdate))]
    public static class MapInterface_MapInterfaceUpdate_Patch
    {
        public static void Postfix()
        {
            // 确保我们处于游戏状态且当前地图存在
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
            {
                return;
            }

            // 获取当前地图的BeamDraw组件实例
            BeamDraw beamDrawer = Find.CurrentMap.GetComponent<BeamDraw>();
            if (beamDrawer != null)
            {
                // 调用我们的绘制方法
                beamDrawer.DrawBeams();
            }
        }
    }
    // 属性补丁: 动态修改武器属性
    [HarmonyPatch(typeof(StatWorker), nameof(StatWorker.GetValueUnfinalized))]
    public static class StatWorker_GetValueUnfinalized_Patch
    {
        public static void Postfix(StatRequest req, ref float __result, StatDef ___stat)
        {
            // 确保我们只处理有Comp的物品
            if (req.HasThing && req.Thing is ThingWithComps thing)
            {
                var comp = thing.GetComp<Comp_MultiVerbSwitcher>();
                if (comp != null)
                {
                    // 调用Comp中的新方法来修改属性值
                    comp.ModifyStatValueIfApplicable(___stat, ref __result);
                }
            }
        }
    }

    // ====================================================================
    // 部分 B: 数据容器
    // ====================================================================
    public class WeaponMode
    {
        public string label;
        public string description;
        public string uiIcon;

        // verb属性都是可空类型
        public float? warmupTime;
        public float? range;
        public int? burstShotCount;
        public int? ticksBetweenBurstShots;
        public SoundDef soundCast;
        public ThingDef defaultProjectile;
        public System.Type verbClass;
        public bool? preventFriendlyFire;
        public float? cooldownTime;
        public float? accuracyTouch;
        public float? accuracyShort;
        public float? accuracyMedium;
        public float? accuracyLong;
        // 激光模式专属参数 (同样改为可空)
        public float? beamWidth;
        public int? beamDurationTicks;
        public ThingDef beamMoteDef;
    }

    public class DefModExtension_MultiVerb : DefModExtension
    {
        public List<WeaponMode> modes = new List<WeaponMode>();
    }

    // ====================================================================
    // 部分 C: 武器组件
    // ====================================================================
    public class CompProperties_MultiVerbSwitcher : CompProperties
    {
        public CompProperties_MultiVerbSwitcher()
        {
            this.compClass = typeof(Comp_MultiVerbSwitcher);
        }
    }

    public class Comp_MultiVerbSwitcher : ThingComp
    {
        private int currentModeIndex = 0;
        private DefModExtension_MultiVerb extension;
        private WeaponMode CurrentMode => extension.modes[currentModeIndex];

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            extension = parent.def.GetModExtension<DefModExtension_MultiVerb>();
            if (extension == null || extension.modes.NullOrEmpty())
            {
                Log.Error($"[Touhou Armory] {parent.def.defName} has Comp_MultiVerbSwitcher but is missing DefModExtension_MultiVerb or its modes list is empty.");
                return;
            }
            SwitchToMode(currentModeIndex);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (extension != null && extension.modes.Count > 1)
            {
                yield return new Command_Action
                {
                    defaultLabel = "切换模式: " + CurrentMode.label,
                    defaultDesc = CurrentMode.description,
                    icon = ContentFinder<Texture2D>.Get(CurrentMode.uiIcon),
                    action = delegate
                    {
                        currentModeIndex = (currentModeIndex + 1) % extension.modes.Count;
                        SwitchToMode(currentModeIndex);
                        SoundDefOf.Click.PlayOneShotOnCamera();

                        // 【新增】中断持有者当前的工作，避免因正在进行的攻击而产生状态残留
                        if (parent.ParentHolder is Pawn_EquipmentTracker equipmentTracker && equipmentTracker.pawn != null)
                        {
                            if (equipmentTracker.pawn.jobs != null)
                            {
                                equipmentTracker.pawn.jobs.StopAll(false, true);
                            }
                        }
                    }
                };
            }
        }

        private void SwitchToMode(int index)
        {
            var compEquippable = parent.GetComp<CompEquippable>();
            if (compEquippable?.PrimaryVerb == null) return;

            var primaryVerb = compEquippable.PrimaryVerb;
            var mode = extension.modes[index];
            var baseVerbProps = parent.def.Verbs.FirstOrDefault();

            if (baseVerbProps == null)
            {
                Log.ErrorOnce($"[Touhou Armory] {parent.def.defName} has no base verbs defined in its ThingDef to use as a fallback.", parent.def.GetHashCode());
                return;
            }

            // 【新增】Debug日志 - 切换前
            if (Prefs.DevMode)
            {
                Log.Message($"[MVS DEBUG] Switching to '{mode.label}'. BEFORE switch:");
                Log.Message($" - Burst Count: {primaryVerb.verbProps.burstShotCount}, Range: {primaryVerb.verbProps.range}, Warmup: {primaryVerb.verbProps.warmupTime}, VerbClass: {primaryVerb.verbProps.verbClass}");
            }

            var newVerbProps = new VerbProperties
            {
                verbClass = mode.verbClass ?? baseVerbProps.verbClass,
                hasStandardCommand = true,
                warmupTime = mode.warmupTime ?? baseVerbProps.warmupTime,
                range = mode.range ?? baseVerbProps.range,
                burstShotCount = mode.burstShotCount ?? baseVerbProps.burstShotCount,
                ticksBetweenBurstShots = mode.ticksBetweenBurstShots ?? baseVerbProps.ticksBetweenBurstShots,
                soundCast = mode.soundCast ?? baseVerbProps.soundCast,
                defaultProjectile = mode.defaultProjectile ?? baseVerbProps.defaultProjectile,
                muzzleFlashScale = baseVerbProps.muzzleFlashScale
            };

            // 1. 赋上新的 VerbProperties
            primaryVerb.verbProps = newVerbProps;

            // 2. 调用 Reset() 来清除所有内部缓存状态
            primaryVerb.Reset();

            // 3. 【修改】移除对EquipmentUtility的依赖，直接在此处通过反射清除Verb缓存
            var verbType = typeof(Verb);
            verbType.GetField("cachedTicksBetweenBurstShots", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(primaryVerb, null);
            verbType.GetField("cachedBurstShotCount", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(primaryVerb, null);

            // 【新增】Debug日志 - 切换后
            if (Prefs.DevMode)
            {
                Log.Message($"[MVS DEBUG] AFTER switch to '{mode.label}':");
                Log.Message($" - Burst Count: {primaryVerb.verbProps.burstShotCount}, Range: {primaryVerb.verbProps.range}, Warmup: {primaryVerb.verbProps.warmupTime}, VerbClass: {primaryVerb.verbProps.verbClass}");
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentModeIndex, "currentModeIndex", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 确保在加载游戏后也应用正确的模式，这会应用所有切换逻辑
                SwitchToMode(currentModeIndex);
            }
        }

        public WeaponMode GetCurrentMode()
        {
            if (extension == null || extension.modes.NullOrEmpty()) return null;
            return CurrentMode;
        }

        public void ModifyStatValueIfApplicable(StatDef stat, ref float val)
        {
            if (extension == null || extension.modes.NullOrEmpty()) return;

            var mode = CurrentMode;

            if (stat == StatDefOf.RangedWeapon_Cooldown && mode.cooldownTime.HasValue)
            {
                val = mode.cooldownTime.Value;
            }
            else if (stat == StatDefOf.AccuracyTouch && mode.accuracyTouch.HasValue)
            {
                val = mode.accuracyTouch.Value;
            }
            else if (stat == StatDefOf.AccuracyShort && mode.accuracyShort.HasValue)
            {
                val = mode.accuracyShort.Value;
            }
            else if (stat == StatDefOf.AccuracyMedium && mode.accuracyMedium.HasValue)
            {
                val = mode.accuracyMedium.Value;
            }
            else if (stat == StatDefOf.AccuracyLong && mode.accuracyLong.HasValue)
            {
                val = mode.accuracyLong.Value;
            }
        }
    }
}


