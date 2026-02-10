/**
 * @file s3_writer.h
 * @brief S3 utility functions for uploading and downloading files or JSON data to/from AWS S3.
 *
 * This header provides convenience methods for interacting with AWS S3 using the AWS SDK for C++,
 * supporting both file-based and in-memory JSON uploads, as well as downloads with optional progress callbacks.
 */

#pragma once

// Following definition is needed to avoid compilation error
#define USE_IMPORT_EXPORT

#include "aws/transfer/TransferHandle.h"
#include "aws/transfer/TransferManager.h"
#include "json.hpp"

#include <vector>
#include <filesystem>
#include <map>
#include <string>
#include <optional>

namespace edf {

namespace S3Writer {
/**
 * @typedef DownloadProgressCallback
 * @brief Callback type used to report download progress during file transfers.
 */
using DownloadProgressCallback = std::function<void(const Aws::Transfer::TransferManager*,
                                                    const std::shared_ptr<const Aws::Transfer::TransferHandle>&)>;

/**
 * @brief Uploads a file to AWS S3.
 *
 * @param filePath Path to the local file to upload.
 * @param s3Key The S3 key (object path) to store the file under.
 * @param awsConfig A map containing AWS configuration settings (e.g., bucket, region, access credentials).
 * @return true if the upload succeeds, false otherwise.
 */
bool uploadToS3(const std::filesystem::path& filePath,
                const Aws::String& s3Key,
                const std::map<std::string, std::string>& awsConfig);

/**
 * @brief Uploads a JSON array to AWS S3.
 *
 * The function converts the provided JSON data into a string and uploads it to S3 as an object.
 *
 * @param jsonData Vector of JSON objects to upload.
 * @param s3Key The S3 key (object path) to store the JSON under.
 * @param awsConfig A map containing AWS configuration settings.
 * @return true if the upload succeeds, false otherwise.
 */
bool uploadToS3(const std::vector<nlohmann::json>& jsonData,
                const Aws::String& s3Key,
                const std::map<std::string, std::string>& awsConfig);

/**
 * @brief Downloads an object from S3 and returns its contents as a string.
 *
 * @param s3Key The S3 key (object path) of the file to download.
 * @param awsConfig A map containing AWS configuration settings.
 * @return An optional string containing the file's contents. Returns `std::nullopt` on failure.
 */
std::optional<std::string> downloadFromS3(const Aws::String& s3Key,
                                          const std::map<std::string, std::string>& awsConfig);

/**
 * @brief Downloads a file from S3 to a local destination.
 *
 * @param s3Key The S3 key (object path) of the file to download.
 * @param awsConfig A map containing AWS configuration settings.
 * @param localDestination Path to store the downloaded file locally.
 * @param downloadProgressCallback Optional callback function to monitor download progress.
 * @return true if the download is successful, false otherwise.
 */
bool downloadFromS3(const Aws::String& s3Key,
                    const std::map<std::string, std::string>& awsConfig,
                    const std::filesystem::path& localDestination,
                    DownloadProgressCallback downloadProgressCallback);

} // namespace S3Writer

} // namespace edf
