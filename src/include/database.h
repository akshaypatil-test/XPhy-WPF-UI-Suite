#pragma once

#include "sqlite_orm/sqlite_orm.h"

#include <filesystem>
#include <string>
#include <vector>
#include <memory>

namespace edf::db {
struct Face {
    int serial_number                   = -1;
    long long timestamp                 = 0;
    float prob_fake_score               = 0;
    float contour_ratio                 = 0;
    float proportion_of_fakes           = 0;
    float prob_fake_threshold           = 0;
    float fake_and_contour_threshold    = 0;
    float mask_threshold                = 0;
    float proportion_of_fakes_threshold = 0;
    std::string model_identifier;
    bool background_run = false;
    std::string artifact_location;
    std::shared_ptr<std::string> raw_artifact_location;
    size_t grid_index    = 0;
    bool uploaded        = false;
    bool deleted_locally = false;
};

struct Voice {
    int serial_number                   = -1;
    long long timestamp                 = 0;
    float score                         = 0;
    float proportion_of_fakes           = 0;
    float threshold                     = 0;
    float proportion_of_fakes_threshold = 0;
    std::string model_identifier;
    bool use_win_reverser = false;
    bool background_run   = false;
    std::string artifact_location;
    bool uploaded        = false;
    bool deleted_locally = false;
};

inline auto initStorage(const std::filesystem::path& path) {
    using namespace sqlite_orm;
    return make_storage(path.string(),
                        make_table("faces",
                                   make_column("serial_number", &Face::serial_number, primary_key()),
                                   make_column("timestamp", &Face::timestamp),
                                   make_column("prob_fake_score", &Face::prob_fake_score),
                                   make_column("contour_ratio", &Face::contour_ratio),
                                   make_column("proportion_of_fakes", &Face::proportion_of_fakes),
                                   make_column("prob_fake_threshold", &Face::prob_fake_threshold),
                                   make_column("fake_and_contour_threshold", &Face::fake_and_contour_threshold),
                                   make_column("mask_threshold", &Face::mask_threshold),
                                   make_column("proportion_of_fakes_threshold", &Face::proportion_of_fakes_threshold),
                                   make_column("model_identifier", &Face::model_identifier),
                                   make_column("background_run", &Face::background_run),
                                   make_column("artifact_location", &Face::artifact_location),
                                   make_column("raw_artifact_location", &Face::raw_artifact_location),
                                   make_column("grid_index", &Face::grid_index),
                                   make_column("uploaded", &Face::uploaded),
                                   make_column("deleted_locally", &Face::deleted_locally, default_value(false))),

                        make_table("voices",
                                   make_column("serial_number", &Voice::serial_number, primary_key()),
                                   make_column("timestamp", &Voice::timestamp),
                                   make_column("score", &Voice::score),
                                   make_column("proportion_of_fakes", &Voice::proportion_of_fakes),
                                   make_column("threshold", &Voice::threshold),
                                   make_column("proportion_of_fakes_threshold", &Voice::proportion_of_fakes_threshold),
                                   make_column("model_identifier", &Voice::model_identifier),
                                   make_column("use_win_reverser", &Voice::use_win_reverser),
                                   make_column("background_run", &Voice::background_run),
                                   make_column("artifact_location", &Voice::artifact_location),
                                   make_column("uploaded", &Voice::uploaded),
                                   make_column("deleted_locally", &Voice::deleted_locally, default_value(false))));
}

using Storage = decltype(initStorage(""));

class Database {
  public:
    const std::filesystem::path db_file_name = "db.sqlite";
    Database(const std::filesystem::path& db_dir);
    void insert(Face);
    void insert(Voice);
    std::vector<Face> get_all_faces() const;
    std::vector<Voice> get_all_voices() const;
    std::vector<Face> get_faces_older_than(long long age) const;
    std::vector<Voice> get_voices_older_than(long long age) const;
    std::vector<Face> get_not_uploaded_faces() const;
    std::vector<Voice> get_not_uploaded_voices() const;
    void mark_as_uploaded(Face);
    void mark_as_uploaded(Voice);
    void mark_as_deleted(Face);
    void mark_as_deleted(Voice);

  private:
    std::unique_ptr<Storage> storage;
    const std::filesystem::path db_path;
};
} // namespace edf::db
