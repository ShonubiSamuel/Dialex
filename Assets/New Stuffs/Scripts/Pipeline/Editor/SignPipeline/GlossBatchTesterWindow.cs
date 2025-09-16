#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YourApp.Signs.Pipeline.Scheduling;

/// <summary>
/// Tools → Sign Pipeline → Gloss Batch Tester
/// Paste text, optionally call your IGlossExtractor (Groq/OpenAI),
/// then normalize + resolve to see which clips will play.
/// If Play Mode & targets assigned, can call your scheduler/queue to play.
/// </summary>
public class GlossBatchTesterWindow : EditorWindow
{
    [TextArea(3,10)] [SerializeField] private string inputText;
    [SerializeField] private bool useExtractor = false;

    // Drag scene objects/components:
    [SerializeField] private MonoBehaviour glossExtractorBehaviour; // must implement IGlossExtractor
    [SerializeField] private SignResolver resolver;                 // for Contains() & path read
    [SerializeField] private GlossToSignScheduler scheduler;        // optional: to play sequence
    [SerializeField] private SignQueueController queue;             // or direct queue
    [SerializeField] private SignPlaybackController player;         // or direct player

    private List<string> _glosses = new();
    private List<Item> _items = new();
    private Vector2 _scroll;
    private bool _busy;

    private interface IExtractorShim
    {
        IEnumerator ExtractAsync(string english, Action<List<string>> onDone, Action<Exception> onError);
    }

    private class ExtractorAdapter : IExtractorShim
    {
        private readonly object _impl;
        public ExtractorAdapter(object impl) { _impl = impl; }

        public IEnumerator ExtractAsync(string english, Action<List<string>> onDone, Action<Exception> onError)
        {
            // Supports: YourApp.Signs.Pipeline.Gloss.IGlossExtractor  and  SignPipelineController.IGlossExtractor
            var t = _impl.GetType();
            var m = t.GetMethod("ExtractAsync");
            if (m == null) { onError?.Invoke(new MissingMethodException("No ExtractAsync on extractor.")); yield break; }

            // Kick the enumerator
            var enumerator = (IEnumerator)m.Invoke(_impl, new object[] { english, onDone, onError });
            while (enumerator != null && enumerator.MoveNext())
                yield return enumerator.Current;
        }
    }

    private struct Item
    {
        public string raw;
        public string key;
        public bool hasClip;
        public string path;
        public string note;
    }

    [MenuItem("Tools/Sign Pipeline/Gloss Batch Tester")]
    public static void Open() => GetWindow<GlossBatchTesterWindow>("Gloss Tester");

    private void OnGUI()
    {
        EditorGUILayout.Space();
        inputText = EditorGUILayout.TextArea(inputText ?? "", GUILayout.MinHeight(80));

        useExtractor = EditorGUILayout.ToggleLeft("Use Extractor (Groq/OpenAI) instead of heuristic split", useExtractor);

        EditorGUILayout.Space();
        glossExtractorBehaviour = (MonoBehaviour)EditorGUILayout.ObjectField("Extractor (scene)", glossExtractorBehaviour, typeof(MonoBehaviour), true);
        resolver = (SignResolver)EditorGUILayout.ObjectField("SignResolver (scene/prefab)", resolver, typeof(SignResolver), true);
        scheduler = (GlossToSignScheduler)EditorGUILayout.ObjectField("Scheduler (play mode)", scheduler, typeof(GlossToSignScheduler), true);
        queue = (SignQueueController)EditorGUILayout.ObjectField("QueueController (play mode)", queue, typeof(SignQueueController), true);
        player = (SignPlaybackController)EditorGUILayout.ObjectField("PlaybackController (play mode)", player, typeof(SignPlaybackController), true);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = !_busy;
            if (GUILayout.Button("Analyze", GUILayout.Height(28))) StartAnalyze();
            GUI.enabled = _items.Count > 0 && Application.isPlaying;
            if (GUILayout.Button("Play In Scene", GUILayout.Height(28))) PlayInScene();
            GUI.enabled = true;
        }

        EditorGUILayout.Space();
        if (_busy) EditorGUILayout.HelpBox("Running…", MessageType.Info);

        if (_items.Count > 0)
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var it in _items)
            {
                var msg = it.hasClip ? "OK" : "MISSING";
                var type = it.hasClip ? MessageType.Info : MessageType.Error;
                using (new GUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField($"Gloss: {it.raw}");
                    EditorGUILayout.LabelField($"Key:   {it.key}");
                    EditorGUILayout.HelpBox($"{msg}  •  {it.note}", type);
                    EditorGUILayout.LabelField("Path/Address", it.path ?? "—");
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void StartAnalyze()
    {
        _items.Clear();
        _glosses.Clear();

        if (string.IsNullOrWhiteSpace(inputText))
        {
            EditorUtility.DisplayDialog("Gloss Tester", "Paste some text first.", "OK");
            return;
        }

        _busy = true;

        if (useExtractor && glossExtractorBehaviour != null)
        {
            var shim = new ExtractorAdapter(glossExtractorBehaviour);
            RunEditorCoroutine(shim.ExtractAsync(
                inputText,
                onDone: list =>
                {
                    _glosses = list ?? new List<string>();
                    BuildItems();
                    _busy = false; Repaint();
                },
                onError: ex =>
                {
                    Debug.LogError(ex);
                    _busy = false; Repaint();
                }));
        }
        else
        {
            // Heuristic: split by whitespace/commas; post-process with your normalizer
            var raw = inputText.Replace("\n", " ").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in raw) _glosses.Add(token);
            _glosses = YourApp.Signs.Pipeline.Gloss.GlossPostProcessor.Process(_glosses);
            BuildItems();
            _busy = false;
        }
    }

    private void BuildItems()
    {
        foreach (var g in _glosses)
        {
            var key = GlossNormalizer.Normalize(g);
            var item = new Item { raw = g, key = key, hasClip = false, note = "" };

            if (resolver != null)
            {
                item.hasClip = resolver.Contains(key);
                if (resolver.treatMapPathAsAddressableKey && resolver.TryGetAddressableKey(key, out var addr))
                {
                    item.path = addr;
                    item.note = item.hasClip ? "Addressables key" : "Not found in map";
                }
                else if (resolver.TryGetResourcesKey(key, out var resKey))
                {
                    item.path = resKey;
                    item.note = item.hasClip ? "Resources key" : "Not found in map";
                }
                else
                {
                    item.note = "Key not present in resolver map.";
                }
            }
            else
            {
                item.note = "No resolver provided; showing normalized keys only.";
            }

            _items.Add(item);
        }
    }

    private void PlayInScene()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Play In Scene", "Enter Play Mode first.", "OK");
            return;
        }

        var keys = new List<string>();
        foreach (var it in _items) if (it.hasClip) keys.Add(it.key);

        if (keys.Count == 0)
        {
            EditorUtility.DisplayDialog("Play In Scene", "No resolvable keys.", "OK");
            return;
        }

        if (scheduler != null)
        {
            scheduler.ScheduleFromGlosses(keys);
        }
        else if (queue != null)
        {
            queue.PlaySequenceFromKeys(keys);
        }
        else if (player != null)
        {
            foreach (var k in keys) player.Enqueue(k);
        }
        else
        {
            EditorUtility.DisplayDialog("Play In Scene", "Assign Scheduler / Queue / Player.", "OK");
        }
    }

    // --- tiny editor coroutine runner (no package needed) ---
    private static readonly List<IEnumerator> _editorCoroutines = new();
    private static bool _hooked;

    private static void RunEditorCoroutine(IEnumerator e)
    {
        _editorCoroutines.Add(e);
        if (_hooked) return;
        _hooked = true;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        for (int i = _editorCoroutines.Count - 1; i >= 0; i--)
        {
            var it = _editorCoroutines[i];
            if (!it.MoveNext()) _editorCoroutines.RemoveAt(i);
        }
        if (_editorCoroutines.Count == 0)
        {
            EditorApplication.update -= Tick;
            _hooked = false;
        }
    }
}
#endif
