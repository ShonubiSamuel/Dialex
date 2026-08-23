# Dialex

A bidirectional sign-language translator in Unity. It reads American Sign Language from a camera and turns it into text, and it turns text or speech back into signed 3D animation — both directions in one runtime.

**Unity (C#) · MediaPipe holistic landmarks · ONNX via Unity Inference Engine · TensorFlow Lite**

---

## Why both directions is the hard part

Most sign-language projects pick one direction. Recognition alone gives you a classifier; production alone gives you an animation player. Doing both in one runtime means the two halves have to agree on a shared vocabulary — and that vocabulary is the real engineering problem, because a model emits *glosses* (`ASK_OUT`) while an animation library is a folder of files (`Ask Out.fbx`), and neither is a stable key.

Dialex resolves this with a normalization layer that both halves route through:

```
"  Ásk-out!!  "  ──GlossNormalizer──▶  "ask_out"  ──SignRegistry──▶  animation clip
model output "ASK_OUT" ─────────────────────┘
```

`GlossNormalizer` (Unicode-folding, punctuation-stripping, case-normalizing) produces the same key from a model's output, a user's typed text, and a filename on disk. `SignMap.json` / `SignMap_List.json` hold the lexicon, and `SignMapExporter` regenerates them from the animation library so the map can never silently drift from the assets it points at.

## Pipelines

### Recognition — camera → text

```
Camera → MediaPipe holistic (hand + pose landmarks)
       → ISLRPreprocess          normalize/window the landmark sequence
       → AslPredictor            ONNX inference (Unity Inference Engine)
         └ SignPredictorTFLite   alternate TFLite backend
       → SignPrediction          temporal smoothing, confidence gating → gloss
```

Two inference backends exist deliberately: `AslPredictor` runs `model_simplified.onnx` through Unity's Inference Engine, while `SignPredictorTFLite` runs `hand_sign_recognizer.bytes` through TFLite. `ISLROfflineReplay` re-runs a recorded landmark sequence through the same path, so a classification change can be evaluated without standing in front of a camera.

### Production — text/speech → signed animation

```
text or speech → GlossNormalizer → SignRegistry / SignMapProvider
               → SignQueueController      sequence, dedupe, raise lifecycle events
               → SignPlaybackController    blend clips, hold, transition
               → 3D signer (FBX clips)
```

`SignQueueController` accepts a list of raw glosses or normalized keys and emits sequence-start / per-item / complete / cancel events, so UI can track progress without polling. `SignPlaybackController` is the largest single component — clip resolution, blending and timing between consecutive signs is where fluency actually comes from.

### Speech input

`YorubaTranscribeClient` posts recorded audio to a transcription service and feeds the result into the production pipeline — so the spoken-language side isn't limited to English. `TimeDilationController` slows playback for learners.

## Layout

| Path | What |
|---|---|
| `Assets/New Stuffs/Scripts/` | The engine: inference, preprocessing, registry, queue, playback |
| `Assets/Editor/` | `SignMapExporter`, `SignSelectionExtractor` — regenerate the lexicon from assets |
| `Assets/Signs/` | The sign animation library (the bulk of the repository) |
| `Assets/StreamingAssets/` | MediaPipe landmarker models |
| `Assets/SignMap.json`, `SignMap_List.json` | Gloss → clip lexicon, generated |
| `Assets/model_simplified.onnx`, `hand_sign_recognizer.bytes` | Classification models |
| `Assets/Scenes/SampleScene.unity` | Entry point |

~112 first-party C# scripts, alongside a vendored [MediaPipeUnityPlugin](https://github.com/homuler/MediaPipeUnityPlugin).

## Running it

Open in Unity, load `Assets/Scenes/SampleScene.unity`, press Play. A webcam is required for the recognition direction; the production direction runs without one.

> **Clone size warning.** This repository is large (~772 MB compressed) because the sign animation library and the MediaPipe native binaries are committed. Use `--depth 1` unless you need history:
> ```bash
> git clone --depth 1 https://github.com/ShonubiSamuel/Dialex.git
> ```

## State of the project

A working prototype, honestly labelled. What runs: both pipelines end to end, both inference backends, offline replay, the generated lexicon, Yoruba speech input.

Known rough edges, left visible rather than hidden:

- `ASLSignDetector.cs`, `ASLInference.cs` and `HolisticToInferenceBridge.cs` are superseded and fully commented out — earlier approaches to the recognition path, kept for reference.
- Directory names (`New Stuffs`, `My Stuffs`) predate the project having a shape and should be renamed.
- No automated tests. Classification changes are evaluated by replaying recorded landmark sequences through `ISLROfflineReplay`.
- Vocabulary is limited to the signs present in `Assets/Signs/`.

## License

Not yet licensed — all rights reserved. The vendored MediaPipeUnityPlugin retains its own license.
