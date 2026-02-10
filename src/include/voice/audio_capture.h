#pragma once
#include "voice/audio_buffer.h"

#define NOMINMAX
#include "windows.h"
#include "Mmdeviceapi.h"
#include "audioclient.h"

#include <memory>

namespace edf::voice {
enum class AudioType { Mono, Stereo };

class AudioCapture {
    static void release(auto* p) { p->Release(); }
    template <typename T> using released_ptr = std::unique_ptr<T, decltype(&AudioCapture::release<T>)>;

    const unsigned long refTimePerSec      = 10000000;
    const unsigned long refTimePerMilliSec = 10000;
    const size_t maxLoopsBeforeStop        = 2; // each iteration is 0.5 seconds
    const CLSID CLSID_MMDeviceEnumerator   = __uuidof(MMDeviceEnumerator);
    const IID IID_IMMDeviceEnumerator      = __uuidof(IMMDeviceEnumerator);
    const IID IID_IAudioClient             = __uuidof(IAudioClient);
    const IID IID_IAudioCaptureClient      = __uuidof(IAudioCaptureClient);

    UINT32 bufferFrameCount_ = 0;
    DWORD hnsActualDuration_ = 0;
    const int chunkFactor    = 2;

    released_ptr<IMMDeviceEnumerator> enumerator_{nullptr, release};
    released_ptr<IMMDevice> device_{nullptr, release};
    released_ptr<IAudioClient> audioClient_{nullptr, release};
    std::unique_ptr<WAVEFORMATEX, decltype(&CoTaskMemFree)> wfx_{nullptr, CoTaskMemFree};
    released_ptr<IAudioCaptureClient> captureClient_{nullptr, release};

    AudioType audioType_;

    void start();
    void stop();

  public:
    explicit AudioCapture(AudioType type) : audioType_(type) { start(); }

    AudioCapture(const AudioCapture&)            = delete;
    AudioCapture& operator=(const AudioCapture&) = delete;

    AudioBuffer read(int durationSecs);

    ~AudioCapture() { stop(); }
};
} // namespace edf::voice
