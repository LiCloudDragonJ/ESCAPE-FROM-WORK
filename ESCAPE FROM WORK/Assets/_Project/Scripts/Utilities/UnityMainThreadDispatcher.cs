using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromWork.Utilities
{
    /// <summary>
    /// 线程安全的主线程调度器 — 允许在异步回调/后台线程中安全地执行 Unity API 调用。
    /// 搭配 UniTask 使用效果最佳。
    /// </summary>
    /// <remarks>
    /// 用法:
    ///   UnityMainThreadDispatcher.Instance.Enqueue(() => Instantiate(prefab));
    /// 或在 UniTask 中:
    ///   await UniTask.SwitchToMainThread();
    ///   // 现在已在主线程，直接调用 Unity API
    /// </remarks>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                    Debug.LogError("[UnityMainThreadDispatcher] 未找到实例。请确保场景中存在带此组件的 GameObject。");
                return _instance;
            }
        }

        private readonly Queue<Action> _executionQueue = new Queue<Action>();
        private readonly Queue<Action> _executionQueueCopy = new Queue<Action>();
        private bool _hasPendingActions = false;

        [SerializeField, Tooltip("每秒最多执行的操作数，0 = 无限制")]
        private int maxActionsPerFrame = 0;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[UnityMainThreadDispatcher] 存在多个实例，销毁重复项。");
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// 将操作排入主线程执行队列。
        /// </summary>
        public void Enqueue(Action action)
        {
            if (action == null) return;

            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
                _hasPendingActions = true;
            }
        }

        /// <summary>
        /// 将 IEnumerator 包装后排入主线程执行。
        /// </summary>
        public void Enqueue(IEnumerator routine)
        {
            Enqueue(() => StartCoroutine(routine));
        }

        void Update()
        {
            if (!_hasPendingActions) return;

            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                    _executionQueueCopy.Enqueue(_executionQueue.Dequeue());
                _hasPendingActions = _executionQueue.Count > 0;
            }

            int count = 0;
            while (_executionQueueCopy.Count > 0)
            {
                var action = _executionQueueCopy.Dequeue();
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UnityMainThreadDispatcher] 操作执行异常: {ex}");
                }

                count++;
                if (maxActionsPerFrame > 0 && count >= maxActionsPerFrame)
                    break;
            }
        }
    }
}
