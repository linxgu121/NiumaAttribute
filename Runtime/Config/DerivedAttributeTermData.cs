using System;
using UnityEngine;

namespace NiumaAttribute.Config
{
    /// <summary>
    /// 派生属性计算项。
    /// 表示当前属性从某个来源属性读取数值并乘以系数。
    /// </summary>
    [Serializable]
    public sealed class DerivedAttributeTermData
    {
        [Tooltip("参与计算的来源属性 ID。例如 str、dex、con。")]
        public string SourceAttributeId;

        [Tooltip("来源属性系数。例如 2 表示 SourceAttribute * 2。")]
        public float Coefficient = 1f;
    }
}
