#pragma once

#include <vector>

namespace edf::voice {
struct AudioBuffer {
    std::vector<unsigned char> samples;
    unsigned long rate   = 0;
    size_t channels      = 1;
    int bytes_per_sample = 4;

    size_t num_samples() const { return samples.size() / channels / bytes_per_sample; }
};
} // namespace edf::voice
