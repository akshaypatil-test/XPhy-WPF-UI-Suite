#pragma once

// Forward declarations only - no OpenCV includes in C++/CLI header
#include <memory>
#include <atomic>
#include <future>
#include <functional>
#include <string>
#include <vector>
#include <vcclr.h>  // Required for gcroot

// Forward declare cv::Mat to avoid including OpenCV in header
namespace cv {
    class Mat;
}

namespace edf {
    class ApplicationController;
    enum class VideoMode {
        LiveCall,
        WebSurfing
    };
    enum class AudioMode {
        LiveCall,
        WebSurfing
    };
    struct InferenceEnvironmentError;
    
    // Forward declare nested types - ApplicationController is a class, not a namespace
    // FaceDetectionUpdate is a nested type in ApplicationController class
    namespace vision::utils {
        std::vector<cv::Mat> captureScreenMats();
    }
}

using namespace System;
// Don't use System::Runtime::InteropServices namespace to avoid Windows COM header conflicts

// Forward declare for callback
namespace XPhyWrapper {
    ref class ApplicationControllerWrapper;
}

// Free function callback bridge declarations
void OnResultNotificationBridge(void* managedObject, const char* resultPath, int isLast);
void OnFaceUpdateBridge(void* managedObject, void* facesData, int faceCount);
void OnClassificationBridge(void* managedObject, int classification);
void OnVoiceClassificationBridge(void* managedObject, int classification); // 0=Real, 1=Deepfake, 2=Analyzing, 3=Invalid, 4=None
void OnVoiceGraphScoreBridge(void* managedObject, float score);

namespace XPhyWrapper {

    // Managed structure representing a detected face
    public value struct DetectedFace {
        array<System::Byte>^ ImageData;  // Image bytes (BGR format)
        int ImageWidth;
        int ImageHeight;
        int ImageChannels;
        bool IsFake;
        float ProbFakeScore;  // 0.0 to 1.0
        float ContourRatio;
    };

    // Managed wrapper for ApplicationController
    public ref class ApplicationControllerWrapper
    {
    public:
        ApplicationControllerWrapper(String^ outputDir, String^ configPath);
        ~ApplicationControllerWrapper();
        !ApplicationControllerWrapper();

        // Get the results directory path
        String^ GetResultsDir();

        // Open the results folder in Windows Explorer
        void OpenResultsFolder();

        // Start Web Surfing Video Detection
        // Note: Callbacks will be invoked from a background thread
        void StartWebSurfingVideoDetection(int sessionDurationSecs, 
            Action<String^, bool>^ resultCallback,
            Action<array<DetectedFace>^>^ faceUpdateCallback,
            Action<bool>^ classificationCallback);  // true=Deepfake, false=Real

        // Start Live Call Video Detection
        // Note: Callbacks will be invoked from a background thread
        void StartLiveCallVideoDetection(int sessionDurationSecs, 
            Action<String^, bool>^ resultCallback,
            Action<array<DetectedFace>^>^ faceUpdateCallback,
            Action<bool>^ classificationCallback);  // true=Deepfake, false=Real

        // Start Web Surfing Audio Detection
        // Note: Callbacks will be invoked from a background thread
        void StartWebSurfingAudioDetection(int sessionDurationSecs,
            Action<String^, bool>^ resultCallback,
            Action<int>^ classificationCallback,  // 0=Real, 1=Deepfake, 2=Analyzing, 3=Invalid, 4=None
            Action<float>^ graphScoreCallback);  // Score for graph visualization (0.0 to 1.0)

        // Start Live Call Audio Detection
        // Note: Callbacks will be invoked from a background thread
        void StartLiveCallAudioDetection(int sessionDurationSecs,
            Action<String^, bool>^ resultCallback,
            Action<int>^ classificationCallback,  // 0=Real, 1=Deepfake, 2=Analyzing, 3=Invalid, 4=None
            Action<float>^ graphScoreCallback);  // Score for graph visualization (0.0 to 1.0)

        // Stop video detection
        void StopVideoDetection();
        
        // Stop video detection and wait for results to be saved
        // waitForResults: if true, don't clear callbacks immediately (for Web Surfing mode to save results)
        void StopVideoDetection(bool waitForResults);

        // Stop audio detection
        void StopAudioDetection();

        // Check if detection is running
        bool IsDetectionRunning();

        /// <summary>
        /// Prepares the inference environment (loads models, etc.) so that the first
        /// Start detection call is more likely to succeed. Call after controller creation
        /// to avoid first-time setup failures when the user starts detection.
        /// </summary>
        void PrepareInferenceEnvironment();

    private:
        // Internal helper for audio detection
        void StartAudioDetection(edf::AudioMode mode, int sessionDurationSecs,
            Action<String^, bool>^ resultCallback,
            Action<int>^ classificationCallback,
            Action<float>^ graphScoreCallback);
        void* controllerHandle_; // Opaque pointer to ApplicationControllerHandle
        std::atomic_bool* run_;
        std::atomic_bool* audioRun_;  // Separate run flag for audio detection
        std::future<void>* videoInferenceFut_;
        std::future<void>* audioInferenceFut_;  // Future for audio inference thread
        std::future<void>* audioCaptureFut_;   // Future for audio capture thread
        void* audioCaptureQueue_;  // Opaque pointer to audio capture queue
        bool detectionRunning_;
        bool isAudioDetection_;  // Track if we're running audio or video detection
        std::atomic<bool>* callbacksEnabled_;  // Flag to prevent callbacks after stop
        Action<String^, bool>^ resultCallback_; // Keep callbacks alive
        Action<array<DetectedFace>^>^ faceUpdateCallback_;
        Action<bool>^ classificationCallback_;  // For video
        Action<int>^ voiceClassificationCallback_;  // For audio
        Action<float>^ voiceGraphScoreCallback_;  // For audio graph scores
        System::IntPtr gchandlePtr_; // Store GCHandle pointer for cleanup
        
    public:
        // Public methods for callback bridges to access callbacks
        void InvokeResultCallback(String^ resultPath, bool isLast);
        void InvokeFaceUpdateCallback(array<DetectedFace>^ faces);
        void InvokeClassificationCallback(bool isDeepfake);
        void InvokeVoiceClassificationCallback(int classification);
        void InvokeVoiceGraphScoreCallback(float score);
    };

}
