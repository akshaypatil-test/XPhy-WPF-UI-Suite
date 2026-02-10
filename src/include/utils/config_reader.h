#pragma once

#include "toml.hpp"

#include <map>
#include <string>
#include <filesystem>

namespace edf {

class ApplicationController;

} // namespace edf

namespace edf::config_reader {

struct ParseError {};

class ApplicationConfig {
    ApplicationConfig(const std::filesystem::path& tomlConfigFilepath);

    std::map<std::string, std::string> getLicenseConfig();

    // Config fields

    // app
    const char* modelDirectory;
    bool optOutOfScreenCapture;

    // voice.generic
    const char* voiceGenericModelIdentifier;
    float voiceGenericProbScoreThreshold;
    float voiceGenericFakeProportionThreshold;

    // voice.live
    const char* voiceLiveModelIdentifier;
    float voiceLiveProbScoreThreshold;
    float voiceLiveFakeProportionThreshold;

    // video
    int videoCaffeDetectionSize;
    int videoMaxNumberFaces;
    int videoRollingWindowExpiryDuration;
    int videoRollingWindowCooldownDuration;
    int videoRollingWindowMinimumAlertSize;

    // video.generic
    const char* videoGenericModelIdentifier;
    float videoGenericFakeAndContourThreshold;
    float videoGenericMaskThreshold;
    float videoGenericProbFakeThreshold;
    float videoGenericFakeProportionThreshold;

    // video.live
    const char* videoLiveModelIdentifier;
    float videoLiveFakeAndContourThreshold;
    float videoLiveMaskThreshold;
    float videoLiveProbFakeThreshold;
    float videoLiveFakeProportionThreshold;

    template <typename T> void setIfPresent(T& fieldName, std::string key);
    void overrideFromFile(); // This method is only to be called in non-release mode

    // License configs
    std::string platform;
    std::string baseUrl;
    std::string machineActivationPath;
    std::string licenseKeyValidationPath;
    std::string machineIdPath;
    std::string licensePath;

    toml::table table;

    friend class edf::ApplicationController;
};

} // namespace edf::config_reader
