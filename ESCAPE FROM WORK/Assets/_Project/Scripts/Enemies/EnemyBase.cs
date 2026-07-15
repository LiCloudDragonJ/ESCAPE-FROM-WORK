using UnityEngine;
using EscapeFromWork.Core;
using EscapeFromWork.Data;

namespace EscapeFromWork.Enemies
{
    /// <summary>
    /// Core state machine for all enemy AI. Idle, Patrol, Chase, and Attack states
    /// drive behaviour each frame. Dead state suppresses all AI updates.
    /// </summary>
    public enum EnemyState
    {
        /// <summary>Standing still — no active behaviour.</summary>
        Idle,

        /// <summary>Wandering within the assigned patrol area.</summary>
        Patrol,

        /// <summary>Moving toward a known target (player or taunt source).</summary>
        Chase,

        /// <summary>Within attack range and firing attacks on cooldown.</summary>
        Attack,

        /// <summary>Health depleted; AI disabled, death sequence playing.</summary>
        Dead
    }

    /// <summary>
    /// Abstract base for every enemy in the game. Provides the state machine,
    /// movement (XZ-plane only), attack cooldown gating, status-effect management,
    /// loot-drop logic, and the <see cref="IDamageable"/> contract.
    ///
    /// <para>Concrete subclasses must implement <see cref="PatrolBehavior"/> and
    /// <see cref="PerformAttack"/>; they may also override <see cref="ChaseBehavior"/>,
    /// <see cref="AttackBehavior"/>, and <see cref="Die"/>.</para>
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable
    {
        // ---- Inspector-assigned data --------------------------------------------

        /// <summary>ScriptableObject defining stats, perception, and loot for this enemy.</summary>
        [SerializeField] protected EnemyData data;

        // ---- State --------------------------------------------------------------

        /// <summary>Current AI state.</summary>
        protected EnemyState _state;

        /// <summary>
        /// Current pursuit / attack target. Set by <see cref="SetTarget"/>,
        /// <see cref="OnTriggerEnter"/>, or taunt effects.
        /// </summary>
        protected Transform _target;

        /// <summary>Current health; clamped to [0, maxHealth].</summary>
        protected float _currentHealth;

        /// <summary>Last <see cref="Time.time"/> value at which an attack was performed.</summary>
        protected float _lastAttackTime;

        // ---- Status effects -----------------------------------------------------

        /// <summary>True when the enemy cannot see the player (blind effect active).</summary>
        protected bool _isBlinded;

        /// <summary>Remaining duration of the blind effect in seconds.</summary>
        protected float _blindTimer;

        /// <summary>True when the enemy cannot move (root effect active).</summary>
        protected bool _isRooted;

        /// <summary>Remaining duration of the root effect in seconds.</summary>
        protected float _rootTimer;

        /// <summary>True when the enemy is forced to target the taunt source.</summary>
        protected bool _isTaunted;

        /// <summary>Remaining duration of the taunt effect in seconds.</summary>
        protected float _tauntTimer;

        // ---- Convenience data accessors -----------------------------------------

        /// <summary>Effective max health, falling back to 100 if data is unassigned.</summary>
        protected float MaxHealth => data != null ? data.MaxHealth : 100f;

        /// <summary>Effective movement speed in world units per second.</summary>
        protected float MoveSpeed => data != null ? data.MoveSpeed : 3f;

        /// <summary>Effective attack range in world units.</summary>
        protected float AttackRange => data != null ? data.AttackRange : 1.5f;

        /// <summary>Minimum seconds between consecutive attacks.</summary>
        protected float AttackCooldown => data != null ? data.AttackCooldown : 1.5f;

        /// <summary>Radius at which the enemy becomes aware of the player.</summary>
        protected float DetectionRange => data != null ? data.DetectionRange : 15f;

        /// <summary>True while the enemy is alive (state is not Dead).</summary>
        protected bool IsAlive => _state != EnemyState.Dead;

        // ---- Unity lifecycle ----------------------------------------------------

        /// <summary>
        /// Initialise health to max and start patrolling.
        /// Subclasses that override this must call base.Awake().
        /// </summary>
        protected virtual void Awake()
        {
            _currentHealth = MaxHealth;
            _state = EnemyState.Patrol;
        }

        /// <summary>
        /// Per-frame AI tick. Expires status effects first, then delegates to the
        /// active state behaviour. Dead enemies skip all processing.
        /// </summary>
        protected virtual void Update()
        {
            UpdateStatusEffects();

            if (_state == EnemyState.Dead)
                return;

            switch (_state)
            {
                case EnemyState.Idle:
                    // Stand still; transitions handled by subclasses or external triggers.
                    break;

                case EnemyState.Patrol:
                    PatrolBehavior();
                    break;

                case EnemyState.Chase:
                    ChaseBehavior();
                    break;

                case EnemyState.Attack:
                    AttackBehavior();
                    break;
            }
        }

        // ---- Abstract behaviours ------------------------------------------------

        /// <summary>
        /// Called every frame while <see cref="_state"/> is <see cref="EnemyState.Patrol"/>.
        /// Implementations should define wandering / idle-wait logic.
        /// </summary>
        protected abstract void PatrolBehavior();

        /// <summary>
        /// Execute one attack against the current target. Called from
        /// <see cref="AttackBehavior"/> when cooldown has elapsed and the target is
        /// within <see cref="AttackRange"/>.
        /// </summary>
        protected abstract void PerformAttack();

        // ---- Default Chase / Attack behaviours ----------------------------------

        /// <summary>
        /// Move toward <see cref="_target"/> on the XZ plane. Transitions to
        /// <see cref="EnemyState.Attack"/> when within range, or falls back to
        /// <see cref="EnemyState.Patrol"/> when the target is lost.
        /// </summary>
        protected virtual void ChaseBehavior()
        {
            if (_target == null)
            {
                _state = EnemyState.Patrol;
                return;
            }

            float dist = DistanceToTarget();
            if (dist <= AttackRange)
            {
                _state = EnemyState.Attack;
            }
            else if (!_isRooted)
            {
                MoveToward(_target.position);
            }
        }

        /// <summary>
        /// Stay within attack range of <see cref="_target"/> and fire
        /// <see cref="PerformAttack"/> on cooldown. Falls back to
        /// <see cref="EnemyState.Chase"/> if the target moves out of range or
        /// <see cref="EnemyState.Patrol"/> if the target is lost entirely.
        /// </summary>
        protected virtual void AttackBehavior()
        {
            if (_target == null)
            {
                _state = EnemyState.Patrol;
                return;
            }

            float dist = DistanceToTarget();
            if (dist > AttackRange)
            {
                _state = EnemyState.Chase;
                return;
            }

            // Face the target on the XZ plane.
            FaceTarget();

            if (Time.time >= _lastAttackTime + AttackCooldown)
            {
                PerformAttack();
                _lastAttackTime = Time.time;
            }
        }

        // ---- IDamageable ---------------------------------------------------------

        /// <summary>
        /// Apply damage to this enemy. Triggers <see cref="Die"/> when health
        /// reaches zero. Already-dead enemies ignore all damage.
        /// </summary>
        /// <param name="damage">Raw damage value before resistance calculations.</param>
        /// <param name="source">The GameObject that originated the damage (projectile, melee swing, etc.). Optional.</param>
        public virtual void TakeDamage(float damage, GameObject source = null)
        {
            if (_state == EnemyState.Dead)
                return;

            _currentHealth = Mathf.Max(0f, _currentHealth - damage);

            // If blind and not already chasing, set the damage source as target.
            if (!_isBlinded && source != null && _state != EnemyState.Chase && _state != EnemyState.Attack)
            {
                SetTarget(source.transform);
            }

            if (_currentHealth <= 0f)
            {
                _state = EnemyState.Dead;
                Die();
            }
        }

        // ---- Death & loot -------------------------------------------------------

        /// <summary>
        /// Handle death: spawn the guaranteed 工牌 (work-badge) drop, roll each
        /// <see cref="EnemyData.PossibleDrops"/> entry against
        /// <see cref="EnemyData.DropChance"/>, and destroy this GameObject after
        /// a short delay.
        ///
        /// <para>Subclasses can override to add death VFX / SFX; they should call
        /// base.Die() to preserve loot logic.</para>
        /// </summary>
        protected virtual void Die()
        {
            if (data == null)
            {
                Destroy(gameObject, 0.5f);
                return;
            }

            // -- Guaranteed drop: 工牌 (work badge) --
            SpawnWorldDrop(data.GuaranteedDrop, transform.position);

            // -- Random drops --
            if (data.PossibleDrops != null)
            {
                foreach (ItemData item in data.PossibleDrops)
                {
                    if (item != null && Random.value <= data.DropChance)
                    {
                        // Scatter random drops in a small radius around the death point.
                        Vector3 scatterPos = transform.position
                            + new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
                        SpawnWorldDrop(item, scatterPos);
                    }
                }
            }

            // -- Destroy after delay so death effects / ragdolls can play --
            Destroy(gameObject, 2f);
        }

        /// <summary>
        /// Spawn a placeholder world object for the given <see cref="ItemData"/>.
        /// Replaced by the full loot-pickup system (Task 7+); currently creates a
        /// small cube so the drop is visible and position-tagged.
        /// </summary>
        /// <param name="item">The item to represent in the world.</param>
        /// <param name="worldPosition">World-space spawn position.</param>
        private void SpawnWorldDrop(ItemData item, Vector3 worldPosition)
        {
            if (item == null)
                return;

            // Placeholder visual — replace with a LootPickup prefab once available.
            GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            drop.transform.position = worldPosition + Vector3.up * 0.5f;
            drop.transform.localScale = Vector3.one * 0.25f;
            drop.name = $"工牌_{item.ItemName}";
            drop.tag = "Loot";

            // Ensure the placeholder has a collider for future pickup interaction.
            Collider col = drop.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        // ---- Movement helpers ---------------------------------------------------

        /// <summary>
        /// Move this enemy toward a world-space point on the XZ plane (Y is locked
        /// to zero). Blinded enemies skip this call in <see cref="ChaseBehavior"/>.
        /// </summary>
        /// <param name="target">World-space destination point.</param>
        protected void MoveToward(Vector3 target)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude <= 0.001f)
                return;

            dir.Normalize();
            transform.position += dir * MoveSpeed * Time.deltaTime;
        }

        /// <summary>
        /// Rotate to face the current target on the XZ plane.
        /// </summary>
        protected void FaceTarget()
        {
            if (_target == null)
                return;

            Vector3 dir = _target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        /// <summary>
        /// Horizontal (XZ-plane only) distance to <see cref="_target"/>.
        /// Returns <see cref="float.MaxValue"/> when there is no target.
        /// </summary>
        protected float DistanceToTarget()
        {
            if (_target == null)
                return float.MaxValue;

            Vector3 a = transform.position;
            a.y = 0f;
            Vector3 b = _target.position;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// Assign a new pursuit target and immediately transition to
        /// <see cref="EnemyState.Chase"/>. Pass null to clear the target and
        /// return to <see cref="EnemyState.Patrol"/>.
        /// </summary>
        /// <param name="t">The Transform to pursue, or null.</param>
        public void SetTarget(Transform t)
        {
            _target = t;

            if (t != null)
            {
                _state = EnemyState.Chase;
            }
            else
            {
                _state = EnemyState.Patrol;
            }
        }

        // ---- Status effects (public API) ----------------------------------------

        /// <summary>
        /// Apply a blind effect. A blinded enemy cannot detect new targets through
        /// vision and will not chase.
        /// </summary>
        /// <param name="duration">Effect duration in seconds. Stacks by replacing the current timer if the new duration is longer.</param>
        public void ApplyBlind(float duration)
        {
            _isBlinded = true;
            if (duration > _blindTimer)
                _blindTimer = duration;
        }

        /// <summary>
        /// Apply a root effect. A rooted enemy cannot move but can still attack
        /// if a target is within range.
        /// </summary>
        /// <param name="duration">Effect duration in seconds. Stacks by replacing the current timer if the new duration is longer.</param>
        public void ApplyRoot(float duration)
        {
            _isRooted = true;
            if (duration > _rootTimer)
                _rootTimer = duration;
        }

        /// <summary>
        /// Apply a taunt effect. A taunted enemy is forced to target the taunt
        /// source and will ignore the player for the duration.
        /// </summary>
        /// <param name="duration">Effect duration in seconds. Stacks by replacing the current timer if the new duration is longer.</param>
        public void ApplyTaunt(float duration)
        {
            _isTaunted = true;
            if (duration > _tauntTimer)
                _tauntTimer = duration;
        }

        // ---- Status effects (internal) ------------------------------------------

        /// <summary>
        /// Decrement all status-effect timers each frame and clear any that have
        /// expired. Called at the top of <see cref="Update"/> before AI processing.
        /// </summary>
        private void UpdateStatusEffects()
        {
            float dt = Time.deltaTime;

            if (_isBlinded)
            {
                _blindTimer -= dt;
                if (_blindTimer <= 0f)
                {
                    _isBlinded = false;
                    _blindTimer = 0f;
                }
            }

            if (_isRooted)
            {
                _rootTimer -= dt;
                if (_rootTimer <= 0f)
                {
                    _isRooted = false;
                    _rootTimer = 0f;
                }
            }

            if (_isTaunted)
            {
                _tauntTimer -= dt;
                if (_tauntTimer <= 0f)
                {
                    _isTaunted = false;
                    _tauntTimer = 0f;
                }
            }
        }

        // ---- Trigger detection --------------------------------------------------

        /// <summary>
        /// When a Player enters the enemy's trigger volume, immediately set them
        /// as the pursuit target and transition to Chase. Already-dead enemies
        /// and blinded enemies ignore the trigger.
        /// </summary>
        protected virtual void OnTriggerEnter(Collider other)
        {
            if (_state == EnemyState.Dead)
                return;

            if (_isBlinded)
                return;

            if (other.CompareTag("Player"))
            {
                _target = other.transform;
                _state = EnemyState.Chase;
            }
        }
    }
}
