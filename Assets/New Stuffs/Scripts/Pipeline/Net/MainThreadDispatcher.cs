using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YourApp.Signs.Pipeline.Net
{
    /// <summary>
    /// Minimal main-thread dispatcher: enqueue actions/coroutines from any context,
    /// they’ll run on Update() safely.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private readonly Queue<Action> _actions = new Queue<Action>();
        private readonly Queue<IEnumerator> _coroutines = new Queue<IEnumerator>();
        private int _mainThreadId;

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[MainThreadDispatcher]");
                    _instance = go.AddComponent<MainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            { Destroy(gameObject); return; }
            _instance = this;
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            lock (_actions)
                while (_actions.Count > 0) _actions.Dequeue()?.Invoke();

            lock (_coroutines)
                while (_coroutines.Count > 0) StartCoroutine(_coroutines.Dequeue());
        }

        public static void Post(Action a)
        {
            if (a == null) return;
            if (IsMainThread)
            {
                a();
            }
            else
            {
                lock (Instance._actions) Instance._actions.Enqueue(a);
            }
        }

        public static void PostCoroutine(IEnumerator routine)
        {
            if (routine == null) return;
            if (IsMainThread)
            {
                Instance.StartCoroutine(routine);
            }
            else
            {
                lock (Instance._coroutines) Instance._coroutines.Enqueue(routine);
            }
        }

        public static bool IsMainThread =>
            _instance != null &&
            System.Threading.Thread.CurrentThread.ManagedThreadId == _instance._mainThreadId;
    }
}
