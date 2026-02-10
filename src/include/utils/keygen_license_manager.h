/**
 * @file keygen_license_manager.h
 * @brief The implementation of the Keygen License Server client.
 */

#pragma once

#define _TURN_OFF_PLATFORM_STRING // disables the U() macro
#include "cpprest/json.h"

#include <string>
#include <map>

namespace edf {

class ApplicationController;

} // namespace edf

namespace edf::license_manager {

/**
 * @brief Reasons for which license validation might fail.
 */
enum class LicenseValidationFailureReason {
    Invalid,
    MachineLimitExceeded,
    ActivationError,
    MetadataError,
    UnknownError,
    KeyMissing,
    ServerError,
    HttpError,
    ValidationError,
    Expired,
    NotFound,
    BadResponse,
    CannotVerifySignature
};

struct LicenseValidationFailure {
    LicenseValidationFailureReason reason;
};

class KeygenLicenseManager {
    // Sends HTTP request to Keygen to validate license key
    //   If valid -> set Keygen Machine ID
    // If machine unregistered -> then send request to register machine -> Validate license key again
    //   Based on Keygen documentation, after machine registration, repeating validation a second time should be
    //   performed (#126)

    KeygenLicenseManager(const std::map<std::string, std::string>& licenseConfig);

    std::map<std::string, std::string> getS3ResultsCredentials() const;

    std::map<std::string, std::string> getS3UpdatesCredentials() const;

    bool hasS3UploadResultsEntitlement() const;

    // Send HTTP POST request to ping heartbeat
    void pingHeartbeat();

    // Set License ID
    void setLicenseId(const web::json::value& jsonRes);

    // Get entitlements from Keygen License
    void setEntitlements();

    const std::map<std::string, std::string> configuration_;
    const std::map<std::string, std::string> systemInformation_;
    bool entitlementContainsS3UploadResults_;
    std::map<std::string, std::string> s3ResultsCredentials_;
    std::map<std::string, std::string> s3UpdatesCredentials_;
    utility::string_t keygenLicenseId_;
    utility::string_t keygenMachineId_;

    friend class edf::ApplicationController;
};

} // namespace edf::license_manager
