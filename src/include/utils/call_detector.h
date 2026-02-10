/**
 * @file call_detector.h
 * @brief Declares the CallDetector class for detecting active conferencing applications and microphone usage.
 *
 * This utility class is designed for use on Windows systems to determine:
 *   - Whether any known conferencing applications are currently active (foreground).
 *   - Whether the system microphone is actively being used.
 *
 * Internally, it keeps track of the foreground application's PID and a configurable list of known conferencing process
 * names.
 */

#pragma once

#define NOMINMAX
#include <windows.h>

#include <set>
#include <string>

/**
 * @class CallDetector
 * @brief Utility class for detecting conferencing activity and microphone usage on Windows.
 */
class CallDetector {
  public:
    /**
     * @brief Constructor. Initializes the internal process list.
     * @param processes collection of process names to monitor, defaults to slack.exe and ms-teams.exe
     */
    CallDetector(const std::set<std::wstring>& processes = {L"slack.exe", L"ms-teams.exe"}) : processes_{processes} {};

    ~CallDetector();

    /**
     * @brief Checks if any known conferencing application is currently the foreground window.
     *
     * This checks if the current foreground process matches one of the known conferencing applications.
     *
     * @return true if an active conferencing app is detected, false otherwise.
     */
    bool is_active_conference_app();

    /**
     * @brief Checks if the microphone is currently active (being used by any process).
     *
     * @return true if the microphone is in use, false otherwise.
     */
    bool is_active_mic();

    /**
     * @brief Gets the last known detected process ID.
     *
     * @return The previously cached foreground process ID.
     */
    DWORD get_curr_pid() const { return last_pid; }

    /**
     * @brief Resets the cached process ID to 0.
     */
    void reset_pid() { last_pid = 0; }

  private:
    std::set<std::wstring> processes_;
    DWORD last_pid = 0;
};
