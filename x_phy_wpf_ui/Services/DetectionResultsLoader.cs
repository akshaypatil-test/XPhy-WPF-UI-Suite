using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    /// <summary>
    /// Loads detection results from the SQLite database in the X-PHY results folder.
    /// Database schema matches the C++ edf::db (faces and voices tables).
    /// </summary>
    public static class DetectionResultsLoader
    {
        private const string DbFileName = "db.sqlite";

        /// <summary>
        /// Gets the default results directory path when controller is not available.
        /// </summary>
        public static string GetDefaultResultsDir()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "X-PHY", "X-PHY Deepfake Detector", "Deepfake - results");
        }

        /// <summary>
        /// Loads all detection results from the SQLite DB in the given results directory.
        /// Returns a combined list of face and voice results, sorted by timestamp descending.
        /// </summary>
        public static List<DetectionResultItem> LoadFromResultsDir(string resultsDir)
        {
            var list = new List<DetectionResultItem>();
            if (string.IsNullOrEmpty(resultsDir) || !Directory.Exists(resultsDir))
                return list;

            string dbPath = Path.Combine(resultsDir, DbFileName);
            if (!File.Exists(dbPath))
                return list;

            try
            {
                var connStr = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
                using (var conn = new SqliteConnection(connStr))
                {
                    conn.Open();

                    // Load faces: timestamp is stored as Unix milliseconds or seconds in C++ (long long)
                    const string faceSql = "SELECT serial_number, timestamp, prob_fake_score, artifact_location FROM faces ORDER BY timestamp DESC";
                    using (var cmd = new SqliteCommand(faceSql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            long ts = r.GetInt64(1);
                            double score = r.GetDouble(2);
                            string artifact = r.IsDBNull(3) ? null : r.GetString(3);
                            int pct = (int)Math.Round(score * 100);
                            bool isFake = score >= 0.5;
                            list.Add(new DetectionResultItem
                            {
                                SerialNumber = r.GetInt32(0),
                                Timestamp = UnixMillisToDateTime(ts),
                                Type = "Video",
                                IsAiManipulationDetected = isFake,
                                ConfidencePercent = Math.Min(100, Math.Max(0, pct)),
                                ResultPathOrId = artifact ?? "",
                                MediaSourceDisplay = "Local" // Local DB has no app name; show "Local"
                            });
                        }
                    }

                    // Load voices
                    const string voiceSql = "SELECT serial_number, timestamp, score, artifact_location FROM voices ORDER BY timestamp DESC";
                    using (var cmd = new SqliteCommand(voiceSql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            long ts = r.GetInt64(1);
                            double score = r.GetDouble(2);
                            string artifact = r.IsDBNull(3) ? null : r.GetString(3);
                            int pct = (int)Math.Round(score * 100);
                            bool isFake = score >= 0.5;
                            list.Add(new DetectionResultItem
                            {
                                SerialNumber = r.GetInt32(0),
                                Timestamp = UnixMillisToDateTime(ts),
                                Type = "Audio",
                                IsAiManipulationDetected = isFake,
                                ConfidencePercent = Math.Min(100, Math.Max(0, pct)),
                                ResultPathOrId = artifact ?? "",
                                MediaSourceDisplay = "Local"
                            });
                        }
                    }
                }

                list.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DetectionResultsLoader: Failed to read DB: {ex.Message}");
            }

            return list;
        }

        private static DateTime UnixMillisToDateTime(long unixMillis)
        {
            // C++ timestamp may be in seconds or milliseconds
            if (unixMillis < 1e12)
                unixMillis *= 1000;
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMillis).LocalDateTime;
        }
    }
}
