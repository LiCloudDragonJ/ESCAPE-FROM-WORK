using UnityEngine;

namespace EscapeFromWork.Core
{
    public enum StatusEffectType
    {
        Blind,
        Root,
        Slow,
        Taunt,
        DamageOverTime,
        Buff
    }

    [System.Serializable]
    public struct ActiveStatusEffect
    {
        public StatusEffectType type;
        public float remainingDuration;
        public float totalDuration;
        public float magnitude;
        public GameObject source;
        public bool isBuff;

        public float Progress => totalDuration > 0f ? 1f - (remainingDuration / totalDuration) : 1f;
        public bool IsExpired => remainingDuration <= 0f;
    }

    [System.Serializable]
    public struct StatusEffectData
    {
        public StatusEffectType type;
        public float duration;
        public float magnitude;
        public bool isBuff;
    }
}
