#pragma once

#include <vcclr.h>
#include <string>

using namespace System;

// Forward declaration
class CallDetector;

namespace XPhyWrapper {

    // Managed wrapper for CallDetector
    public ref class CallDetectorWrapper
    {
    public:
        CallDetectorWrapper();
        ~CallDetectorWrapper();
        !CallDetectorWrapper();

        // Check if any known conferencing application is currently active
        bool IsActiveConferenceApp();

        // Get the name of the detected process (e.g., "Zoom.exe", "ms-teams.exe")
        String^ GetDetectedProcessName();

    private:
        CallDetector* detector_;
    };

}
