/**
 * @file timer.h
 * @brief Utility class for measuring elapsed time using a steady clock.
 */

#pragma once

#include <chrono>

namespace edf::utils {
/**
 * @class Timer
 * @brief A simple RAII-based timer that uses `std::chrono::steady_clock` to measure elapsed time.
 *
 * This class is useful for benchmarking or timing operations. Upon creation,
 * it records the current time. You can then call `elapsed()` to get the
 * duration that has passed since the timer was created.
 */
class Timer {
  public:
    /**
     * @brief Constructs a new Timer object and captures the current time.
     */
    Timer() : start_(std::chrono::steady_clock::now()) {}

    /**
     * @brief Returns the time duration since the timer was started.
     *
     * @return Duration since construction in `std::chrono::steady_clock::duration`.
     * You can cast this to seconds, milliseconds, etc., using `std::chrono::duration_cast`.
     */
    std::chrono::steady_clock::duration elapsed() const { return std::chrono::steady_clock::now() - start_; }

  private:
    std::chrono::time_point<std::chrono::steady_clock> start_;
};
} // namespace edf::utils
