/**
 * @file logger.h
 * @brief Logger utility for application-wide logging using spdlog.
 *
 * This file provides a singleton-like logger class with global macros
 * for different log levels (trace, debug, info, warn, error, critical).
 */

#pragma once

#ifndef PROD_MODE
#define SPDLOG_ACTIVE_LEVEL SPDLOG_LEVEL_DEBUG
#else
#define SPDLOG_ACTIVE_LEVEL SPDLOG_LEVEL_INFO
#endif

#define SPDLOG_WCHAR_TO_UTF8_SUPPORT

#pragma warning(push)
#pragma warning(disable : 26498)
#include "spdlog/spdlog.h"
#pragma warning(pop)

#include <memory>
#include <filesystem>

namespace edf {
/**
 * @class Logger
 * @brief Centralized logging manager based on spdlog.
 *
 * This class provides a static interface to initialize and access
 * a shared logger instance. The logger is initialized with a file sink
 * pointing to the specified logs directory.
 */
class Logger {
    static std::shared_ptr<spdlog::logger> appLogger;

  public:
    /**
     * @brief Constructs a Logger instance (not usually used directly).
     */
    Logger();

    /**
     * @brief Destroys the Logger instance.
     */
    ~Logger();

    /**
     * @brief Initializes the logger and sets the output log file location.
     *
     * @param logs_dir The directory path where log files should be saved.
     */
    static void intialise(const std::filesystem::path& logs_dir);

    /**
     * @brief Returns a reference to the shared logger instance.
     *
     * @return Reference to the `std::shared_ptr<spdlog::logger>`.
     */
    static std::shared_ptr<spdlog::logger>& getLogger() { return appLogger; };
};
} // namespace edf

// Logging macros for global use
#define LOG_TRACE(...) SPDLOG_LOGGER_TRACE(edf::Logger::getLogger(), __VA_ARGS__)
#define LOG_DEBUG(...) SPDLOG_LOGGER_DEBUG(edf::Logger::getLogger(), __VA_ARGS__)
#define LOG_INFO(...) SPDLOG_LOGGER_INFO(edf::Logger::getLogger(), __VA_ARGS__)
#define LOG_WARN(...) SPDLOG_LOGGER_WARN(edf::Logger::getLogger(), __VA_ARGS__)
#define LOG_ERROR(...) SPDLOG_LOGGER_ERROR(edf::Logger::getLogger(), __VA_ARGS__)
#define LOG_CRITICAL(...) SPDLOG_LOGGER_CRITICAL(edf::Logger::getLogger(), __VA_ARGS__)
