// Define these before any includes to minimize Windows COM conflicts
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif

#include "ApplicationControllerWrapper.h"
#include "ApplicationControllerWrapperNative.h"
#include <filesystem>
#include <vcclr.h>  // For PtrToStringChars and gcroot
#include <cstring>  // For memcpy
#include <chrono>   // For wait_for timeout

using namespace System;
// Use fully qualified names for InteropServices to minimize conflicts

// Forward declare ApplicationControllerHandle
struct ApplicationControllerHandle;

// Free function callback bridges - called from native code
// Must be outside namespace to avoid __clrcall calling convention issues
void OnResultNotificationBridge(void* managedObject, const char* resultPath, int isLast)
{
    try {
        System::Runtime::InteropServices::GCHandle gch = 
            System::Runtime::InteropServices::GCHandle::FromIntPtr(System::IntPtr(managedObject));
        
        if (!gch.IsAllocated) {
            return; // GCHandle already freed, ignore callback
        }
        
        XPhyWrapper::ApplicationControllerWrapper^ wrapper = 
            safe_cast<XPhyWrapper::ApplicationControllerWrapper^>(gch.Target);
        
        if (wrapper != nullptr) {
            String^ resultPathStr = gcnew String(resultPath);
            bool isLastBool = (isLast != 0);
            try {
                wrapper->InvokeResultCallback(resultPathStr, isLastBool);
            }
            catch (System::Runtime::InteropServices::COMException^) {
                // COM disconnection - object is being destroyed, ignore
            }
            catch (...) {
                // Ignore other exceptions in callback
            }
        }
    }
    catch (System::Runtime::InteropServices::COMException^) {
        // COM disconnection when accessing GCHandle - object is being destroyed, ignore
    }
    catch (...) {
        // Ignore all exceptions - object may be disconnected
    }
}

void OnFaceUpdateBridge(void* managedObject, void* facesData, int faceCount)
{
    try {
        System::Runtime::InteropServices::GCHandle gch = 
            System::Runtime::InteropServices::GCHandle::FromIntPtr(System::IntPtr(managedObject));
        
        if (!gch.IsAllocated) {
            return; // GCHandle already freed, ignore callback
        }
        
        XPhyWrapper::ApplicationControllerWrapper^ wrapper = 
            safe_cast<XPhyWrapper::ApplicationControllerWrapper^>(gch.Target);
        
        if (wrapper != nullptr && facesData != nullptr && faceCount > 0) {
            try {
            // Cast to FaceData array
            XPhyWrapperNative::FaceData* faceDataArray = 
                static_cast<XPhyWrapperNative::FaceData*>(facesData);
            
            // Convert to managed array
            array<XPhyWrapper::DetectedFace>^ managedFaces = gcnew array<XPhyWrapper::DetectedFace>(faceCount);
            
            for (int i = 0; i < faceCount; i++) {
                XPhyWrapperNative::FaceData& fd = faceDataArray[i];
                XPhyWrapper::DetectedFace managedFace;
                
                // Copy image data immediately (native memory will be freed after this function returns)
                if (fd.imageData != nullptr && fd.imageSize > 0) {
                    managedFace.ImageData = gcnew array<System::Byte>(fd.imageSize);
                    pin_ptr<System::Byte> pinnedData = &managedFace.ImageData[0];
                    memcpy(pinnedData, fd.imageData, fd.imageSize);
                } else {
                    managedFace.ImageData = gcnew array<System::Byte>(0);
                }
                
                managedFace.ImageWidth = fd.imageWidth;
                managedFace.ImageHeight = fd.imageHeight;
                managedFace.ImageChannels = fd.imageChannels;
                managedFace.IsFake = fd.isFake;
                managedFace.ProbFakeScore = fd.probFakeScore;
                managedFace.ContourRatio = fd.contourRatio;
                
                managedFaces[i] = managedFace;
            }
            
                wrapper->InvokeFaceUpdateCallback(managedFaces);
            }
            catch (System::Runtime::InteropServices::COMException^) {
                // COM disconnection - object is being destroyed, ignore
            }
            catch (...) {
                // Ignore other exceptions in callback
            }
        }
    }
    catch (System::Runtime::InteropServices::COMException^) {
        // COM disconnection when accessing GCHandle - object is being destroyed, ignore
    }
    catch (...) {
        // Ignore all exceptions - object may be disconnected
    }
}

void OnClassificationBridge(void* managedObject, int classification)
{
    try {
        System::Runtime::InteropServices::GCHandle gch = 
            System::Runtime::InteropServices::GCHandle::FromIntPtr(System::IntPtr(managedObject));
        
        if (!gch.IsAllocated) {
            return; // GCHandle already freed, ignore callback
        }
        
        XPhyWrapper::ApplicationControllerWrapper^ wrapper = 
            safe_cast<XPhyWrapper::ApplicationControllerWrapper^>(gch.Target);
        
        if (wrapper != nullptr) {
            bool isDeepfake = (classification != 0);
            try {
                wrapper->InvokeClassificationCallback(isDeepfake);
            }
            catch (System::Runtime::InteropServices::COMException^) {
                // COM disconnection - object is being destroyed, ignore
            }
            catch (...) {
                // Ignore other exceptions in callback
            }
        }
    }
    catch (System::Runtime::InteropServices::COMException^) {
        // COM disconnection when accessing GCHandle - object is being destroyed, ignore
    }
    catch (...) {
        // Ignore all exceptions - object may be disconnected
    }
}

void OnVoiceClassificationBridge(void* managedObject, int classification)
{
    try {
        System::Runtime::InteropServices::GCHandle gch = 
            System::Runtime::InteropServices::GCHandle::FromIntPtr(System::IntPtr(managedObject));
        
        if (!gch.IsAllocated) {
            return; // GCHandle already freed, ignore callback
        }
        
        XPhyWrapper::ApplicationControllerWrapper^ wrapper = 
            safe_cast<XPhyWrapper::ApplicationControllerWrapper^>(gch.Target);
        
        if (wrapper != nullptr) {
            try {
                wrapper->InvokeVoiceClassificationCallback(classification);
            }
            catch (System::Runtime::InteropServices::COMException^) {
                // COM disconnection - object is being destroyed, ignore
            }
            catch (...) {
                // Ignore other exceptions in callback
            }
        }
    }
    catch (System::Runtime::InteropServices::COMException^) {
        // COM disconnection when accessing GCHandle - object is being destroyed, ignore
    }
    catch (...) {
        // Ignore all exceptions - object may be disconnected
    }
}

void OnVoiceGraphScoreBridge(void* managedObject, float score)
{
    try {
        System::Runtime::InteropServices::GCHandle gch = 
            System::Runtime::InteropServices::GCHandle::FromIntPtr(System::IntPtr(managedObject));
        
        if (!gch.IsAllocated) {
            return; // GCHandle already freed, ignore callback
        }
        
        XPhyWrapper::ApplicationControllerWrapper^ wrapper = 
            safe_cast<XPhyWrapper::ApplicationControllerWrapper^>(gch.Target);
        
        if (wrapper != nullptr) {
            try {
                wrapper->InvokeVoiceGraphScoreCallback(score);
            }
            catch (System::Runtime::InteropServices::COMException^) {
                // COM disconnection - object is being destroyed, ignore
            }
            catch (...) {
                // Ignore other exceptions in callback
            }
        }
    }
    catch (System::Runtime::InteropServices::COMException^) {
        // COM disconnection when accessing GCHandle - object is being destroyed, ignore
    }
    catch (...) {
        // Ignore all exceptions - object may be disconnected
    }
}

namespace XPhyWrapper {

    ApplicationControllerWrapper::ApplicationControllerWrapper(String^ outputDir, String^ configPath)
    {
        try {
            pin_ptr<const wchar_t> outputDirPtr = PtrToStringChars(outputDir);
            pin_ptr<const wchar_t> configPathPtr = PtrToStringChars(configPath);
            std::wstring outputDirW(outputDirPtr);
            std::wstring configPathW(configPathPtr);
            std::filesystem::path outputDirPath(outputDirW);
            std::filesystem::path configPathPath(configPathW);

            // Create the controller using native wrapper (avoids OpenCV in C++/CLI code)
            controllerHandle_ = XPhyWrapperNative::CreateController(outputDirPath, configPathPath);

            run_ = new std::atomic_bool(false);
            audioRun_ = new std::atomic_bool(false);
            callbacksEnabled_ = new std::atomic<bool>(true);
            videoInferenceFut_ = new std::future<void>();
            audioInferenceFut_ = new std::future<void>();
            audioCaptureFut_ = new std::future<void>();
            audioCaptureQueue_ = nullptr;
            detectionRunning_ = false;
            isAudioDetection_ = false;
            resultCallback_ = nullptr;
            faceUpdateCallback_ = nullptr;
            classificationCallback_ = nullptr;
            voiceClassificationCallback_ = nullptr;
            voiceGraphScoreCallback_ = nullptr;
            gchandlePtr_ = System::IntPtr::Zero;
        }
        catch (const std::exception& e) {
            throw gcnew System::Exception(gcnew String(e.what()));
        }
        catch (...) {
            // Catch any other C++ exception (e.g. license failure before we translated it)
            // so the managed layer always receives an exception and controller stays null.
            throw gcnew System::Exception("License or controller initialization failed.");
        }
    }

    ApplicationControllerWrapper::~ApplicationControllerWrapper()
    {
        this->!ApplicationControllerWrapper();
    }

    ApplicationControllerWrapper::!ApplicationControllerWrapper()
    {
        // Stop audio detection if running
        if (audioRun_) {
            *audioRun_ = false;
        }
        
        // Stop video detection if running
        if (run_) {
            *run_ = false;
        }

        // Wait for audio capture thread
        if (audioCaptureFut_ && audioCaptureFut_->valid()) {
            try {
                audioCaptureFut_->wait_for(std::chrono::milliseconds(500));
            }
            catch (...) {
                // Ignore exceptions during cleanup
            }
            delete audioCaptureFut_;
            audioCaptureFut_ = nullptr;
        }

        // Wait for audio inference thread
        if (audioInferenceFut_ && audioInferenceFut_->valid()) {
            try {
                audioInferenceFut_->wait_for(std::chrono::milliseconds(500));
            }
            catch (...) {
                // Ignore exceptions during cleanup
            }
            delete audioInferenceFut_;
            audioInferenceFut_ = nullptr;
        }

        // Clean up audio capture queue
        if (audioCaptureQueue_) {
            XPhyWrapperNative::DestroyAudioCaptureQueue(audioCaptureQueue_);
            audioCaptureQueue_ = nullptr;
        }

        // Wait for video inference thread
        if (videoInferenceFut_ && videoInferenceFut_->valid()) {
            try {
                videoInferenceFut_->wait_for(std::chrono::milliseconds(100));
            }
            catch (...) {
                // Ignore exceptions during cleanup
            }
            delete videoInferenceFut_;
            videoInferenceFut_ = nullptr;
        }

        // Free GCHandle if allocated
        if (gchandlePtr_ != System::IntPtr::Zero) {
            try {
                System::Runtime::InteropServices::GCHandle gch = 
                    System::Runtime::InteropServices::GCHandle::FromIntPtr(gchandlePtr_);
                if (gch.IsAllocated) {
                    gch.Free();
                }
            }
            catch (...) {
                // Ignore exceptions
            }
            gchandlePtr_ = System::IntPtr::Zero;
        }

        if (controllerHandle_) {
            XPhyWrapperNative::DestroyController(static_cast<ApplicationControllerHandle*>(controllerHandle_));
            controllerHandle_ = nullptr;
        }

        if (run_) {
            delete run_;
            run_ = nullptr;
        }
        
        if (audioRun_) {
            delete audioRun_;
            audioRun_ = nullptr;
        }
        
        if (callbacksEnabled_) {
            delete callbacksEnabled_;
            callbacksEnabled_ = nullptr;
        }
    }

    String^ ApplicationControllerWrapper::GetResultsDir()
    {
        if (!controllerHandle_) {
            throw gcnew System::InvalidOperationException("Controller not initialized");
        }

        try {
            auto resultsPath = XPhyWrapperNative::GetResultsDir(static_cast<ApplicationControllerHandle*>(controllerHandle_));
            return gcnew String(resultsPath.wstring().c_str());
        }
        catch (const std::exception& e) {
            throw gcnew System::Exception(gcnew String(e.what()));
        }
    }

    void ApplicationControllerWrapper::OpenResultsFolder()
    {
        try {
            String^ resultsDir = GetResultsDir();
            pin_ptr<const wchar_t> resultsDirPtr = PtrToStringChars(resultsDir);
            std::filesystem::path resultsDirPath(resultsDirPtr);
            XPhyWrapperNative::OpenResultsFolder(resultsDirPath);
        }
        catch (const std::exception& e) {
            throw gcnew System::Exception(gcnew String(e.what()));
        }
    }


    void ApplicationControllerWrapper::PrepareInferenceEnvironment()
    {
        if (!controllerHandle_) {
            throw gcnew System::InvalidOperationException("Controller not initialized");
        }
        if (detectionRunning_) {
            return; // Do not prepare while detection is running
        }
        try {
            if (controllerHandle_) {
                try {
                    XPhyWrapperNative::ClearEnvironment(static_cast<ApplicationControllerHandle*>(controllerHandle_));
                }
                catch (...) {
                    // Ignore cleanup errors
                }
            }
            XPhyWrapperNative::SetupInferenceEnv(static_cast<ApplicationControllerHandle*>(controllerHandle_), edf::VideoMode::WebSurfing);
        }
        catch (const std::exception& e) {
            throw gcnew System::Exception(gcnew String(e.what()));
        }
        catch (...) {
            throw gcnew System::Exception("Failed to setup inference environment");
        }
    }

    void ApplicationControllerWrapper::StartWebSurfingVideoDetection(
        int sessionDurationSecs,
        Action<String^, bool>^ resultCallback,
        Action<array<DetectedFace>^>^ faceUpdateCallback,
        Action<bool>^ classificationCallback)
    {
        if (!controllerHandle_) {
            throw gcnew System::InvalidOperationException("Controller not initialized");
        }

        if (detectionRunning_) {
            throw gcnew System::InvalidOperationException("Detection is already running");
        }

        try {
            // First, ensure previous detection is fully stopped and cleaned up
            if (videoInferenceFut_ && videoInferenceFut_->valid()) {
                // Previous detection might still be running - stop it and wait briefly
                try {
                    *run_ = false; // Stop previous detection
                    // Wait briefly to ensure previous detection stops
                    auto status = videoInferenceFut_->wait_for(std::chrono::milliseconds(500));
                    // If still running, continue anyway - it will finish when run_ is false
                }
                catch (...) {
                    // Ignore exceptions
                }
                
                // Clean up the old future
                delete videoInferenceFut_;
                videoInferenceFut_ = new std::future<void>();
            }
            
            // Clean up any previous GCHandle before starting new detection
            if (gchandlePtr_ != System::IntPtr::Zero) {
                try {
                    System::Runtime::InteropServices::GCHandle gch = 
                        System::Runtime::InteropServices::GCHandle::FromIntPtr(gchandlePtr_);
                    if (gch.IsAllocated) {
                        gch.Free();
                    }
                }
                catch (...) {
                    // Ignore exceptions
                }
                gchandlePtr_ = System::IntPtr::Zero;
            }
            
            // Clear environment from previous run if needed
            if (controllerHandle_) {
                try {
                    XPhyWrapperNative::ClearEnvironment(static_cast<ApplicationControllerHandle*>(controllerHandle_));
                }
                catch (...) {
                    // Ignore exceptions
                }
            }
            
            // Now setup inference environment for new detection
            XPhyWrapperNative::SetupInferenceEnv(static_cast<ApplicationControllerHandle*>(controllerHandle_), edf::VideoMode::WebSurfing);

            // Set run flag and detection running status
            *run_ = true;
            if (callbacksEnabled_) {
                *callbacksEnabled_ = true; // Enable callbacks
            }
            detectionRunning_ = true;

            // Store callbacks to keep them alive
            resultCallback_ = resultCallback;
            faceUpdateCallback_ = faceUpdateCallback;
            classificationCallback_ = classificationCallback;
            
            // Start video detection asynchronously using native wrapper
            // Pass callback bridge functions and GCHandle as callback data
            System::Runtime::InteropServices::GCHandle gch = 
                System::Runtime::InteropServices::GCHandle::Alloc(this);
            gchandlePtr_ = System::Runtime::InteropServices::GCHandle::ToIntPtr(gch);
            void* callbackData = gchandlePtr_.ToPointer();
            *videoInferenceFut_ = std::async(
                std::launch::async,
                XPhyWrapperNative::RunVideoDetection,
                static_cast<ApplicationControllerHandle*>(controllerHandle_),
                std::ref(*run_),
                edf::VideoMode::WebSurfing,
                false, // isBackgroundRun
                sessionDurationSecs,
                OnResultNotificationBridge,
                OnFaceUpdateBridge,
                OnClassificationBridge,
                callbackData);
        }
        catch (const std::exception& e) {
            detectionRunning_ = false;
            throw gcnew System::Exception(gcnew String(e.what()));
        }
        catch (...) {
            detectionRunning_ = false;
            throw gcnew System::Exception("Failed to setup inference environment");
        }
    }

    void ApplicationControllerWrapper::StartLiveCallVideoDetection(
        int sessionDurationSecs,
        Action<String^, bool>^ resultCallback,
        Action<array<DetectedFace>^>^ faceUpdateCallback,
        Action<bool>^ classificationCallback)
    {
        if (!controllerHandle_) {
            throw gcnew System::InvalidOperationException("Controller not initialized");
        }

        if (detectionRunning_) {
            throw gcnew System::InvalidOperationException("Detection is already running");
        }

        try {
            // First, ensure previous detection is fully stopped and cleaned up
            if (videoInferenceFut_ && videoInferenceFut_->valid()) {
                // Previous detection might still be running - stop it and wait briefly
                try {
                    *run_ = false; // Stop previous detection
                    // Wait briefly to ensure previous detection stops
                    auto status = videoInferenceFut_->wait_for(std::chrono::milliseconds(500));
                    // If still running, continue anyway - it will finish when run_ is false
                }
                catch (...) {
                    // Ignore exceptions
                }
                
                // Clean up the old future
                delete videoInferenceFut_;
                videoInferenceFut_ = new std::future<void>();
            }
            
            // Clean up any previous GCHandle before starting new detection
            if (gchandlePtr_ != System::IntPtr::Zero) {
                try {
                    System::Runtime::InteropServices::GCHandle gch = 
                        System::Runtime::InteropServices::GCHandle::FromIntPtr(gchandlePtr_);
                    if (gch.IsAllocated) {
                        gch.Free();
                    }
                }
                catch (...) {
                    // Ignore exceptions
                }
                gchandlePtr_ = System::IntPtr::Zero;
            }
            
            // Clear environment from previous run if needed
            if (controllerHandle_) {
                try {
                    XPhyWrapperNative::ClearEnvironment(static_cast<ApplicationControllerHandle*>(controllerHandle_));
                }
                catch (...) {
                    // Ignore exceptions
                }
            }
            
            // Now setup inference environment for LiveCall mode
            XPhyWrapperNative::SetupInferenceEnv(static_cast<ApplicationControllerHandle*>(controllerHandle_), edf::VideoMode::LiveCall);

            // Set run flag and detection running status
            *run_ = true;
            if (callbacksEnabled_) {
                *callbacksEnabled_ = true; // Enable callbacks
            }
            detectionRunning_ = true;

            // Store callbacks to keep them alive
            resultCallback_ = resultCallback;
            faceUpdateCallback_ = faceUpdateCallback;
            classificationCallback_ = classificationCallback;
            
            // Start video detection asynchronously using native wrapper
            // Pass callback bridge functions and GCHandle as callback data
            System::Runtime::InteropServices::GCHandle gch = 
                System::Runtime::InteropServices::GCHandle::Alloc(this);
            gchandlePtr_ = System::Runtime::InteropServices::GCHandle::ToIntPtr(gch);
            void* callbackData = gchandlePtr_.ToPointer();
            *videoInferenceFut_ = std::async(
                std::launch::async,
                XPhyWrapperNative::RunVideoDetection,
                static_cast<ApplicationControllerHandle*>(controllerHandle_),
                std::ref(*run_),
                edf::VideoMode::LiveCall,
                false, // isBackgroundRun
                sessionDurationSecs,
                OnResultNotificationBridge,
                OnFaceUpdateBridge,
                OnClassificationBridge,
                callbackData);
        }
        catch (const std::exception& e) {
            detectionRunning_ = false;
            throw gcnew System::Exception(gcnew String(e.what()));
        }
        catch (...) {
            detectionRunning_ = false;
            throw gcnew System::Exception("Failed to setup inference environment");
        }
    }

    void ApplicationControllerWrapper::StopVideoDetection()
    {
        // Default behavior: clear callbacks immediately
        StopVideoDetection(false);
    }

    void ApplicationControllerWrapper::StopVideoDetection(bool waitForResults)
    {
        // If audio detection is running, stop it instead
        if (isAudioDetection_) {
            StopAudioDetection();
            return;
        }

        // Disable callbacks first to prevent new callbacks from being processed
        if (callbacksEnabled_) {
            *callbacksEnabled_ = false;
        }

        // Set detectionRunning_ to false immediately so IsDetectionRunning() returns false quickly
        detectionRunning_ = false;
        
        // Set run flag to false to stop detection loop (this is the critical part)
        // This must happen first to stop the detection thread
        if (run_) {
            *run_ = false;
        }
        
        // Wait for video thread to finish (with timeout) before clearing callbacks
        if (videoInferenceFut_ && videoInferenceFut_->valid()) {
            try {
                videoInferenceFut_->wait_for(std::chrono::milliseconds(1000)); // Wait up to 1 second
            }
            catch (...) {
                // Ignore exceptions
            }
        }

        // For Web Surfing mode, we want to wait for the final result callback to save results
        // So we don't clear callbacks immediately in that case
        if (!waitForResults)
        {
            // Clear callbacks immediately to prevent further updates from reaching UI
            // This prevents any pending callbacks from executing after stop
            resultCallback_ = nullptr;
            faceUpdateCallback_ = nullptr;
            classificationCallback_ = nullptr;
        }
        // If waitForResults is true, callbacks will be cleared when final result arrives (isLast = true)
        // See InvokeResultCallback where isLast = true clears callbacks
        
        // Note: We intentionally don't wait for the thread or clean up resources here
        // to keep this function non-blocking and responsive. The detection thread will
        // check run_ flag and stop naturally. Cleanup happens:
        // 1. On next StartWebSurfingVideoDetection() or StartLiveCallVideoDetection() call (which waits and cleans up)
        // 2. In the destructor when object is destroyed
    }

    void ApplicationControllerWrapper::StartWebSurfingAudioDetection(
        int sessionDurationSecs,
        Action<String^, bool>^ resultCallback,
        Action<int>^ classificationCallback,
        Action<float>^ graphScoreCallback)
    {
        StartAudioDetection(edf::AudioMode::WebSurfing, sessionDurationSecs, resultCallback, classificationCallback, graphScoreCallback);
    }

    void ApplicationControllerWrapper::StartLiveCallAudioDetection(
        int sessionDurationSecs,
        Action<String^, bool>^ resultCallback,
        Action<int>^ classificationCallback,
        Action<float>^ graphScoreCallback)
    {
        StartAudioDetection(edf::AudioMode::LiveCall, sessionDurationSecs, resultCallback, classificationCallback, graphScoreCallback);
    }

    void ApplicationControllerWrapper::StartAudioDetection(
        edf::AudioMode mode,
        int sessionDurationSecs,
        Action<String^, bool>^ resultCallback,
        Action<int>^ classificationCallback,
        Action<float>^ graphScoreCallback)
    {
        if (!controllerHandle_) {
            throw gcnew System::InvalidOperationException("Controller not initialized");
        }

        if (detectionRunning_) {
            throw gcnew System::InvalidOperationException("Detection is already running");
        }

        try {
            // Clean up any previous audio detection
            if (audioInferenceFut_ && audioInferenceFut_->valid()) {
                try {
                    *audioRun_ = false;
                    auto status = audioInferenceFut_->wait_for(std::chrono::milliseconds(500));
                }
                catch (...) {
                    // Ignore exceptions
                }
                delete audioInferenceFut_;
                audioInferenceFut_ = new std::future<void>();
            }

            if (audioCaptureFut_ && audioCaptureFut_->valid()) {
                try {
                    *audioRun_ = false;
                    auto status = audioCaptureFut_->wait_for(std::chrono::milliseconds(500));
                }
                catch (...) {
                    // Ignore exceptions
                }
                delete audioCaptureFut_;
                audioCaptureFut_ = new std::future<void>();
            }

            // Clean up previous audio capture queue
            if (audioCaptureQueue_) {
                XPhyWrapperNative::DestroyAudioCaptureQueue(audioCaptureQueue_);
                audioCaptureQueue_ = nullptr;
            }

            // Clean up any previous GCHandle
            if (gchandlePtr_ != System::IntPtr::Zero) {
                try {
                    System::Runtime::InteropServices::GCHandle gch = 
                        System::Runtime::InteropServices::GCHandle::FromIntPtr(gchandlePtr_);
                    if (gch.IsAllocated) {
                        gch.Free();
                    }
                }
                catch (...) {
                    // Ignore exceptions
                }
                gchandlePtr_ = System::IntPtr::Zero;
            }

            // Clear voice environment from previous run if needed
            if (controllerHandle_) {
                try {
                    XPhyWrapperNative::ClearVoiceEnvironment(static_cast<ApplicationControllerHandle*>(controllerHandle_));
                }
                catch (...) {
                    // Ignore exceptions
                }
            }

            // Setup voice inference environment
            XPhyWrapperNative::SetupVoiceInferenceEnv(static_cast<ApplicationControllerHandle*>(controllerHandle_), mode);

            // Create audio capture queue
            audioCaptureQueue_ = XPhyWrapperNative::CreateAudioCaptureQueue();

            // Set run flags and detection running status
            *audioRun_ = true;
            if (callbacksEnabled_) {
                *callbacksEnabled_ = true; // Enable callbacks
            }
            detectionRunning_ = true;
            isAudioDetection_ = true;

            // Store callbacks to keep them alive
            resultCallback_ = resultCallback;
            voiceClassificationCallback_ = classificationCallback;
            voiceGraphScoreCallback_ = graphScoreCallback;

            // Allocate GCHandle for callback data
            System::Runtime::InteropServices::GCHandle gch = 
                System::Runtime::InteropServices::GCHandle::Alloc(this);
            gchandlePtr_ = System::Runtime::InteropServices::GCHandle::ToIntPtr(gch);
            void* callbackData = gchandlePtr_.ToPointer();

            // Start audio capture thread
            int captureDurationSecs = 1; // Default 1 second capture windows
            XPhyWrapperNative::AudioCaptureParams* captureParams = new XPhyWrapperNative::AudioCaptureParams();
            captureParams->queue = audioCaptureQueue_;
            captureParams->run = audioRun_;
            captureParams->captureDurationSecs = captureDurationSecs;
            *audioCaptureFut_ = std::async(
                std::launch::async,
                XPhyWrapperNative::RunAudioCaptureWrapper,
                captureParams);

            // Start voice detection asynchronously
            XPhyWrapperNative::VoiceDetectionParams* voiceParams = new XPhyWrapperNative::VoiceDetectionParams();
            voiceParams->handle = static_cast<ApplicationControllerHandle*>(controllerHandle_);
            voiceParams->run = audioRun_;
            voiceParams->mode = mode;
            voiceParams->isBackgroundRun = false;
            voiceParams->sessionDurationSecs = static_cast<size_t>(sessionDurationSecs);
            voiceParams->captureDurationSecs = captureDurationSecs;
            voiceParams->captureQueue = audioCaptureQueue_;
            voiceParams->resultCallback = OnResultNotificationBridge;
            voiceParams->classificationCallback = OnVoiceClassificationBridge;
            voiceParams->graphScoreCallback = OnVoiceGraphScoreBridge;
            voiceParams->callbackData = callbackData;
            *audioInferenceFut_ = std::async(
                std::launch::async,
                XPhyWrapperNative::RunVoiceDetectionWrapper,
                voiceParams);
        }
        catch (const std::exception& e) {
            detectionRunning_ = false;
            isAudioDetection_ = false;
            throw gcnew System::Exception(gcnew String(e.what()));
        }
        catch (...) {
            detectionRunning_ = false;
            isAudioDetection_ = false;
            throw gcnew System::Exception("Failed to setup audio inference environment");
        }
    }

    void ApplicationControllerWrapper::StopAudioDetection()
    {
        // Disable callbacks first to prevent new callbacks from being processed
        if (callbacksEnabled_) {
            *callbacksEnabled_ = false;
        }
        
        // Set detectionRunning_ to false immediately
        detectionRunning_ = false;
        isAudioDetection_ = false;
        
        // Set audio run flag to false to stop detection loop
        if (audioRun_) {
            *audioRun_ = false;
        }

        // Wait for threads to finish (with timeout) before clearing callbacks
        // This prevents callbacks from being invoked after we clear them
        if (audioCaptureFut_ && audioCaptureFut_->valid()) {
            try {
                audioCaptureFut_->wait_for(std::chrono::milliseconds(1000)); // Wait up to 1 second
            }
            catch (...) {
                // Ignore exceptions
            }
        }

        if (audioInferenceFut_ && audioInferenceFut_->valid()) {
            try {
                audioInferenceFut_->wait_for(std::chrono::milliseconds(1000)); // Wait up to 1 second
            }
            catch (...) {
                // Ignore exceptions
            }
        }

        // Now safe to clear callbacks - threads should have finished
        resultCallback_ = nullptr;
        voiceClassificationCallback_ = nullptr;
        voiceGraphScoreCallback_ = nullptr;
    }

    bool ApplicationControllerWrapper::IsDetectionRunning()
    {
        return detectionRunning_;
    }

    // Public methods for callback bridges to access callbacks
    void ApplicationControllerWrapper::InvokeResultCallback(String^ resultPath, bool isLast)
    {
        // Check if callbacks are enabled (not stopped)
        if (callbacksEnabled_ && !(*callbacksEnabled_)) {
            return; // Callbacks disabled, ignore
        }
        
        if (resultCallback_ != nullptr) {
            try {
                resultCallback_(resultPath, isLast);
            }
            catch (System::Runtime::InteropServices::COMException^) {
                // COM disconnection - ignore
            }
            catch (...) {
                // Ignore exceptions in callback - don't let callback errors break detection
            }
            
            // When we get the final result (isLast = true), mark detection as completed
            // and clear callbacks after invoking (this allows results to be saved when stopping)
            if (isLast) {
                detectionRunning_ = false;
                // Clear callbacks after final result is delivered
                // This ensures results are saved when StopVideoDetection(waitForResults=true) is called
                resultCallback_ = nullptr;
                if (isAudioDetection_) {
                    // Clear audio callbacks
                    voiceClassificationCallback_ = nullptr;
                    voiceGraphScoreCallback_ = nullptr;
                } else {
                    // Clear video callbacks
                    faceUpdateCallback_ = nullptr;
                    classificationCallback_ = nullptr;
                }
            }
        }
    }

    void ApplicationControllerWrapper::InvokeFaceUpdateCallback(array<DetectedFace>^ faces)
    {
        // Check if callbacks are enabled (not stopped)
        if (callbacksEnabled_ && !(*callbacksEnabled_)) {
            return; // Callbacks disabled, ignore
        }
        
        if (faceUpdateCallback_ != nullptr) {
            try {
                faceUpdateCallback_(faces);
            }
            catch (System::Runtime::InteropServices::COMException^) {
                // COM disconnection - ignore
            }
            catch (...) {
                // Ignore exceptions
            }
        }
    }

    void ApplicationControllerWrapper::InvokeClassificationCallback(bool isDeepfake)
    {
        // Check if callbacks are enabled (not stopped)
        if (callbacksEnabled_ && !(*callbacksEnabled_)) {
            return; // Callbacks disabled, ignore
        }
        
        if (classificationCallback_ != nullptr) {
            try {
                classificationCallback_(isDeepfake);
            }
            catch (System::Runtime::InteropServices::COMException^) {
                // COM disconnection - ignore
            }
            catch (...) {
                // Ignore exceptions
            }
        }
    }

    void ApplicationControllerWrapper::InvokeVoiceClassificationCallback(int classification)
    {
        // Check if callbacks are enabled (not stopped)
        if (callbacksEnabled_ && !(*callbacksEnabled_)) {
            return; // Callbacks disabled, ignore
        }
        
        if (voiceClassificationCallback_ != nullptr) {
            try {
                voiceClassificationCallback_(classification);
            }
            catch (System::Runtime::InteropServices::COMException^) {
                // COM disconnection - ignore
            }
            catch (...) {
                // Ignore exceptions
            }
        }
    }

    void ApplicationControllerWrapper::InvokeVoiceGraphScoreCallback(float score)
    {
        // Check if callbacks are enabled (not stopped)
        if (callbacksEnabled_ && !(*callbacksEnabled_)) {
            return; // Callbacks disabled, ignore
        }
        
        if (voiceGraphScoreCallback_ != nullptr) {
            try {
                voiceGraphScoreCallback_(score);
            }
            catch (System::Runtime::InteropServices::COMException^) {
                // COM disconnection - ignore
            }
            catch (...) {
                // Ignore exceptions
            }
        }
    }

}
