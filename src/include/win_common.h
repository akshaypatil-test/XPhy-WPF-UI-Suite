/**
 * @file win_common.h
 * @brief Common Windows-specific UI and audio utility components for the application.
 */

#pragma once

#include "utils/logger.h"
#include "voice/audio_capture.h"

#include "toml.hpp"

#include <format>
#include <fstream>

namespace edf::windows {

/**
 * @brief Captures audio from the system and enqueues it into the provided queue.
 *
 * @param run Atomic flag to control capture loop termination.
 * @param captureDurationSecs Duration of each capture window in seconds.
 * @param captureQueue The queue into which audio buffers are pushed.
 * @throws std::system_error
 */
void voiceCapture(std::atomic_bool& run,
                  int captureDurationSecs,
                  moodycamel::ReaderWriterQueue<voice::AudioBuffer>& captureQueue) {
    LOG_DEBUG("Start capturing audio");
    try {
        voice::AudioCapture audio{voice::AudioType::Stereo};
        while (run) {
            bool succeeded = captureQueue.enqueue(audio.read(captureDurationSecs));
            if (!succeeded) {
                run = false;
                break;
            }
        }
    } catch (...) {
        run = false;
        throw;
    }
    LOG_DEBUG("Stop capturing audio");
}

/**
 * @class ProgressBar
 * @brief A simple modal Windows progress bar dialog.
 */
class ProgressBar {
  public:
    /**
     * @brief Construct the progress bar UI.
     *
     * @param hwndParent Handle to the parent window.
     * @param hInstance Application instance handle.
     * @param title Dialog window title.
     */
    ProgressBar(HWND hwndParent, HINSTANCE hInstance, const wchar_t* title) {
        hwndDialog = CreateWindowEx(0,
                                    WC_DIALOG,
                                    title,
                                    WS_OVERLAPPED | WS_MINIMIZEBOX | WS_SYSMENU | WS_VISIBLE | CS_NOCLOSE,
                                    600,
                                    300,
                                    300,
                                    120,
                                    hwndParent,
                                    nullptr,
                                    hInstance,
                                    nullptr);
        auto hIcon = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_SMALL));
        SendMessage(hwndDialog, WM_SETICON, ICON_SMALL, (LPARAM)hIcon);
        hwndPB = CreateWindowEx(0,
                                PROGRESS_CLASS,
                                nullptr,
                                WS_CHILD | WS_VISIBLE,
                                40,
                                20,
                                200,
                                20,
                                hwndDialog,
                                nullptr,
                                hInstance,
                                nullptr);
    }

    ProgressBar(const ProgressBar&)            = delete;
    ProgressBar& operator=(const ProgressBar&) = delete;

    /**
     * @brief Updates the progress bar's position.
     *
     * @param progress Progress percentage (0 to 100).
     */
    void update(size_t progress) { SendMessage(hwndPB, PBM_SETPOS, progress, 0); }

    ~ProgressBar() {
        DestroyWindow(hwndPB);
        DestroyWindow(hwndDialog);
    }

  private:
    HWND hwndPB;
    HWND hwndDialog;
};

/**
 * @class EULADialog
 * @brief Dialog that prompts the user to accept or reject a license agreement (EULA).
 */
class EULADialog {
  public:
    /**
     * @brief Create the EULA dialog UI.
     *
     * @param hwndParent Parent window handle.
     * @param hInstance Application instance handle.
     * @param title Dialog window title.
     * @param filePath Path to the license agreement document.
     * @param accept Menu ID for the accept action.
     * @param reject Menu ID for the reject action.
     */
    EULADialog(HWND hwndParent,
               HINSTANCE hInstance,
               const wchar_t* title,
               const std::filesystem::path& filePath,
               HMENU accept,
               HMENU reject) {
        hwndParent_ = hwndParent;
        WNDCLASSEX wcex{};
        GetClassInfoEx(nullptr, WC_DIALOG, &wcex);
        wcex.cbSize        = sizeof(WNDCLASSEX);
        wcex.lpfnWndProc   = EULADialog::proc;
        wcex.hInstance     = hInstance;
        wcex.hIcon         = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_SMALL));
        wcex.lpszClassName = TEXT("euladialog");
        wcex.style |= CS_NOCLOSE;

        RegisterClassEx(&wcex);

        hwndDialog_ = CreateWindowEx(0,
                                     wcex.lpszClassName,
                                     title,
                                     WS_OVERLAPPED | WS_SYSMENU | WS_VISIBLE,
                                     600,
                                     300,
                                     300,
                                     200,
                                     nullptr,
                                     nullptr,
                                     hInstance,
                                     this);
        auto text   = std::format("Please accept the <A HREF=\"file:///{}\">license agreeement</A> to proceed.",
                                std::filesystem::absolute(filePath).string());
        hwndLink_   = CreateWindowExA(0,
                                    "SysLink",
                                    text.c_str(),
                                    WS_VISIBLE | WS_CHILD | ES_MULTILINE,
                                    40,
                                    20,
                                    200,
                                    40,
                                    hwndDialog_,
                                    nullptr,
                                    hInstance,
                                    nullptr);
        hwndAccept_ = CreateWindowExA(0,
                                      "button",
                                      "Accept",
                                      WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON,
                                      40,
                                      80,
                                      100,
                                      40,
                                      hwndDialog_,
                                      accept,
                                      hInstance,
                                      nullptr);
        hwndReject_ = CreateWindowExA(0,
                                      "button",
                                      "Reject",
                                      WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON,
                                      150,
                                      80,
                                      100,
                                      40,
                                      hwndDialog_,
                                      reject,
                                      hInstance,
                                      nullptr);
        reject_     = reject;
    }

    EULADialog(const EULADialog&)            = delete;
    EULADialog& operator=(const EULADialog&) = delete;

    ~EULADialog() {
        DestroyWindow(hwndAccept_);
        DestroyWindow(hwndReject_);
        DestroyWindow(hwndLink_);
        DestroyWindow(hwndDialog_);
    }

  private:
    static LRESULT CALLBACK proc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) {
        EULADialog* pThis = nullptr;

        if (message == WM_NCCREATE) {
            CREATESTRUCT* pCreate = reinterpret_cast<CREATESTRUCT*>(lParam);
            pThis                 = reinterpret_cast<EULADialog*>(pCreate->lpCreateParams);
            SetWindowLongPtr(hWnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pThis));
        } else {
            pThis = reinterpret_cast<EULADialog*>(GetWindowLongPtr(hWnd, GWLP_USERDATA));
        }
        switch (message) {
        case WM_NOTIFY:
            switch (((LPNMHDR)lParam)->code) {
            case NM_CLICK:
            case NM_RETURN: {
                PNMLINK pNMLink = reinterpret_cast<PNMLINK>(lParam);
                LITEM item      = pNMLink->item;
                ShellExecute(NULL, L"open", item.szUrl, NULL, NULL, SW_SHOW);
                break;
            }
            }
            break;
        case WM_COMMAND: {
            switch (HIWORD(wParam)) {
            case BN_CLICKED:
                auto code = LOWORD(wParam);
                PostMessageW(pThis->hwndParent_, WM_COMMAND, code, 0);
                break;
            }
        } break;
        case WM_CLOSE: {
            PostMessageW(pThis->hwndParent_, WM_COMMAND, MAKEWPARAM(pThis->reject_, 0), 0);
        } break;
        default:
            return DefWindowProc(hWnd, message, wParam, lParam);
        }
        return 0;
    }

    HWND hwndLink_;
    HWND hwndDialog_;
    HWND hwndAccept_;
    HWND hwndReject_;
    HWND hwndParent_;
    HMENU reject_;
};

/**
 * @class LicenseKeyDialog
 * @brief Dialog for displaying, editing, and saving a license key to a config file.
 */
class LicenseKeyDialog {
  public:
    /**
     * @brief Construct the license key editing dialog.
     *
     * @param hInstance Application instance handle.
     * @param title Dialog window title.
     * @param configPath Path to the TOML config file.
     * @param restartUtilPath Path to the executable used to restart the app.
     * @param exeName Name of the main executable (used in restart command).
     * @param mainWindow Optional main application window handle. If provided, will be sent WM_DESTROY.
     */
    LicenseKeyDialog(HINSTANCE hInstance,
                     const wchar_t* title,
                     const std::filesystem::path& configPath,
                     const std::filesystem::path& restartUtilPath,
                     const std::string& exeName,
                     HWND mainWindow = nullptr) {
        configPath_      = configPath;
        restartUtilPath_ = restartUtilPath;
        exeName_         = exeName;
        hwndMain_        = mainWindow;
        WNDCLASSEX wcex{};
        GetClassInfoEx(nullptr, WC_DIALOG, &wcex);
        wcex.cbSize        = sizeof(WNDCLASSEX);
        wcex.lpfnWndProc   = LicenseKeyDialog::proc;
        wcex.hInstance     = hInstance;
        wcex.hIcon         = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_SMALL));
        wcex.lpszClassName = TEXT("licensekeydialog");
        wcex.style |= CS_NOCLOSE;

        RegisterClassEx(&wcex);

        hwndDialog_ = CreateWindowEx(0,
                                     wcex.lpszClassName,
                                     title,
                                     WS_OVERLAPPED | WS_SYSMENU | WS_VISIBLE,
                                     600,
                                     300,
                                     400,
                                     200,
                                     nullptr,
                                     nullptr,
                                     hInstance,
                                     this);
        hwndLabel_  = CreateWindowExA(0,
                                     "STATIC",
                                     "Current license key:",
                                     WS_VISIBLE | WS_CHILD,
                                     20,
                                     20,
                                     300,
                                     20,
                                     hwndDialog_,
                                     nullptr,
                                     hInstance,
                                     nullptr);

        hwndInput_ = CreateWindowExA(0,
                                     "EDIT",
                                     "",
                                     WS_VISIBLE | WS_CHILD | WS_BORDER | ES_AUTOHSCROLL,
                                     20,
                                     50,
                                     350,
                                     25,
                                     hwndDialog_,
                                     nullptr,
                                     hInstance,
                                     nullptr);

        // Display license key from config.toml
        loadCurrentLicenseKey();
        SetFocus(hwndInput_);

        hwndSave_ = CreateWindowExA(0,
                                    "button",
                                    "Save",
                                    WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON,
                                    120,
                                    120,
                                    80,
                                    30,
                                    hwndDialog_,
                                    (HMENU)LICENSE_KEY_SAVE,
                                    hInstance,
                                    nullptr);

        hwndCancel_ = CreateWindowExA(0,
                                      "button",
                                      "Cancel",
                                      WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON,
                                      220,
                                      120,
                                      80,
                                      30,
                                      hwndDialog_,
                                      (HMENU)LICENSE_KEY_CANCEL,
                                      hInstance,
                                      nullptr);
    }

    LicenseKeyDialog(const LicenseKeyDialog&)            = delete;
    LicenseKeyDialog& operator=(const LicenseKeyDialog&) = delete;

    ~LicenseKeyDialog() {
        DestroyWindow(hwndSave_);
        DestroyWindow(hwndCancel_);
        DestroyWindow(hwndInput_);
        DestroyWindow(hwndLabel_);
        DestroyWindow(hwndDialog_);
    }

    /**
     * @brief Returns the HWND handle to the dialog window.
     */
    HWND getDialogHandle() const { return hwndDialog_; }

    /**
     * @brief Validates (ignores spaces anywhere) and saves the license key to the config file.
     *
     * @return true if save was successful, false otherwise.
     */
    bool saveLicenseKey() {
        char buffer[256];
        GetWindowTextA(hwndInput_, buffer, sizeof(buffer));
        std::string newLicenseKey(buffer);
        std::erase_if(newLicenseKey, [](unsigned char c) { return std::isspace(c); });

        if (newLicenseKey.empty()) {
            MessageBoxA(hwndDialog_, "License key cannot be empty.", "Error", MB_OK | MB_ICONERROR);
            return false;
        }

        return updateConfigFile(newLicenseKey);
    }

  private:
    static LRESULT CALLBACK proc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) {
        LicenseKeyDialog* pThis = nullptr;

        if (message == WM_NCCREATE) {
            CREATESTRUCT* pCreate = reinterpret_cast<CREATESTRUCT*>(lParam);
            pThis                 = reinterpret_cast<LicenseKeyDialog*>(pCreate->lpCreateParams);
            SetWindowLongPtr(hWnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pThis));
        } else {
            pThis = reinterpret_cast<LicenseKeyDialog*>(GetWindowLongPtr(hWnd, GWLP_USERDATA));
        }

        switch (message) {
        case WM_COMMAND: {
            switch (HIWORD(wParam)) {
            case BN_CLICKED:
                auto code = LOWORD(wParam);
                if (code == LICENSE_KEY_SAVE) {
                    if (pThis->saveLicenseKey()) {
                        MessageBoxA(
                            pThis->hwndDialog_,
                            "License key updated successfully! The app will now relaunch for it to take effect.",
                            "Success",
                            MB_OK | MB_ICONINFORMATION);
                        auto args    = std::format("{} \"{}\"", GetCurrentProcessId(), pThis->exeName_);
                        auto batPath = pThis->restartUtilPath_.string();
                        ShellExecuteA(nullptr, "open", batPath.c_str(), args.c_str(), nullptr, SW_HIDE);
                        DestroyWindow(pThis->hwndMain_ ? pThis->hwndMain_ : hWnd);
                    }
                } else if (code == LICENSE_KEY_CANCEL) {
                    DestroyWindow(hWnd);
                }
                break;
            }
        } break;
        case WM_SETFOCUS: {
            SetFocus(pThis->hwndInput_);
        } break;
        default:
            return DefWindowProc(hWnd, message, wParam, lParam);
        }
        return 0;
    }

    void loadCurrentLicenseKey() {
        try {
            if (!std::filesystem::exists(configPath_)) {
                SetWindowTextA(hwndInput_, "[No config file found]");
                return;
            }

            toml::table config = toml::parse_file(configPath_.string());

            auto configTable = config["license"].as_table();
            if (configTable) {
                auto key = configTable->at_path("KEY").value<std::string>();
                if (key.has_value())
                    SetWindowTextA(hwndInput_, key.value().c_str());
                else
                    SetWindowTextA(hwndInput_, "[No license key found]");
            } else
                SetWindowTextA(hwndInput_, "[No license section found]");

        } catch (const std::exception& e) {
            std::string errorText = "[Error reading config: ";
            errorText += e.what();
            errorText += "]";
            SetWindowTextA(hwndInput_, errorText.c_str());
        }
    }

    bool updateConfigFile(const std::string& newLicenseKey) {
        try {
            toml::table config;
            if (std::filesystem::exists(configPath_)) {
                config = toml::parse_file(configPath_.string());
            }

            if (!config.contains("license")) {
                config.insert("license", toml::table{});
            }

            auto licenseTable = config["license"].as_table();
            if (!licenseTable) {
                MessageBoxA(hwndDialog_, "Failed to access license table", "Error", MB_OK | MB_ICONERROR);
                return false;
            }

            licenseTable->insert_or_assign("KEY", newLicenseKey);

            std::ofstream file(configPath_, std::ios::out | std::ios::trunc);
            if (!file.is_open()) {
                MessageBoxA(hwndDialog_, "Failed to open config file for writing", "Error", MB_OK | MB_ICONERROR);
                return false;
            }

            file << config;
            file.flush();
            file.close();

            return true;

        } catch (const std::exception& e) {
            std::string errorMsg = "Failed to update config file: ";
            errorMsg += e.what();
            MessageBoxA(hwndDialog_, errorMsg.c_str(), "Error", MB_OK | MB_ICONERROR);
            return false;
        }
    }

    HWND hwndLabel_;
    HWND hwndDialog_;
    HWND hwndInput_;
    HWND hwndSave_;
    HWND hwndCancel_;
    HWND hwndMain_;
    std::filesystem::path configPath_;
    std::filesystem::path restartUtilPath_;
    std::string exeName_;
};

} // namespace edf::windows
