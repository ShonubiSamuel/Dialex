using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;
using Newtonsoft.Json;

public class ThankYouMultiPlayer : MonoBehaviour
{
    [Header("Folder under StreamingAssets")]
    public string subDir = "islr_replay_all";

    [Header("Playback")]
    public float fps = 30f;         // playback speed
    public float sphereSize = 0.015f;
    public float zDepth = 1.8f;     // distance in front of camera
    public bool loopClip = true;

    [Header("Optional model run (off by default)")]
    public bool runModel = false;
    public ModelAsset modelAsset;
    public BackendType backend = BackendType.CPU;

    // landmark layout
    const int FACE_START = 0,   FACE_CNT = 468;
    const int LH_START   = 468, LH_CNT   = 21;
    const int POSE_START = 489, POSE_CNT = 33;
    const int RH_START   = 522, RH_CNT   = 21;
    const int N = 543, C = 3;

    // common shape (from shape.json)
    int B = 1, T = 384;

    // current clip data
    float[] data;          // [B*T*N*C] float32 from .bin
    int T_orig = 0;        // original frames (for nicer loop)
    int frame = 0;
    float tick = 0f;

    // spheres
    Transform[] leftHand, rightHand, faceDots;

    // multi files
    class Index { public List<Item> items; }
    class Item { public string stem; public string bin; public string info; }
    class Shape { public int B, T, N, C; }
    class Info  { public string label; public long participant_id; public long sequence_id; public string path; public int T_orig; }

    List<Item> items;
    int clipIdx = 0;
    Camera cam;

    // (optional) model
    Worker worker;
    Tensor<float> input;

    void Awake()
    {
        cam = Camera.main;

        string basePath = Path.Combine(Application.streamingAssetsPath, subDir);
        // shape
        var shp = JsonConvert.DeserializeObject<Shape>(File.ReadAllText(Path.Combine(basePath, "shape.json")));
        B = shp.B; T = shp.T;
        // index
        var idx = JsonConvert.DeserializeObject<Index>(File.ReadAllText(Path.Combine(basePath, "index.json")));
        items = idx.items;
        if (items == null || items.Count == 0) { Debug.LogError("No items in index.json"); enabled = false; return; }

        // build spheres once
        leftHand  = BuildSpheres("LeftHand",  LH_CNT, Color.green);
        rightHand = BuildSpheres("RightHand", RH_CNT, new Color(1f, 0.2f, 1f));
        faceDots  = BuildSpheres("FaceDots",  6, Color.cyan); // a few anchors

        // optional model
        if (runModel && modelAsset != null)
        {
            var model = ModelLoader.Load(modelAsset);
            worker = new Worker(model, backend);
            input  = new Tensor<float>(new TensorShape(B, T, N, C));
        }

        LoadClip(clipIdx);
    }

    Transform[] BuildSpheres(string groupName, int count, Color color)
    {
        var parent = new GameObject(groupName).transform;
        parent.SetParent(transform);
        var arr = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            s.name = $"{groupName}_{i}";
            s.SetParent(parent);
            s.localScale = Vector3.one * sphereSize;
            var mr = s.GetComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mr.sharedMaterial.color = color;
            arr[i] = s;
        }
        return arr;
    }

    void LoadClip(int idx)
    {
        string basePath = Path.Combine(Application.streamingAssetsPath, subDir);
        var it = items[Mathf.Clamp(idx, 0, items.Count - 1)];

        // read bin
        var raw = File.ReadAllBytes(Path.Combine(basePath, it.bin));
        data = new float[raw.Length / 4];
        Buffer.BlockCopy(raw, 0, data, 0, raw.Length);

        // read info (for label + T_orig)
        var info = JsonConvert.DeserializeObject<Info>(File.ReadAllText(Path.Combine(basePath, it.info)));
        T_orig = Mathf.Clamp(info.T_orig, 1, T);  // safety
        frame = 0; tick = 0f;

        Debug.Log($"Loaded clip {idx+1}/{items.Count}: label='{info.label}', seq={info.sequence_id}, T_orig={T_orig}");
    }

    void Update()
    {
        HandleKeys();

        if (data == null) return;

        // step timeline
        tick += Time.deltaTime;
        float dt = 1f / Mathf.Max(1, fps);
        while (tick >= dt)
        {
            tick -= dt;
            frame++;
            if (frame >= T_orig)
            {
                if (loopClip) frame = 0;
                else frame = T_orig - 1;
            }
        }

        // update spheres for current frame
        int off = frame * N * C;
        // left hand (21)
        for (int i = 0; i < LH_CNT; i++)
            MoveSphere(leftHand[i], off, LH_START + i);
        // right hand (21)
        for (int i = 0; i < RH_CNT; i++)
            MoveSphere(rightHand[i], off, RH_START + i);
        // a few face anchors (help orientation)
        int[] faceIDs = {0, 61, 291, 13, 17, 199};
        for (int i = 0; i < faceDots.Length; i++)
            MoveSphere(faceDots[i], off, FACE_START + faceIDs[i]);

        // (optional) run model on whole window (once per frame for demo)
        if (runModel && worker != null)
        {
            input.Upload(data); // full [1,T,543,3]
            worker.Schedule(input);
            var o = worker.PeekOutput() as Tensor<float>;
            var logits = o.DownloadToArray();
            // you can compute top-k here if you want
        }
    }

    void MoveSphere(Transform t, int off, int idx)
    {
        int p = off + idx * C;
        if (p < 0 || p + 2 >= data.Length) return;

        float x = data[p + 0];
        float y = data[p + 1];
        float z = data[p + 2];

        // guard against NaN/Inf from missing detections
        if (float.IsNaN(x) || float.IsInfinity(x) ||
            float.IsNaN(y) || float.IsInfinity(y))
        {
            // Option A: hide the point when it's invalid
            t.gameObject.SetActive(false);
            return;
            // Option B (alternative): place at center instead of hiding
            // x = 0.5f; y = 0.5f;
        }
        else
        {
            t.gameObject.SetActive(true);
        }

        // Clamp to viewport range and flip Y (MediaPipe origin is top-left)
        x = Mathf.Clamp01(x);
        y = Mathf.Clamp01(y);

        // Ensure we have a camera
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Keep zDepth within clip planes
        float zn = Mathf.Clamp(zDepth, cam.nearClipPlane + 0.01f, cam.farClipPlane - 0.01f);

        var vp = new Vector3(x, 1f - y, zn);
        var world = cam.ViewportToWorldPoint(vp);
        t.position = world;
    }

    void HandleKeys()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            clipIdx = (clipIdx + 1) % items.Count;
            LoadClip(clipIdx);
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            clipIdx = (clipIdx - 1 + items.Count) % items.Count;
            LoadClip(clipIdx);
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // toggle pause by freezing tick advance
            if (Mathf.Approximately(fps, 0f)) fps = 30f; else fps = 0f;
        }
    }

    void OnDestroy()
    {
        input?.Dispose();
        worker?.Dispose();
    }
}