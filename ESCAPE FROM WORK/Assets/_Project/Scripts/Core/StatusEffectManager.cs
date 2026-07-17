using System.Collections.Generic;
using UnityEngine;
using EscapeFromWork.Player;

namespace EscapeFromWork.Core
{
    /// <summary>
    /// Manages status effects on a character. Attach to any GameObject
    /// that can receive blind/root/slow/taunt/buff/DOT effects.
    /// </summary>
    public class StatusEffectManager : MonoBehaviour
    {
        [SerializeField] private float blindAutoAimRangeReduction = 0.3f;

        private readonly List<ActiveStatusEffect> _activeEffects = new();
        private PlayerController _playerController;
        private PlayerCombat _playerCombat;

        public bool IsBlinded => HasEffect(StatusEffectType.Blind);
        public bool IsRooted => HasEffect(StatusEffectType.Root);
        public bool IsSlowed => HasEffect(StatusEffectType.Slow);
        public float SlowMultiplier => GetEffectMagnitude(StatusEffectType.Slow);
        public float BuffMultiplier => GetEffectMagnitude(StatusEffectType.Buff);
        public int ActiveEffectCount => _activeEffects.Count;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _playerCombat = GetComponent<PlayerCombat>();
        }

        private void Update()
        {
            TickEffects(Time.deltaTime);
        }

        public void Apply(StatusEffectType type, float duration, float magnitude, GameObject source = null, bool isBuff = false)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].type == type)
                {
                    var e = _activeEffects[i];
                    e.remainingDuration = Mathf.Max(e.remainingDuration, duration);
                    e.magnitude = magnitude;
                    e.source = source;
                    _activeEffects[i] = e;
                    return;
                }
            }

            _activeEffects.Add(new ActiveStatusEffect
            {
                type = type, remainingDuration = duration,
                totalDuration = duration, magnitude = magnitude,
                source = source, isBuff = isBuff
            });
        }

        public void Remove(StatusEffectType type) => _activeEffects.RemoveAll(e => e.type == type);
        public void ClearAll() => _activeEffects.Clear();

        public bool HasEffect(StatusEffectType type)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
                if (_activeEffects[i].type == type) return true;
            return false;
        }

        public float GetEffectMagnitude(StatusEffectType type)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
                if (_activeEffects[i].type == type) return _activeEffects[i].magnitude;
            return 0f;
        }

        public float GetModifiedMoveSpeed(float baseSpeed)
        {
            float speed = baseSpeed;
            if (IsSlowed) speed *= (1f - SlowMultiplier);
            if (IsRooted) speed = 0f;
            if (HasEffect(StatusEffectType.Buff)) speed *= (1f + BuffMultiplier);
            return Mathf.Max(0f, speed);
        }

        public float GetModifiedAutoAimRange(float baseRange)
            => IsBlinded ? baseRange * blindAutoAimRangeReduction : baseRange;

        private void TickEffects(float deltaTime)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var e = _activeEffects[i];
                e.remainingDuration -= deltaTime;

                if (e.type == StatusEffectType.DamageOverTime && e.remainingDuration > 0f)
                {
                    var health = GetComponent<PlayerHealth>();
                    if (health != null) health.TakeDamage(e.magnitude * deltaTime, e.source);
                }

                if (e.IsExpired) _activeEffects.RemoveAt(i);
                else _activeEffects[i] = e;
            }
        }
    }
}
