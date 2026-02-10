#pragma once

#pragma warning(push)
#pragma warning(disable : 6269 26495 6294 6201)
#include "opencv2/dnn.hpp"
#include "opencv2/opencv.hpp"
#pragma warning(pop)
#include "onnxruntime_cxx_api.h"

namespace edf::vision {
class InferenceEngine {
    cv::dnn::Net dnn_net_;
    // Ort::Session session_;
    Ort::Env env_{nullptr};
    Ort::MemoryInfo memory_info_{nullptr};

    Ort::Session session_{nullptr};

    void loadingCaffeModel(const std::string& dirPath);

    void createOnnxSession(const std::string& dirPath, const std::string& modelFileName);
    void createOnnxMemoryInfo();
    bool checkCaffeModelIsLoaded();
    void getOnnxSession(const std::string& dirPath, const std::string& modelFileName);
    void getOnnxMemoryInfo();
    void createSessionEnv();
    void loadCaffeModel(const std::string& dirPath);

  public:
    static constexpr int onnx_inf_len = 512;
    std::vector<Ort::Value> runOnnxInference(cv::Mat& mat_onnx_blob);
    cv::Mat runCaffeInference(cv::Mat mat_desktop_orig, int inputSizeHeight);

    void setupOnnxRuntime(const std::string& dirPath, const std::string& modelFileName);
    void setupCaffeModel(const std::string& dirPath);
    void releaseResources();
};
} // namespace edf::vision
