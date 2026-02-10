#pragma once

#include <filesystem>
#include <atomic>
#include <functional>
#include <vector>
#include <memory>

// Forward declarations to avoid including OpenCV in header
namespace cv {
    class Mat;
}

// Forward declarations - actual definitions come from application_controller.h
namespace edf {
    enum class VideoMode;  // Forward declaration only
    enum class AudioMode;  // Forward declaration only
    class ApplicationController;
    namespace voice {
        struct AudioBuffer;
    }
    namespace windows {
        void voiceCapture(std::atomic_bool& run, int captureDurationSecs, void* captureQueue);
    }
}

// Opaque pointer type for ApplicationController
struct ApplicationControllerHandle {
    std::unique_ptr<edf::ApplicationController>* controller;
};

// Callback function pointer types for managed callbacks
typedef void (*ManagedResultCallbackFunc)(void* managedObject, const char* resultPath, int isLast);
typedef void (*ManagedFaceUpdateCallbackFunc)(void* managedObject, void* facesData, int faceCount);
typedef void (*ManagedClassificationCallbackFunc)(void* managedObject, int classification); // 0=Real, 1=Deepfake
typedef void (*ManagedVoiceClassificationCallbackFunc)(void* managedObject, int classification); // 0=Real, 1=Deepfake, 2=Analyzing, 3=Invalid, 4=None
typedef void (*ManagedVoiceGraphScoreCallbackFunc)(void* managedObject, float score);

namespace XPhyWrapperNative {

    ApplicationControllerHandle* CreateController(const std::filesystem::path& outputDir, const std::filesystem::path& configPath);
    void DestroyController(ApplicationControllerHandle* handle);
    void SetupInferenceEnv(ApplicationControllerHandle* handle, edf::VideoMode mode);
    void SetupVoiceInferenceEnv(ApplicationControllerHandle* handle, edf::AudioMode mode);
    
    // Structure to pass face data to managed code
    // Note: imageData must be copied (not just pointer) as cv::Mat may be temporary
    struct FaceData {
        void* imageData;        // Pointer to copied image data (BGR format)
        int imageSize;          // Size of image data in bytes
        int imageWidth;
        int imageHeight;
        int imageChannels;
        int imageType;
        bool isFake;
        float probFakeScore;
        float contourRatio;
    };

    void RunVideoDetection(
        ApplicationControllerHandle* handle,
        std::atomic_bool& run,
        edf::VideoMode mode,
        bool isBackgroundRun,
        int sessionDurationSecs,
        ManagedResultCallbackFunc resultCallback,
        ManagedFaceUpdateCallbackFunc faceCallback,
        ManagedClassificationCallbackFunc classificationCallback,
        void* callbackData);
    
    // Audio detection functions
    void* CreateAudioCaptureQueue();
    void DestroyAudioCaptureQueue(void* queue);
    void RunAudioCapture(void* queue, std::atomic_bool* run, int captureDurationSecs);
    
    // Helper structures and wrapper functions for async calls
    struct AudioCaptureParams {
        void* queue;
        std::atomic_bool* run;
        int captureDurationSecs;
    };
    void RunAudioCaptureWrapper(AudioCaptureParams* params);
    
    struct VoiceDetectionParams {
        ApplicationControllerHandle* handle;
        std::atomic_bool* run;
        edf::AudioMode mode;
        bool isBackgroundRun;
        size_t sessionDurationSecs;
        int captureDurationSecs;
        void* captureQueue;
        ManagedResultCallbackFunc resultCallback;
        ManagedVoiceClassificationCallbackFunc classificationCallback;
        ManagedVoiceGraphScoreCallbackFunc graphScoreCallback;
        void* callbackData;
    };
    void RunVoiceDetectionWrapper(VoiceDetectionParams* params);
    
    void RunVoiceDetection(
        ApplicationControllerHandle* handle,
        std::atomic_bool* run,
        edf::AudioMode mode,
        bool isBackgroundRun,
        size_t sessionDurationSecs,
        int captureDurationSecs,
        void* captureQueue,
        ManagedResultCallbackFunc resultCallback,
        ManagedVoiceClassificationCallbackFunc classificationCallback,
        ManagedVoiceGraphScoreCallbackFunc graphScoreCallback,
        void* callbackData);
    
    void ClearEnvironment(ApplicationControllerHandle* handle);
    void ClearVoiceEnvironment(ApplicationControllerHandle* handle);
    std::filesystem::path GetResultsDir(ApplicationControllerHandle* handle);
    void OpenResultsFolder(const std::filesystem::path& resultsDir);

}
