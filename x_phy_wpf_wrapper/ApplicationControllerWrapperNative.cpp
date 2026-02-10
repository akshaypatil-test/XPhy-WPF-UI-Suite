// This file is compiled as unmanaged C++ (no /clr) to avoid OpenCV Concurrency Runtime conflicts
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <shellapi.h>

#include "ApplicationControllerWrapperNative.h"
#include "application_controller.h"
#include "vision/utils.h"
#include "utils/logger.h"
#include "desktop/resource.h"  // For LICENSE_KEY_* definitions
#include "voice/audio_capture.h"  // For AudioCapture
#include "voice/audio_buffer.h"  // For AudioBuffer
#include "readerwriterqueue/readerwriterqueue.h"  // For ReaderWriterQueue
#include "spdlog/spdlog.h"  // For spdlog::get() to check if logger exists
#include <filesystem>
#include <atomic>
#include <future>
#include <functional>
#include <cstdlib>  // For malloc/free
#include <cstring>  // For memcpy
#include <windows.h>

// Wrapper functions that can be called from C++/CLI code
namespace XPhyWrapperNative {

    ApplicationControllerHandle* CreateController(const std::filesystem::path& outputDir, const std::filesystem::path& configPath) {
        // Initialize logger only if it doesn't already exist
        // Check if logger is already initialized by checking the static appLogger and spdlog registry
        try {
            auto existingLogger = edf::Logger::getLogger();
            if (!existingLogger) {
                // Logger not initialized in our class, check if it exists in spdlog registry
                // The logger name is "Deepfake-Detector" based on the error message
                auto registryLogger = spdlog::get("Deepfake-Detector");
                if (!registryLogger) {
                    // Logger doesn't exist in registry either, safe to initialize
                    edf::Logger::intialise(outputDir);
                }
                // If logger exists in registry but not in our class, 
                // the ApplicationController will handle it or we can continue
            }
            // If logger already exists, skip initialization to avoid "logger already exists" error
        }
        catch (...) {
            // If logger initialization fails (e.g., logger already exists), try to continue
            // Check if logger exists in registry
            try {
                auto registryLogger = spdlog::get("Deepfake-Detector");
                // If logger exists in registry, continue anyway - controller should still work
                // If it doesn't exist, we'll proceed without logger initialization
            }
            catch (...) {
                // If all else fails, continue without logger initialization
                // The ApplicationController might still work
            }
        }
        
        ApplicationControllerHandle* handle = new ApplicationControllerHandle();
        try {
            handle->controller = new std::unique_ptr<edf::ApplicationController>(
                std::make_unique<edf::ApplicationController>(outputDir, configPath));
        }
        catch (const edf::license_manager::LicenseValidationFailure& e) {
            // LicenseValidationFailure is not derived from std::exception, so it would not be
            // caught by the C++/CLI layer. Translate to std::runtime_error so C# gets the message
            // and can block Start Detection (same behavior as the old x_phy_detection_program_ui).
            delete handle;
            const char* msg = "License validation failed.";
            switch (e.reason) {
                case edf::license_manager::LicenseValidationFailureReason::MachineLimitExceeded:
                    msg = "Maximum no. machines granted to your license key already in use. You cannot use the application.";
                    break;
                case edf::license_manager::LicenseValidationFailureReason::Invalid:
                    msg = "License is invalid. You cannot use the application.";
                    break;
                case edf::license_manager::LicenseValidationFailureReason::KeyMissing:
                    msg = "License key is missing. Please enter a valid key.";
                    break;
                case edf::license_manager::LicenseValidationFailureReason::Expired:
                    msg = "License has expired. Please enter a new key.";
                    break;
                case edf::license_manager::LicenseValidationFailureReason::NotFound:
                    msg = "License not found or invalid. Please re-enter your key.";
                    break;
                case edf::license_manager::LicenseValidationFailureReason::ActivationError:
                    msg = "Machine activation unsuccessful.";
                    break;
                case edf::license_manager::LicenseValidationFailureReason::MetadataError:
                    msg = "License information is invalid. You cannot use the application.";
                    break;
                case edf::license_manager::LicenseValidationFailureReason::ServerError:
                    msg = "Unable to validate license. Server returned error.";
                    break;
                case edf::license_manager::LicenseValidationFailureReason::HttpError:
                    msg = "Unable to validate license. Please check your Internet connection and try again.";
                    break;
                case edf::license_manager::LicenseValidationFailureReason::ValidationError:
                    msg = "Unable to validate license.";
                    break;
                case edf::license_manager::LicenseValidationFailureReason::BadResponse:
                    msg = "The license server returned a bad response.";
                    break;
                case edf::license_manager::LicenseValidationFailureReason::CannotVerifySignature:
                    msg = "Could not verify the server's response.";
                    break;
                default:
                    break;
            }
            throw std::runtime_error(msg);
        }
        catch (const std::exception& e) {
            delete handle;
            throw;
        }
        catch (...) {
            delete handle;
            throw;
        }
        return handle;
    }

    void DestroyController(ApplicationControllerHandle* handle) {
        if (handle && handle->controller) {
            try {
                (*handle->controller)->clearEnvironment();
            } catch (...) {}
            delete handle->controller;
            delete handle;
        }
    }

    void SetupInferenceEnv(ApplicationControllerHandle* handle, edf::VideoMode mode) {
        if (handle && handle->controller && *handle->controller) {
            try {
                (*handle->controller)->setupInferenceEnv(mode);
            } catch (const edf::InferenceEnvironmentError&) {
                throw std::runtime_error(
                    "Inference environment setup failed. Ensure required model files are present in the application directory.");
            }
        }
    }

    void RunVideoDetection(
        ApplicationControllerHandle* handle,
        std::atomic_bool& run,
        edf::VideoMode mode,
        bool isBackgroundRun,
        int sessionDurationSecs,
        ManagedResultCallbackFunc resultCallback,
        ManagedFaceUpdateCallbackFunc faceCallback,
        ManagedClassificationCallbackFunc classificationCallback,
        void* callbackData) {
        
        if (handle && handle->controller && *handle->controller) {
            // Create callback wrapper that bridges to managed code
            auto callbackWrapper = [resultCallback, faceCallback, classificationCallback, callbackData](
                const edf::ApplicationController::FaceDetectionUpdate& update) {
                
                // Handle ResultNotification
                if (auto rn = std::get_if<edf::ApplicationController::ResultNotification>(&update)) {
                    if (resultCallback) {
                        if (rn->isLast) {
                            Sleep(100); // Small delay to ensure result_path is available
                        }
                        std::string resultPathStr = rn->result_path.string();
                        resultCallback(callbackData, resultPathStr.c_str(), rn->isLast ? 1 : 0);
                    }
                }
                // Handle ScreenshotFace vector
                else if (auto faces = std::get_if<std::vector<edf::ApplicationController::ScreenshotFace>>(&update)) {
                    if (faceCallback && !faces->empty()) {
                        // Convert ScreenshotFace vector to FaceData array
                        // We need to copy image data as cv::Mat may be temporary
                        std::vector<FaceData> faceDataVec;
                        faceDataVec.reserve(faces->size());
                        
                        for (const auto& face : *faces) {
                            FaceData fd;
                            const cv::Mat& mat = face.resizedPixels;
                            
                            // Calculate image size
                            int imageSize = mat.cols * mat.rows * mat.channels();
                            
                            // Copy image data (cv::Mat uses BGR format)
                            void* imageCopy = malloc(imageSize);
                            if (imageCopy) {
                                memcpy(imageCopy, mat.data, imageSize);
                                fd.imageData = imageCopy;
                                fd.imageSize = imageSize;
                            } else {
                                fd.imageData = nullptr;
                                fd.imageSize = 0;
                            }
                            
                            fd.imageWidth = mat.cols;
                            fd.imageHeight = mat.rows;
                            fd.imageChannels = mat.channels();
                            fd.imageType = mat.type();
                            fd.isFake = face.isFake;
                            fd.probFakeScore = face.probFakeScore;
                            fd.contourRatio = face.contourRatio;
                            faceDataVec.push_back(fd);
                        }
                        
                        // Call managed callback with face data
                        // Managed bridge function will copy the data immediately
                        faceCallback(callbackData, faceDataVec.data(), static_cast<int>(faceDataVec.size()));
                        
                        // Free copied image data after callback completes
                        // (Managed bridge copies data synchronously before invoking managed callback)
                        for (auto& fd : faceDataVec) {
                            if (fd.imageData) {
                                free(fd.imageData);
                            }
                        }
                    }
                }
                // Handle FaceClassification
                else if (auto fc = std::get_if<edf::ApplicationController::FaceClassification>(&update)) {
                    if (classificationCallback) {
                        int classification = (*fc == edf::ApplicationController::FaceClassification::Deepfake) ? 1 : 0;
                        classificationCallback(callbackData, classification);
                    }
                }
            };
            
            // Use captureScreenMats from vision::utils (OpenCV code stays in unmanaged file)
            (*handle->controller)->runVideoDetection(run, mode, isBackgroundRun, sessionDurationSecs, 
                edf::vision::utils::captureScreenMats, callbackWrapper);
        }
    }

    void ClearEnvironment(ApplicationControllerHandle* handle) {
        if (handle && handle->controller && *handle->controller) {
            (*handle->controller)->clearEnvironment();
        }
    }

    std::filesystem::path GetResultsDir(ApplicationControllerHandle* handle) {
        if (handle && handle->controller && *handle->controller) {
            return (*handle->controller)->resultsDir();
        }
        return std::filesystem::path();
    }

    void OpenResultsFolder(const std::filesystem::path& resultsDir) {
        std::wstring resultsDirW = resultsDir.wstring();
        ShellExecuteW(nullptr, L"open", resultsDirW.c_str(), nullptr, nullptr, SW_SHOW);
    }

    void SetupVoiceInferenceEnv(ApplicationControllerHandle* handle, edf::AudioMode mode) {
        if (handle && handle->controller && *handle->controller) {
            try {
                (*handle->controller)->setupVoiceInferenceEnv(mode);
            } catch (const edf::InferenceEnvironmentError&) {
                throw std::runtime_error(
                    "Voice inference environment setup failed. Ensure required model files are present in the application directory.");
            }
        }
    }

    void* CreateAudioCaptureQueue() {
        return new moodycamel::ReaderWriterQueue<edf::voice::AudioBuffer>(1024);
    }

    void DestroyAudioCaptureQueue(void* queue) {
        if (queue) {
            delete static_cast<moodycamel::ReaderWriterQueue<edf::voice::AudioBuffer>*>(queue);
        }
    }

    void RunAudioCapture(void* queue, std::atomic_bool* run, int captureDurationSecs) {
        if (queue && run) {
            auto* captureQueue = static_cast<moodycamel::ReaderWriterQueue<edf::voice::AudioBuffer>*>(queue);
            // Implement voiceCapture inline to avoid including win_common.h
            try {
                edf::voice::AudioCapture audio{edf::voice::AudioType::Stereo};
                while (*run) {
                    bool succeeded = captureQueue->enqueue(audio.read(captureDurationSecs));
                    if (!succeeded) {
                        *run = false;
                        break;
                    }
                }
            } catch (...) {
                *run = false;
                throw;
            }
        }
    }

    // Helper wrapper functions for async calls (avoiding reference parameter issues)
    void RunAudioCaptureWrapper(AudioCaptureParams* params) {
        if (params) {
            RunAudioCapture(params->queue, params->run, params->captureDurationSecs);
            delete params;  // Clean up params after use
        }
    }

    void RunVoiceDetectionWrapper(VoiceDetectionParams* params) {
        if (params) {
            RunVoiceDetection(
                params->handle,
                params->run,
                params->mode,
                params->isBackgroundRun,
                params->sessionDurationSecs,
                params->captureDurationSecs,
                params->captureQueue,
                params->resultCallback,
                params->classificationCallback,
                params->graphScoreCallback,
                params->callbackData);
            delete params;  // Clean up params after use
        }
    }

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
        void* callbackData) {
        
        if (handle && handle->controller && *handle->controller && captureQueue && run) {
            auto* queue = static_cast<moodycamel::ReaderWriterQueue<edf::voice::AudioBuffer>*>(captureQueue);
            
            // Create callback wrapper that bridges to managed code
            auto callbackWrapper = [resultCallback, classificationCallback, graphScoreCallback, callbackData](
                const edf::ApplicationController::VoiceDetectionUpdate& update) {
                
                // Handle ResultNotification
                if (auto rn = std::get_if<edf::ApplicationController::ResultNotification>(&update)) {
                    if (resultCallback) {
                        if (rn->isLast) {
                            Sleep(100); // Small delay to ensure result_path is available
                        }
                        std::string resultPathStr = rn->result_path.string();
                        resultCallback(callbackData, resultPathStr.c_str(), rn->isLast ? 1 : 0);
                    }
                }
                // Handle VoiceClassification
                else if (auto vc = std::get_if<edf::ApplicationController::VoiceClassification>(&update)) {
                    if (classificationCallback) {
                        int classification = 4; // None
                        switch (*vc) {
                            case edf::ApplicationController::VoiceClassification::Deepfake:
                                classification = 1;
                                break;
                            case edf::ApplicationController::VoiceClassification::Real:
                                classification = 0;
                                break;
                            case edf::ApplicationController::VoiceClassification::Analyzing:
                                classification = 2;
                                break;
                            case edf::ApplicationController::VoiceClassification::Invalid:
                                classification = 3;
                                break;
                            case edf::ApplicationController::VoiceClassification::None:
                                classification = 4;
                                break;
                        }
                        classificationCallback(callbackData, classification);
                    }
                }
                // Handle VoiceGraphScore
                else if (auto score = std::get_if<edf::ApplicationController::VoiceGraphScore>(&update)) {
                    if (graphScoreCallback) {
                        graphScoreCallback(callbackData, score->score);
                    }
                }
            };
            
            (*handle->controller)->runVoiceDetection(*run, mode, isBackgroundRun, sessionDurationSecs, 
                captureDurationSecs, *queue, callbackWrapper);
        }
    }

    void ClearVoiceEnvironment(ApplicationControllerHandle* handle) {
        if (handle && handle->controller && *handle->controller) {
            (*handle->controller)->clearVoiceEnvironment();
        }
    }

}
