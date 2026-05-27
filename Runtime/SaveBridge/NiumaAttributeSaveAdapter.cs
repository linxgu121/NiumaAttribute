using System;
using System.Collections.Generic;
using System.Text;
using NiumaAttribute.Controller;
using NiumaAttribute.Data;
using NiumaAttribute.Enum;
using NiumaSave.Controller;
using NiumaSave.Data;
using NiumaSave.Provider;
using UnityEngine;

namespace NiumaAttribute.SaveBridge
{
    /// <summary>
    /// NiumaAttribute 存档桥接器。
    /// 负责把属性快照转换为 NiumaSave 的 Section 数据，并在读档时恢复到属性控制器。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaAttributeSaveAdapter : MonoBehaviour, ISaveDataProvider
    {
        private const string AttributeSectionId = "attribute";
        private const string AttributeSectionVersionV1 = "1";
        private const string CurrentAttributeSectionVersion = AttributeSectionVersionV1;
        private const string AttributeSectionFormat = "json";

        [Header("模块引用")]
        [Tooltip("属性模块根控制器。请拖入场景中的 NiumaAttributeController，导出和导入属性数据都会通过它完成。")]
        [SerializeField] private NiumaAttributeController attributeController;

        [Tooltip("存档模块根控制器。开启自动注册时，请拖入场景中的 NiumaSaveController。")]
        [SerializeField] private NiumaSaveController saveController;

        [Header("注册行为")]
        [Tooltip("启用组件时是否自动注册到 NiumaSaveController。正式场景建议开启，并确保 NiumaSaveController 更早初始化，或把本组件挂在存档控制器子物体下。")]
        [SerializeField] private bool registerOnEnable = true;

        [Tooltip("引用为空时是否自动在场景中查找对应组件。仅建议调试阶段开启；正式多场景或 DontDestroyOnLoad 场景必须手动绑定，避免找到错误实例。")]
        [SerializeField] private bool autoFindReferences = true;

        [Header("导入行为")]
        [Tooltip("导入完成后是否打印迁移警告。旧存档中的属性、资源或最大值绑定与当前配置不一致时，AttributeService 会记录这些警告。")]
        [SerializeField] private bool logMigrationWarningsAfterImport = true;

        private bool _registeredToSaveController;

        /// <summary>
        /// 属性模块的稳定存档段 ID。
        /// </summary>
        public string SectionId => AttributeSectionId;

        /// <summary>
        /// 属性存档段结构版本。
        /// </summary>
        public string SectionVersion => CurrentAttributeSectionVersion;

        /// <summary>
        /// 属性数据修订号。
        /// NiumaSave 通过该值判断属性模块是否发生变化。
        /// </summary>
        public long Revision => attributeController != null ? attributeController.AttributeRevision : 0L;

        private void Awake()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            if (registerOnEnable)
            {
                RegisterToSaveController();
            }
        }

        private void OnDisable()
        {
            UnregisterFromSaveController();
        }

        /// <summary>
        /// 导出属性运行时快照为 NiumaSave Section。
        /// 通过 SaveDataProviderRegistry 批量导出时，外层会捕获导出异常并转为结构化失败结果。
        /// 若外部直接调用该方法，必须自行处理 InvalidOperationException，避免缺少引用时打断完整存档流程。
        /// </summary>
        public SaveSectionData ExportSection()
        {
            ResolveReferences(false);
            if (attributeController == null)
            {
                throw new InvalidOperationException("NiumaAttributeSaveAdapter 缺少 NiumaAttributeController，无法导出属性存档。");
            }

            var saveData = attributeController.ExportSnapshot() ?? new AttributeSaveData();
            ValidateSaveDataForExport(saveData);

            var json = JsonUtility.ToJson(saveData);
            var bytes = Encoding.UTF8.GetBytes(json);

            return new SaveSectionData
            {
                SectionId = SectionId,
                SectionVersion = SectionVersion,
                Format = AttributeSectionFormat,
                DataEncoding = SaveDataEncoding.Base64,
                EncodedData = Convert.ToBase64String(bytes)
            };
        }

        /// <summary>
        /// 从 NiumaSave Section 导入属性快照。
        /// </summary>
        public SaveSectionImportResult ImportSection(SaveSectionData section)
        {
            ResolveReferences(false);
            if (attributeController == null)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.ConfigMissing,
                    "NiumaAttributeSaveAdapter 缺少 NiumaAttributeController，无法导入属性存档。");
            }

            if (section == null)
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.NullSection, "属性存档段为空。");
            }

            if (!string.Equals(section.SectionId, SectionId, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.SectionIdMismatch,
                    $"属性存档段 ID 不匹配：expected={SectionId}, actual={section.SectionId}");
            }

            if (!string.Equals(section.Format, AttributeSectionFormat, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"属性存档段格式不支持：{section.Format}");
            }

            if (!string.Equals(section.DataEncoding, SaveDataEncoding.Base64, StringComparison.Ordinal))
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"属性存档段编码不支持：{section.DataEncoding}");
            }

            if (string.IsNullOrWhiteSpace(section.EncodedData))
            {
                return SaveSectionImportResult.Fail(SaveSectionImportErrorCode.DataCorrupted, "属性存档段数据为空。");
            }

            try
            {
                var readResult = TryReadAttributeSaveData(section, out var saveData);
                if (!readResult.Succeeded)
                {
                    return readResult;
                }

                var importResult = attributeController.ImportSnapshot(saveData);
                if (importResult == null || !importResult.Succeeded)
                {
                    return SaveSectionImportResult.Fail(
                        SaveSectionImportErrorCode.ImportFailed,
                        importResult != null ? importResult.Message : "属性控制器导入结果为空。");
                }

                LogMigrationWarningsAfterImport(importResult.Warnings);
                return SaveSectionImportResult.Success();
            }
            catch (Exception ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"属性存档段解析失败：{ex.Message}");
            }
        }

        private static SaveSectionImportResult TryReadAttributeSaveData(SaveSectionData section, out AttributeSaveData saveData)
        {
            saveData = null;
            switch (section.SectionVersion)
            {
                case AttributeSectionVersionV1:
                    return TryReadVersion1(section, out saveData);
                default:
                    return SaveSectionImportResult.Fail(
                        SaveSectionImportErrorCode.VersionUnsupported,
                        $"属性存档段版本不支持：{section.SectionVersion}");
            }
        }

        private static SaveSectionImportResult TryReadVersion1(SaveSectionData section, out AttributeSaveData saveData)
        {
            saveData = null;
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(section.EncodedData);
            }
            catch (FormatException ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"属性存档段 Base64 解码失败：{ex.Message}");
            }

            string json;
            try
            {
                json = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException ex)
            {
                return SaveSectionImportResult.Fail(
                    SaveSectionImportErrorCode.DataCorrupted,
                    $"属性存档段 UTF8 解码失败：{ex.Message}");
            }

            saveData = JsonUtility.FromJson<AttributeSaveData>(json);
            return ValidateImportedSaveData(saveData);
        }

        [ContextMenu("NiumaAttributeSave/注册到存档模块")]
        private void RegisterToSaveController()
        {
            if (_registeredToSaveController)
            {
                return;
            }

            ResolveReferences(true);
            if (saveController == null)
            {
                return;
            }

            var registered = saveController.RegisterProvider(this);
            _registeredToSaveController = registered;
            if (!registered)
            {
                Debug.LogWarning("[NiumaAttributeSaveAdapter] 注册属性存档 Provider 失败。", this);
            }
        }

        [ContextMenu("NiumaAttributeSave/从存档模块取消注册")]
        private void UnregisterFromSaveController()
        {
            ResolveReferences(false);
            if (_registeredToSaveController && saveController != null)
            {
                saveController.UnregisterProvider(SectionId);
            }

            _registeredToSaveController = false;
        }

        private void ResolveReferences(bool logMissing)
        {
            if (!autoFindReferences)
            {
                return;
            }

            if (attributeController == null)
            {
#if UNITY_2023_1_OR_NEWER
                attributeController = FindFirstObjectByType<NiumaAttributeController>();
#else
                attributeController = FindObjectOfType<NiumaAttributeController>();
#endif
            }

            if (saveController == null)
            {
#if UNITY_2023_1_OR_NEWER
                saveController = FindFirstObjectByType<NiumaSaveController>();
#else
                saveController = FindObjectOfType<NiumaSaveController>();
#endif
            }

            if (logMissing && attributeController == null)
            {
                Debug.LogWarning("[NiumaAttributeSaveAdapter] 未找到 NiumaAttributeController，请在 Inspector 中绑定。", this);
            }

            if (logMissing && saveController == null)
            {
                Debug.LogWarning("[NiumaAttributeSaveAdapter] 未找到 NiumaSaveController，请在 Inspector 中绑定。", this);
            }
        }

        private void LogMigrationWarningsAfterImport(string[] warnings)
        {
            if (!logMigrationWarningsAfterImport)
            {
                return;
            }

            for (var i = 0; warnings != null && i < warnings.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(warnings[i]))
                {
                    Debug.LogWarning($"[NiumaAttributeSaveAdapter] 属性存档迁移警告：{warnings[i]}", this);
                }
            }
        }

        private static void ValidateSaveDataForExport(AttributeSaveData saveData)
        {
            var error = ValidateSaveData(saveData);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException($"属性存档导出数据无效：{error}");
            }
        }

        private static SaveSectionImportResult ValidateImportedSaveData(AttributeSaveData saveData)
        {
            var error = ValidateSaveData(saveData);
            return string.IsNullOrWhiteSpace(error)
                ? SaveSectionImportResult.Success()
                : SaveSectionImportResult.Fail(SaveSectionImportErrorCode.DataCorrupted, $"属性存档段数据无效：{error}");
        }

        private static string ValidateSaveData(AttributeSaveData saveData)
        {
            if (saveData == null)
            {
                return "解析结果为空。";
            }

            if (saveData.Version != 1)
            {
                return $"版本字段无效：{saveData.Version}";
            }

            if (saveData.Revision < 0L)
            {
                return $"Revision 不能为负数：{saveData.Revision}";
            }

            if (saveData.Owners == null)
            {
                return "Owners 字段为空引用。";
            }

            var actorIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < saveData.Owners.Length; i++)
            {
                var owner = saveData.Owners[i];
                if (owner == null)
                {
                    return $"Owners[{i}] 为空。";
                }

                if (string.IsNullOrWhiteSpace(owner.ActorId))
                {
                    return $"Owners[{i}].ActorId 为空。";
                }

                if (!actorIds.Add(owner.ActorId))
                {
                    return $"重复 ActorId：{owner.ActorId}";
                }

                var error = ValidateOwner(owner);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    return $"ActorId={owner.ActorId}：{error}";
                }
            }

            return null;
        }

        private static string ValidateOwner(AttributeOwnerSnapshot owner)
        {
            if (owner.Attributes == null)
            {
                return "Attributes 字段为空引用。";
            }

            if (owner.Resources == null)
            {
                return "Resources 字段为空引用。";
            }

            if (owner.PersistentModifiers == null)
            {
                return "PersistentModifiers 字段为空引用。";
            }

            var attributeIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < owner.Attributes.Length; i++)
            {
                var attribute = owner.Attributes[i];
                if (attribute == null)
                {
                    return $"Attributes[{i}] 为空。";
                }

                if (string.IsNullOrWhiteSpace(attribute.AttributeId))
                {
                    return $"Attributes[{i}].AttributeId 为空。";
                }

                if (!attributeIds.Add(attribute.AttributeId))
                {
                    return $"重复 AttributeId：{attribute.AttributeId}";
                }

                if (!IsFinite(attribute.BaseValue))
                {
                    return $"AttributeId={attribute.AttributeId} 的 BaseValue 不是有效数字。";
                }
            }

            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < owner.Resources.Length; i++)
            {
                var resource = owner.Resources[i];
                if (resource == null)
                {
                    return $"Resources[{i}] 为空。";
                }

                if (string.IsNullOrWhiteSpace(resource.ResourceId))
                {
                    return $"Resources[{i}].ResourceId 为空。";
                }

                if (!resourceIds.Add(resource.ResourceId))
                {
                    return $"重复 ResourceId：{resource.ResourceId}";
                }

                if (!IsFinite(resource.CurrentValue))
                {
                    return $"ResourceId={resource.ResourceId} 的 CurrentValue 不是有效数字。";
                }
            }

            for (var i = 0; i < owner.PersistentModifiers.Length; i++)
            {
                var modifier = owner.PersistentModifiers[i];
                if (modifier == null)
                {
                    return $"PersistentModifiers[{i}] 为空。";
                }

                var error = ValidateModifier(modifier, i);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    return error;
                }
            }

            return null;
        }

        private static string ValidateModifier(AttributeModifierSnapshot modifier, int index)
        {
            if (string.IsNullOrWhiteSpace(modifier.SourceId))
            {
                return $"PersistentModifiers[{index}].SourceId 为空。";
            }

            if (string.IsNullOrWhiteSpace(modifier.ModifierId))
            {
                return $"PersistentModifiers[{index}].ModifierId 为空。";
            }

            if (string.IsNullOrWhiteSpace(modifier.TargetAttributeId))
            {
                return $"PersistentModifiers[{index}].TargetAttributeId 为空。";
            }

            if (modifier.Operation == AttributeModifierOperation.None)
            {
                return $"PersistentModifiers[{index}].Operation 不能为 None。";
            }

            if (modifier.Layer == AttributeModifierLayer.None)
            {
                return $"PersistentModifiers[{index}].Layer 不能为 None。";
            }

            if (!IsFinite(modifier.Value))
            {
                return $"PersistentModifiers[{index}].Value 不是有效数字。";
            }

            if (!IsFinite(modifier.DurationSeconds) || modifier.DurationSeconds < 0f)
            {
                return $"PersistentModifiers[{index}].DurationSeconds 无效。";
            }

            if (!IsFinite(modifier.RemainingSeconds) || modifier.RemainingSeconds < 0f)
            {
                return $"PersistentModifiers[{index}].RemainingSeconds 无效。";
            }

            return null;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
