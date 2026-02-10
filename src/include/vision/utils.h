/**
 * @file utils.h
 * @brief Utility functions and classes for vision-related tasks using OpenCV.
 *
 * This file includes tools for:
 *   - Capturing the screen as OpenCV matrices
 *   - Arranging images in a grid layout
 *   - Drawing contours on face images for visual inspection
 */

#pragma once

#include "utils/logger.h"

#pragma warning(push)
#pragma warning(disable : 6269 26495 6294 6201)
#include "opencv2/opencv.hpp"
#pragma warning(pop)

namespace edf::vision::utils {

/**
 * @brief Captures the current screen(s) and returns each as a cv::Mat.
 *
 * @return A vector of `cv::Mat` objects representing the captured screen images.
 */
std::vector<cv::Mat> captureScreenMats();

/**
 * @class Grid
 * @brief Utility class for arranging multiple OpenCV images (cells) into a grid layout.
 */
class Grid {
  public:
    /**
     * @brief Constructs a Grid to hold a specific number of cells, each with a given size.
     *
     * @param num_cells The total number of cells the grid can hold.
     * @param cell_size The size (width and height) of each cell in the grid.
     */
    Grid(int num_cells, cv::Size cell_size) : current_index(0), num_cells(num_cells), cell_size(cell_size) {
        auto cols = num_cells > 1 ? 2 : 1;
        grid.cols = cell_size.width * cols;
        grid.rows = cell_size.height * ((num_cells + cols - 1) / cols);
        grid      = cv::Mat::zeros(grid.size(), CV_8UC3);
    }

    /**
     * @brief Appends an image (cell) into the next available grid slot.
     *
     * @param cell The image to append into the grid.
     *
     * Logs an error if the grid is already full.
     */
    void append(cv::Mat cell) {
        if (current_index >= num_cells) {
            LOG_ERROR("Grid is full. Cannot append cell.");
            return;
        }
        auto cols = grid.cols / cell_size.width;
        cell.copyTo(grid(cv::Rect((current_index % cols) * cell_size.width,
                                  (current_index / cols) * cell_size.height,
                                  cell_size.width,
                                  cell_size.height)));
        ++current_index;
    }

    /**
     * @brief Returns the full grid as a single OpenCV matrix.
     *
     * @return The composed grid image.
     */
    cv::Mat mat() { return grid; }

  private:
    int current_index;
    size_t num_cells;
    cv::Size cell_size;
    cv::Mat grid;
};

/**
 * @brief Draws contours on a face image based on a binary mask, marking it as real or fake.
 *
 * @param face The image on which contours will be drawn.
 * @param mask The binary mask indicating the region to outline.
 * @param isFake Boolean indicating whether the region is fake or genuine.
 */
void drawContours(cv::Mat face, cv::Mat mask, bool isFake);

} // namespace edf::vision::utils
