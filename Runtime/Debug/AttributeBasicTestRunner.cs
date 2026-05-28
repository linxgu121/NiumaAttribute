using System;
using System.Collections.Generic;
using NiumaAttribute.Config;
using NiumaAttribute.Data;
using NiumaAttribute.Enum;
using NiumaAttribute.Result;
using NiumaAttribute.Service;
using UnityEngine;

namespace NiumaAttribute.Debugging
{
    /// <summary>
    /// NiumaAttribute 阶段八基础冻结测试入口。
    /// 该组件只用于开发期验证属性、资源、Modifier、存档快照和桥接前置数据是否稳定，不参与正式游戏逻辑。
    /// </summary>
    public sealed class AttributeBasicTestRunner : MonoBehaviour
    {
        private const string ActorId = "player";
        private const string StrengthId = "str";
        private const string ConstitutionId = "con";
        private const string AttackId = "attack";
        private const string MaxHealthId = "max_health";
        private const string MaxStaminaId = "max_stamina";
        private const string HealthId = "health";
        private const string StaminaId = "stamina";
        private const string LoopAId = "loop_a";
        private const string LoopBId = "loop_b";

        [Header("运行设置")]
        [Tooltip("Start 时是否自动运行阶段八基础冻结测试。正式场景建议关闭，只通过右键菜单手动运行。")]
        [SerializeField] private bool runOnStart;

        [Tooltip("是否在 Console 输出每一条通过的检查。关闭后只输出失败和最终汇总。")]
        [SerializeField] private bool logPassedChecks;

        [Header("最近一次结果")]
        [Tooltip("最近一次阶段八基础冻结测试是否全部通过。")]
        [SerializeField] private bool lastRunSucceeded;

        [Tooltip("最近一次通过的检查数量。")]
        [SerializeField] private int passedCheckCount;

        [Tooltip("最近一次失败的检查数量。")]
        [SerializeField] private int failedCheckCount;

        [Tooltip("最近一次测试报告。")]
        [TextArea(8, 24)]
        [SerializeField] private string lastReport;

        private readonly List<string> _reportLines = new List<string>();
        private readonly List<UnityEngine.Object> _createdAssets = new List<UnityEngine.Object>();

        private void Start()
        {
            if (runOnStart)
            {
                RunStage8BasicTests();
            }
        }

        /// <summary>
        /// 运行阶段八基础冻结测试。
        /// </summary>
        [ContextMenu("NiumaAttribute/阶段8/运行基础冻结测试")]
        public void RunStage8BasicTests()
        {
            ResetReport();

            RunCase("基础属性与派生属性计算", TestBaseAndDerivedAttributes);
            RunCase("Modifier 分层、百分比、倍率与覆盖", TestModifierStackingAndReplacement);
            RunCase("限时 Modifier 到期移除", TestModifierExpiration);
            RunCase("资源消耗、恢复、延迟和 Clamp", TestResourceConsumeRecoverAndClamp);
            RunCase("资源上限变化时保留百分比", TestMaxValuePreservePercent);
            RunCase("导出导入与持久 Modifier 恢复", TestExportImportPersistentFacts);
            RunCase("循环派生公式兜底", TestCircularFormulaFallback);
            RunCase("UI ViewData 查询稳定", TestViewDataQueries);

            lastRunSucceeded = failedCheckCount == 0;
            lastReport = string.Join(Environment.NewLine, _reportLines);

            var summary = $"[NiumaAttribute][阶段8] 基础冻结测试结束：Passed={passedCheckCount}, Failed={failedCheckCount}";
            if (lastRunSucceeded)
            {
                Debug.Log(summary, this);
            }
            else
            {
                Debug.LogError(summary + Environment.NewLine + lastReport, this);
            }

            DestroyCreatedAssets();
        }

        /// <summary>
        /// 清空最近一次测试报告。
        /// </summary>
        [ContextMenu("NiumaAttribute/阶段8/清空测试报告")]
        public void ClearReport()
        {
            lastRunSucceeded = false;
            passedCheckCount = 0;
            failedCheckCount = 0;
            lastReport = string.Empty;
            _reportLines.Clear();
        }

        private void TestBaseAndDerivedAttributes()
        {
            var service = CreateServiceAndActor();

            ExpectApproximately(service.GetFinalValue(ActorId, StrengthId), 10f, "力量默认值正确");
            ExpectApproximately(service.GetFinalValue(ActorId, AttackId), 22f, "攻击 = 基础 2 + 力量 10 * 2");
            ExpectApproximately(service.GetResourceMax(ActorId, HealthId), 100f, "生命上限 = 基础 50 + 体质 5 * 10");
        }

        private void TestModifierStackingAndReplacement()
        {
            var service = CreateServiceAndActor();

            ExpectSucceeded(service.AddModifier(ActorId, BuildModifier("equipment:sword", "attack_add", AttackId, AttributeModifierOperation.Add, 8f)), "添加攻击固定加成");
            ExpectApproximately(service.GetFinalValue(ActorId, AttackId), 30f, "攻击固定加成生效");

            ExpectSucceeded(service.AddModifier(ActorId, BuildModifier("equipment:sword", "attack_add", AttackId, AttributeModifierOperation.Add, 10f)), "相同 SourceId + ModifierId 覆盖旧加成");
            ExpectApproximately(service.GetFinalValue(ActorId, AttackId), 32f, "攻击固定加成覆盖后生效");

            ExpectSucceeded(service.AddModifier(ActorId, BuildModifier("equipment:ring", "attack_percent", AttackId, AttributeModifierOperation.PercentAdd, 0.5f)), "添加攻击百分比加成");
            var effectMultiplier = BuildModifier("effect:blessing", "attack_multiplier", AttackId, AttributeModifierOperation.Multiplier, 2f);
            effectMultiplier.Layer = AttributeModifierLayer.Effect;
            ExpectSucceeded(service.AddModifier(ActorId, effectMultiplier), "添加攻击倍率加成");
            ExpectApproximately(service.GetFinalValue(ActorId, AttackId), 96f, "分层计算顺序稳定：Equipment 后 Effect");
        }

        private void TestModifierExpiration()
        {
            var service = CreateServiceAndActor();
            var modifier = BuildModifier("effect:short_buff", "temp_attack", AttackId, AttributeModifierOperation.Add, 10f);
            modifier.Layer = AttributeModifierLayer.Effect;
            modifier.DurationSeconds = 0.2f;

            ExpectSucceeded(service.AddModifier(ActorId, modifier), "添加限时 Modifier");
            ExpectApproximately(service.GetFinalValue(ActorId, AttackId), 32f, "限时 Modifier 添加后生效");

            service.Tick(0.25f);
            ExpectApproximately(service.GetFinalValue(ActorId, AttackId), 22f, "限时 Modifier 到期后自动移除");
        }

        private void TestResourceConsumeRecoverAndClamp()
        {
            var service = CreateServiceAndActor();

            ExpectApproximately(service.GetResourceCurrent(ActorId, HealthId), 100f, "生命初始为满值");
            ExpectSucceeded(service.ConsumeResource(ActorId, HealthId, 30f), "消耗生命成功");
            ExpectApproximately(service.GetResourceCurrent(ActorId, HealthId), 70f, "生命消耗后为 70");

            service.Tick(0.5f);
            ExpectApproximately(service.GetResourceCurrent(ActorId, HealthId), 70f, "恢复延迟未结束时生命不恢复");

            service.Tick(0.6f);
            Expect(service.GetResourceCurrent(ActorId, HealthId) > 70f, "恢复延迟结束后生命开始恢复");

            ExpectSucceeded(service.RecoverResource(ActorId, HealthId, 999f), "超量恢复请求成功执行 Clamp");
            ExpectApproximately(service.GetResourceCurrent(ActorId, HealthId), 100f, "生命恢复不会超过上限");

            ExpectSucceeded(service.ConsumeResource(ActorId, HealthId, 999f), "超量消耗请求成功执行 Clamp");
            ExpectApproximately(service.GetResourceCurrent(ActorId, HealthId), 0f, "生命消耗不会低于 0");
        }

        private void TestMaxValuePreservePercent()
        {
            var service = CreateServiceAndActor();

            ExpectSucceeded(service.SetResourceCurrent(ActorId, HealthId, 50f), "设置生命到 50%");
            ExpectSucceeded(service.SetBaseValue(ActorId, MaxHealthId, 200f), "提高生命上限基础值");

            ExpectApproximately(service.GetResourceMax(ActorId, HealthId), 250f, "生命上限重新计算后正确");
            ExpectApproximately(service.GetResourceCurrent(ActorId, HealthId), 125f, "生命上限变化后按百分比保留当前值");
        }

        private void TestExportImportPersistentFacts()
        {
            var service = CreateServiceAndActor();

            ExpectSucceeded(service.SetResourceCurrent(ActorId, HealthId, 55f), "设置生命当前值用于存档");
            var persistent = BuildModifier("story:blessing", "persistent_attack", AttackId, AttributeModifierOperation.Add, 5f);
            persistent.IsPersistent = true;
            ExpectSucceeded(service.AddModifier(ActorId, persistent), "添加持久 Modifier");

            var temporary = BuildModifier("effect:temporary", "temporary_attack", AttackId, AttributeModifierOperation.Add, 99f);
            temporary.IsPersistent = false;
            ExpectSucceeded(service.AddModifier(ActorId, temporary), "添加非持久 Modifier");

            var snapshot = service.ExportSnapshot();
            var restored = new AttributeService(CreateAttributeDefinitions(), CreateResourceDefinitions());
            var importResult = restored.ImportSnapshot(snapshot);

            Expect(importResult != null && importResult.Succeeded, "导入属性快照成功");
            ExpectApproximately(restored.GetResourceCurrent(ActorId, HealthId), 55f, "导入后生命当前值恢复");
            ExpectApproximately(restored.GetFinalValue(ActorId, AttackId), 27f, "导入后只恢复持久 Modifier，不恢复非持久 Modifier");
        }

        private void TestCircularFormulaFallback()
        {
            var service = CreateServiceAndActor();
            ExpectSucceeded(service.AddModifier(ActorId, BuildModifier("debug:loop", "loop_add", LoopAId, AttributeModifierOperation.Add, 3f)), "循环属性添加兜底 Modifier");

            ExpectApproximately(service.GetFinalValue(ActorId, LoopAId), 4f, "循环派生公式使用 BaseValue + Modifier 兜底");
            Expect(service.TryGetFinalValue(ActorId, AttackId, out var attack) && attack > 0f, "循环属性不会阻断后续属性查询");
        }

        private void TestViewDataQueries()
        {
            var service = CreateServiceAndActor();
            ExpectSucceeded(service.AddModifier(ActorId, BuildModifier("equipment:sword", "attack_add", AttackId, AttributeModifierOperation.Add, 8f)), "添加 UI 查询用 Modifier");

            var attributes = service.GetAllAttributes(ActorId);
            var resources = service.GetAllResources(ActorId);

            Expect(attributes != null && attributes.Length >= 5, "属性 ViewData 数量正确");
            Expect(resources != null && resources.Length >= 2, "资源 ViewData 数量正确");
            Expect(service.TryGetResource(ActorId, HealthId, out var health) && health.Percent > 0.99f, "资源 ViewData 百分比正确");
        }

        private AttributeService CreateServiceAndActor()
        {
            var service = new AttributeService(CreateAttributeDefinitions(), CreateResourceDefinitions());
            ExpectSucceeded(service.CreateActor(ActorId, nameof(AttributeBasicTestRunner)), "创建测试 Actor");
            return service;
        }

        private AttributeDefinition[] CreateAttributeDefinitions()
        {
            return new[]
            {
                CreateAttribute(StrengthId, "力量", AttributeKind.Base, 10f),
                CreateAttribute(ConstitutionId, "体质", AttributeKind.Base, 5f),
                CreateAttribute(AttackId, "攻击", AttributeKind.Derived, 2f, new[]
                {
                    new DerivedAttributeTermData { SourceAttributeId = StrengthId, Coefficient = 2f }
                }),
                CreateAttribute(MaxHealthId, "生命上限", AttributeKind.ResourceMax, 50f, new[]
                {
                    new DerivedAttributeTermData { SourceAttributeId = ConstitutionId, Coefficient = 10f }
                }),
                CreateAttribute(MaxStaminaId, "体力上限", AttributeKind.ResourceMax, 100f),
                CreateAttribute(LoopAId, "循环测试 A", AttributeKind.Derived, 1f, new[]
                {
                    new DerivedAttributeTermData { SourceAttributeId = LoopBId, Coefficient = 1f }
                }),
                CreateAttribute(LoopBId, "循环测试 B", AttributeKind.Derived, 2f, new[]
                {
                    new DerivedAttributeTermData { SourceAttributeId = LoopAId, Coefficient = 1f }
                })
            };
        }

        private ResourceDefinition[] CreateResourceDefinitions()
        {
            return new[]
            {
                CreateResource(HealthId, "生命", MaxHealthId, true, 10f, 1f),
                CreateResource(StaminaId, "体力", MaxStaminaId, true, 15f, 0f)
            };
        }

        private AttributeDefinition CreateAttribute(
            string id,
            string displayName,
            AttributeKind kind,
            float baseValue,
            DerivedAttributeTermData[] terms = null)
        {
            var definition = ScriptableObject.CreateInstance<AttributeDefinition>();
            definition.AttributeId = id;
            definition.DisplayName = displayName;
            definition.Kind = kind;
            definition.DefaultBaseValue = baseValue;
            definition.DerivedTerms = terms ?? Array.Empty<DerivedAttributeTermData>();
            _createdAssets.Add(definition);
            return definition;
        }

        private ResourceDefinition CreateResource(
            string id,
            string displayName,
            string maxAttributeId,
            bool enableAutoRecovery,
            float recoveryPerSecond,
            float recoveryDelaySeconds)
        {
            var definition = ScriptableObject.CreateInstance<ResourceDefinition>();
            definition.ResourceId = id;
            definition.DisplayName = displayName;
            definition.MaxAttributeId = maxAttributeId;
            definition.InitialMode = ResourceInitialMode.Full;
            definition.SaveCurrentValue = true;
            definition.EnableAutoRecovery = enableAutoRecovery;
            definition.RecoveryPerSecond = recoveryPerSecond;
            definition.RecoveryDelaySeconds = recoveryDelaySeconds;
            definition.AllowOverRecover = false;
            _createdAssets.Add(definition);
            return definition;
        }

        private static AttributeModifier BuildModifier(
            string sourceId,
            string modifierId,
            string targetAttributeId,
            AttributeModifierOperation operation,
            float value)
        {
            return new AttributeModifier
            {
                SourceId = sourceId,
                ModifierId = modifierId,
                TargetAttributeId = targetAttributeId,
                Operation = operation,
                Layer = AttributeModifierLayer.Equipment,
                Value = value,
                SourceModule = nameof(AttributeBasicTestRunner)
            };
        }

        private void RunCase(string name, Action action)
        {
            try
            {
                action();
                AddReportLine($"[PASS] {name}");
            }
            catch (Exception exception)
            {
                failedCheckCount++;
                var message = $"[FAIL] {name}：{exception.Message}";
                _reportLines.Add(message);
                Debug.LogError($"[NiumaAttribute][阶段8] {message}", this);
            }
        }

        private void ExpectSucceeded(AttributeOperationResult result, string message)
        {
            Expect(result != null && result.Succeeded, $"{message}。Reason={result?.FailureReason}, Detail={result?.Message}");
        }

        private void ExpectApproximately(float actual, float expected, string message, float epsilon = 0.01f)
        {
            Expect(Math.Abs(actual - expected) <= epsilon, $"{message}。Expected={expected}, Actual={actual}");
        }

        private void Expect(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }

            passedCheckCount++;
            if (logPassedChecks)
            {
                AddReportLine($"[OK] {message}");
            }
        }

        private void AddReportLine(string line)
        {
            _reportLines.Add(line);
            if (logPassedChecks)
            {
                Debug.Log($"[NiumaAttribute][阶段8] {line}", this);
            }
        }

        private void ResetReport()
        {
            lastRunSucceeded = false;
            passedCheckCount = 0;
            failedCheckCount = 0;
            lastReport = string.Empty;
            _reportLines.Clear();
            DestroyCreatedAssets();
        }

        private void DestroyCreatedAssets()
        {
            for (var i = 0; i < _createdAssets.Count; i++)
            {
                var asset = _createdAssets[i];
                if (asset == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(asset);
                }
                else
                {
                    DestroyImmediate(asset);
                }
            }

            _createdAssets.Clear();
        }
    }
}
