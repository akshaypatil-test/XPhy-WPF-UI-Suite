# probFakeScore and how we use it for Fake Confidence

## What is probFakeScore?

**`probFakeScore`** is a **float in the range 0.0 to 1.0** that represents the **model’s confidence that a detected face is AI‑manipulated (fake)**.

| Value   | Meaning |
|--------|--------|
| **0.0** | Model is confident the face is **real** (not a deepfake). |
| **1.0** | Model is confident the face is **fake** (deepfake/AI‑manipulated). |
| **0.5** | Model is uncertain (could be either). |
| **e.g. 0.51** | Model leans slightly toward fake → often displayed as “51%” confidence. |

So:

- **probFakeScore** = “probability this face is fake” from the detection model.
- It is **not** “percentage of faces that are fake” or “confidence in the overall video.” It is **per face**, and it is the raw score the model outputs.

The binary **“is this face fake?”** flag (**`isFake`**) is derived from this score in the **native** code (e.g. `isFake = (probFakeScore > threshold)` with a threshold like 0.5). The WPF app does not decide fake vs real; it only receives both `IsFake` and `ProbFakeScore` from the native side.

---

## Where does probFakeScore come from? (native library)

### 1. Contract (this repo)

The type is defined in the **native** header used by this suite:

**`src/include/application_controller.h`**

```cpp
struct ScreenshotFace {
    cv::Mat rawPixels{};
    cv::Mat resizedPixels{};
    cv::Mat mask{};
    bool isFake         = false;
    float contourRatio  = 0;
    float probFakeScore = 0;   // 0.0 = real, 1.0 = fake
};
```

So the **interface** is: each detected face has `probFakeScore` and `isFake`. The **implementation** that actually runs the model and **sets** these fields is **not** in this repo.

### 2. Who sets probFakeScore?

The value is set inside the **native application controller** implementation, which is linked in via **`detection_program_lib`** (and possibly other native modules). In short:

1. The app captures a frame and extracts face regions.
2. Each face is passed through the **deepfake detection model** (e.g. ONNX/Caffe, see `vision/InferenceEngine`).
3. The model outputs a score (e.g. a single probability or a logit that is converted to 0–1).
4. The native code:
   - assigns that value to **`ScreenshotFace.probFakeScore`**
   - sets **`ScreenshotFace.isFake`** (e.g. `true` when `probFakeScore > 0.5`).

So **probFakeScore is the model’s output** for “how likely is this face to be fake?” The exact formula (e.g. sigmoid, threshold) lives in the native library; this repo only consumes the result.

### 3. Path from native to WPF

| Layer | What happens |
|-------|-------------------------------|
| **Native (e.g. detection_program_lib)** | Runs inference, fills `ScreenshotFace.probFakeScore` and `ScreenshotFace.isFake`. |
| **application_controller.h** | Declares `ScreenshotFace` (contract). |
| **ApplicationControllerWrapperNative.cpp** | Copies `face.probFakeScore` into `FaceData.probFakeScore`. |
| **ApplicationControllerWrapper.cpp** (C++/CLI) | Copies `fd.probFakeScore` into `DetectedFace.ProbFakeScore`. |
| **WPF** | Receives `DetectedFace[]` with `ProbFakeScore` (0.0–1.0) and `IsFake`. |

So **Fake Confidence in the UI is ultimately driven by this native `probFakeScore`** (and how we aggregate it; see below).

---

## How we calculate “Fake Confidence” in the WPF app using probFakeScore

We use **`ProbFakeScore`** (the same 0–1 value from the native side) **only** to derive percentages. We do **not** recompute whether a face is fake; we use the native **`IsFake`** for that.

### 1. Per-face display

For each face we get from the native side:

- **Percentage text (e.g. “51%”):**  
  `confidencePercent = face.ProbFakeScore * 100`  
  So we show the **model score as a percentage** (0–100%).

- **FAKE / REAL label:**  
  We use `face.IsFake` (set in native from the same model, e.g. `probFakeScore > threshold`).

So “Fake Confidence” for a **single face** is literally: **`ProbFakeScore * 100`** (the model’s probability that this face is fake, as a %).

### 2. Overall “Confidence %” for the notification and summary

When we have **multiple faces**, we combine their model scores into one number for the notification and “Suspicious” label:

- **Average of model scores (recommended and what we do now):**  
  - `totalFakePercent = sum over all faces of (face.ProbFakeScore * 100)`  
  - `avgPct = totalFakePercent / faces.Length`  
  - **Fake Confidence** = `(int)Math.Round(Math.Min(100, avgPct))`  
  So the **“Confidence X%”** in the notification is the **average**, across all detected faces, of the model’s “probability this face is fake,” expressed as 0–100%.

- We also store **`ConfidencePercent`** per face in **`FaceViewModel`** (again `ProbFakeScore * 100`) and use the **average of those** when we only have the overall classification callback (so the notification stays consistent with “average model confidence” and not “percent of faces marked fake”).

So in all cases:

- **Fake Confidence** in the WPF app = a percentage derived from **probFakeScore** (either for one face, or averaged over many faces).
- We do **not** use “percent of faces that are fake” as the confidence number anymore; we use the **model’s probability (probFakeScore)** so the number matches the meaning “how confident the model is that the content is fake.”

---

## Summary

| Question | Answer |
|----------|--------|
| **What is probFakeScore?** | Float 0.0–1.0: the **model’s probability that this face is fake**. 0 = real, 1 = fake. Set by the native detection library. |
| **Where is it set?** | In the **native application controller** (e.g. inside `detection_program_lib`), after running the deepfake model on each face. |
| **How do we use it for Fake Confidence?** | **Per face:** `ProbFakeScore * 100` → “51%”. **Overall (notification/summary):** average of `ProbFakeScore * 100` over all faces → one “Confidence X%” value. |
| **Who decides fake vs real?** | The **native** code (e.g. `isFake = probFakeScore > threshold`). WPF only displays `IsFake` and the confidence derived from `ProbFakeScore`. |

So: **probFakeScore** is the native model’s per-face “probability of fake,” and **Fake Confidence** in the UI is that same score (or its average) shown as a percentage.
