#pragma once

#include "utils/bounded_deque.h"
#include "voice/audio_buffer.h"

#include "tensorflow/c/c_api.h"

#include <string>
#include <vector>
#include <variant>
#include <optional>

namespace edf::voice {
class InferenceEngineVoice {

    TF_Graph* graph_;
    TF_Status* status_;
    TF_SessionOptions* sessionOpts_;
    TF_Session* session_;

    utils::BoundedDeque<float> internalBuffer_;
    utils::BoundedDeque<float> scores_;

    std::vector<float> makeWindow(const AudioBuffer& incoming);

  public:
    InferenceEngineVoice();
    ~InferenceEngineVoice();

    float runInference(std::vector<float>&, bool);

    struct DeepFake {
        std::vector<float> samples;
        int rate    = 0;
        float score = std::numeric_limits<float>::lowest();
    };
    struct Real {
        float score = std::numeric_limits<float>::lowest();
    };
    struct Analyzing {};
    struct Invalid {};
    using Inference = std::variant<DeepFake, Real, Analyzing, Invalid>;
    std::optional<Inference>
    loadAudioBuffer(const AudioBuffer& samples, bool noMoreIncoming, float threshold, bool useWinReverser);

    void loadTFModel(const std::string& dirPath, const std::string& savedModelDirName);
    void emptyBuffers();
    void unloadTFModel();
};
} // namespace edf::voice
