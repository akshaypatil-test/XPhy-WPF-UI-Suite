/**
 * @file application_controller.h
 * @brief Centralized handle to drive from library from the application.
 */

#pragma once

#include "vision/inference_engine.h"
#include "voice/inference_engine_voice.h"
#include "database.h"
#include "utils/config_reader.h"
#include "utils/keygen_license_manager.h"

#include "readerwriterqueue/readerwriterqueue.h"

#include <filesystem>
#include <variant>
#include <functional>

namespace edf {

/**
 * @brief Marker type indicating inference environment setup failed.
 */
struct InferenceEnvironmentError {};

namespace license_manager {

class KeygenLicenseManager;

} // namespace license_manager

namespace config_reader {

class ApplicationConfig;

} // namespace config_reader

/**
 * @brief Defines modes under which video detection operates.
 */
enum class VideoMode {
    LiveCall,  ///< Real-time video from active call
    WebSurfing ///< Pre-recorded video
};

/**
 * @brief Defines modes under which audio detection operates.
 */
enum class AudioMode {
    LiveCall,  ///< Real-time audio from active call
    WebSurfing ///< Pre-recorded audio
};

/**
 * @class ApplicationController
 * @brief Central controller class for deepfake detection logic.
 */
class ApplicationController {
  public:
    /**
     * @brief Constructs the controller with output and config paths.
     *
     * @param outputDir Directory where inference results will be saved.
     * @param configPath Path to the TOML configuration file.
     * @throws edf::license_manager::LicenseValidationFailure
     * @throws edf::config_reader::ParseError
     * @throws std::runtime_error
     */
    ApplicationController(const std::filesystem::path& outputDir, const std::filesystem::path& configPath);

    /**
     * Initialize the video inference environment.
     * @throws InferenceEnvironmentError
     */
    void setupInferenceEnv(VideoMode mode);

    /// Clears and releases all video-related inference resources.
    void clearEnvironment();

    /**
     * Initialize the voice inference environment.
     * @throws InferenceEnvironmentError
     */
    void setupVoiceInferenceEnv(AudioMode mode);

    /// Clears and releases all voice-related inference resources.
    void clearVoiceEnvironment();

    /**
     * @brief Notifies listeners that a new result is available.
     */
    struct ResultNotification {
        bool isLast = false;               ///< Whether this is the final result
        std::filesystem::path result_path; ///< Path to the result on disk
    };

    /**
     * @brief Score (0 to 1) returned from the voice model (meant to be used in a graph visualization).
     */
    struct VoiceGraphScore {
        float score;
    };

    /// Uploads all saved face detection artifacts to S3.
    void saveFacesToS3();

    /// Uploads all saved voice detection artifacts to S3.
    void saveVoicesToS3();

    /// Checks if the app is allowed to upload results to S3.
    bool hasS3UploadResultsEntitlement();

    /**
     * @brief Deletes result files older than the specified number of days.
     *
     * @param days How many days old the files must be to be deleted.
     */
    void deleteFilesFromDisk(int days);

    /// Sends a health/heartbeat ping to the license server. This is required to keep a registered machine active.
    void pingHeartbeat();

    /**
     * Get credentials to download Velopack updates from S3
     */
    std::map<std::string, std::string> getS3UpdatesCredentials();

    /**
     * Returns true if the app should disable capturing of it's own windows
     * This avoids positive feedback of displayed results back into the detection model
     */
    bool shouldOptOutOfScreenCapture();

    /**
     * @brief Represents a face extracted from a screenshot.
     */

    struct ScreenshotFace {
        cv::Mat rawPixels{};
        cv::Mat resizedPixels{};
        cv::Mat mask{};
        bool isFake         = false;
        float contourRatio  = 0;
        float probFakeScore = 0;
    };

    /**
     * @brief Possible voice classification results.
     */
    enum class VoiceClassification { Deepfake, Real, Analyzing, Invalid, None };

    /// Union of possible updates from voice detection.
    using VoiceDetectionUpdate = std::variant<VoiceClassification, VoiceGraphScore, ResultNotification>;

    /**
     * Runs voice-based deepfake detection.
     *
     * @param run Atomic boolean to control shutdown.
     * @param mode Audio mode (LiveCall or WebSurfing).
     * @param isBackgroundRun Indicates whether the detection is happening in background (only used to annotate results)
     * @param sessionDurationSecs Duration of entire detection session.
     * @param captureDurationSecs Duration of each audio capture interval.
     * @param captureQueue Queue holding audio capture data.
     * @param calback Callback to receive updates.
     */
    void runVoiceDetection(std::atomic_bool& run,
                           AudioMode mode,
                           bool isBackgroundRun,
                           size_t sessionDurationSecs,
                           int captureDurationSecs,
                           moodycamel::ReaderWriterQueue<voice::AudioBuffer>& captureQueue,
                           std::function<void(const VoiceDetectionUpdate&)> callback);

    /**
     * @brief Possible face classification outcomes.
     */
    enum class FaceClassification {
        Deepfake,
        Real,
    };

    /// Union of possible updates from video detection.
    using FaceDetectionUpdate = std::variant<std::vector<ScreenshotFace>, FaceClassification, ResultNotification>;

    /**
     * Runs video-based deepfake detection.
     *
     * @param run Atomic boolean to control shutdown.
     * @param mode Video mode (LiveCall or WebSurfing).
     * @param isBackgroundRun Indicates whether the detection is happening in background (only used to annotate results)
     * @param sessionDurationSecs How long to run the session.
     * @param screenCapture Function that returns captured frames, one per screen (cv::Mat vector).
     * @param callback Callback to receive updates.
     */
    void runVideoDetection(std::atomic_bool& run,
                           VideoMode mode,
                           bool isBackgroundRun,
                           int sessionDurationSecs,
                           std::function<std::vector<cv::Mat>()> screenCapture,
                           std::function<void(const FaceDetectionUpdate&)> callback);

    /**
     * Get the path to the local results directory.
     */
    const std::filesystem::path& resultsDir() const { return resultsRoot_; }

  private:
    // Voice stuff
    std::string voiceModelIdentifier_;
    voice::InferenceEngineVoice voiceInferenceEngine_;

    struct NewInference {
        voice::InferenceEngineVoice::Inference inference;
    };
    struct NoAudio {};
    struct NoChange {};
    using VoiceDetectionState = std::variant<NewInference, NoAudio, NoChange>;

    VoiceDetectionState
    doVoiceDetection(std::atomic_bool&, moodycamel::ReaderWriterQueue<voice::AudioBuffer>&, float, bool);

    std::string videoModelIdentifier_;

    vision::InferenceEngine inferenceEngine_;

    void prepareModels(const std::string& dirPath);

    const std::filesystem::path resultsRoot_;
    db::Database resultsDatabase_;
    std::unique_ptr<edf::license_manager::KeygenLicenseManager> keygenLicenseManager_;
    std::map<std::string, std::string> awsConfig_;
    config_reader::ApplicationConfig applicationConfig_;
    const std::map<std::string, std::string> sysInfo_;
};

} // namespace edf
