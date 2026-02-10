#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <psapi.h>
#include "CallDetectorWrapper.h"
#include "utils/call_detector.h"
#include <set>
#include <string>

using namespace System;

namespace XPhyWrapper {

    CallDetectorWrapper::CallDetectorWrapper()
    {
        // Initialize with common video conferencing apps including Zoom
        std::set<std::wstring> processes = {
            L"slack.exe",
            L"ms-teams.exe",
            L"Zoom.exe",
            L"CptHost.exe",  // Zoom's process name
            L"zoom.exe"
        };
        detector_ = new CallDetector(processes);
    }

    CallDetectorWrapper::~CallDetectorWrapper()
    {
        this->!CallDetectorWrapper();
    }

    CallDetectorWrapper::!CallDetectorWrapper()
    {
        if (detector_) {
            delete detector_;
            detector_ = nullptr;
        }
    }

    bool CallDetectorWrapper::IsActiveConferenceApp()
    {
        if (!detector_) {
            return false;
        }
        return detector_->is_active_conference_app();
    }

    String^ CallDetectorWrapper::GetDetectedProcessName()
    {
        if (!detector_) {
            return nullptr;
        }
        
        // Note: CallDetector doesn't directly expose which process was detected
        // We'll need to check the process list manually or extend CallDetector
        // For now, return a generic string if detected
        if (detector_->is_active_conference_app()) {
            // Try to get the foreground window process name
            HWND hwnd = GetForegroundWindow();
            if (hwnd) {
                DWORD processId;
                GetWindowThreadProcessId(hwnd, &processId);
                
                HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, processId);
                if (hProcess) {
                    wchar_t processName[MAX_PATH] = {0};
                    DWORD size = MAX_PATH;
                    if (QueryFullProcessImageNameW(hProcess, 0, processName, &size)) {
                        // Extract just the filename
                        std::wstring fullPath(processName);
                        size_t pos = fullPath.find_last_of(L"\\");
                        if (pos != std::wstring::npos) {
                            std::wstring filename = fullPath.substr(pos + 1);
                            CloseHandle(hProcess);
                            return gcnew String(filename.c_str());
                        }
                    }
                    CloseHandle(hProcess);
                }
            }
            return gcnew String(L"Conference App");
        }
        return nullptr;
    }

}
