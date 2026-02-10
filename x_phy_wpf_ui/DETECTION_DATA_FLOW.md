# Where detection notification data comes from

This document explains the data flow for the “AI manipulation detected” notification and the **confidence %** value (e.g. 51%).

---

## 1. Source of truth: native inference (C++)

The decision **“is this face fake?”** and the **probability score** are **not** computed in the WPF app. They come from the **native C++ side**:

| Data | Defined in | Set by |
|------|------------|--------|
| `isFake` (bool) | `src/include/application_controller.h` → `ScreenshotFace` | Native inference code (likely inside `detection_program_lib` or linked vision/inference code). Not present in this repo. |
| `probFakeScore` (float 0.0–1.0) | Same | Same native code – model output for “probability this face is fake”. |

So:

- **What the model does:** For each detected face, the native code runs a deepfake detection model and fills:
  - `isFake` – e.g. `true` when `probFakeScore` is above some threshold (e.g. 0.5).
  - `probFakeScore` – raw model confidence (0.0 = real, 1.0 = fake).

- **Why a real video can be marked “deepfake”:** If the notification says “AI manipulated” on a normal/real video, the **model or threshold in that native inference code** is producing false positives. The WPF UI only displays what the native side sends; it does not decide fake vs real.

---

## 2. Path from native to UI

```
Native C++ (application_controller / vision)
  → ScreenshotFace { isFake, probFakeScore, ... }
  → ApplicationControllerWrapperNative.cpp
  → FaceData { isFake, probFakeScore, ... }
  → ApplicationControllerWrapper.cpp (C++/CLI)
  → XPhyWrapper.DetectedFace { IsFake, ProbFakeScore, ... }
  → WPF face-update callback
  → MainWindow.UpdateDetectedFaces(DetectedFace[] faces)
  → DetectionResultsComponent.UpdateDetectedFaces(faces, ...)
```

So the **only** place “fake” and the score are decided is the **native inference**. The rest is passing data through and displaying it.

---

## 3. What the notification shows

The **“Confidence X%”** in the notification can come from two places in the WPF code:

### A) From per-face updates (`UpdateDetectedFaces`)

- **File:** `x_phy_wpf_ui/Controls/DetectionResultsComponent.xaml.cs`
- When at least one face is marked fake, the code does:
  - `avgPct = (totalFakePercent / faces.Length)` where `totalFakePercent` is the sum of `face.ProbFakeScore * 100` for all faces.
  - `LastConfidencePercent = (int)Math.Round(Math.Min(100, avgPct))`.
- So here **“Confidence X%”** is the **average of the model’s `ProbFakeScore`** (per face), expressed as 0–100%.

### B) From overall classification callback (`UpdateOverallClassification`)

- **File:** same component.
- When the **classification callback** says “deepfake” and there are already detected faces:
  - `LastConfidencePercent = (fakeCount / _detectedFaces.Count) * 100`.
- So here **“Confidence X%”** is **“what percentage of detected faces were labeled FAKE”** (e.g. 1 out of 2 faces → 50%), **not** the model’s probability.

The **notification** itself (e.g. “AI manipulation detected”, “Confidence 51%”) is built in:

- **File:** `x_phy_wpf_ui/MainWindow.xaml.cs`
  - `ShowDeepfakeNotification()` uses `DetectionResultsComponent?.LastConfidencePercent` (and falls back to 97 if 0).
- **File:** `x_phy_wpf_ui/DetectionNotificationWindow.xaml.cs`
  - `SetDeepfakeContent(confidencePercent, ...)` shows the text, e.g. `ConfidenceText.Text = $"Confidence {confidencePercent}%"`.

So the number you see is either:

- The **average model score** (from A), or  
- The **fraction of faces marked fake** (from B),  

depending on which path last set `LastConfidencePercent`.

---

## 4. Summary

| Question | Answer |
|----------|--------|
| Where does “fake” / “AI manipulated” come from? | From the **native C++ inference** (model + threshold). WPF only displays it. |
| Where does the confidence % come from? | Either (A) **average of model `ProbFakeScore`** over faces, or (B) **percent of faces with `IsFake == true`**, depending on which code path last ran. Both are in `DetectionResultsComponent.xaml.cs`; the value is then used in `MainWindow` and `DetectionNotificationWindow`. |
| Why did a real video get a “deepfake” notification? | The **native model/threshold** is classifying that content as fake (false positive). To fix or tune this you need to look at the **native inference** (e.g. `detection_program_lib`, model, and threshold), not the WPF UI. |

---

## 5. Relevant files (for reference)

- **Native contract:** `src/include/application_controller.h` (`ScreenshotFace`, `runVideoDetection`, face callback).
- **Wrapper (native → managed):** `x_phy_wpf_wrapper/ApplicationControllerWrapperNative.cpp`, `ApplicationControllerWrapper.cpp`.
- **WPF display and confidence:** `x_phy_wpf_ui/Controls/DetectionResultsComponent.xaml.cs` (`UpdateDetectedFaces`, `UpdateOverallClassification`, `LastConfidencePercent`).
- **Notification popup:** `x_phy_wpf_ui/MainWindow.xaml.cs` (`ShowDeepfakeNotification`), `x_phy_wpf_ui/DetectionNotificationWindow.xaml.cs` (`SetDeepfakeContent`).
