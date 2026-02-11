using System;

namespace x_phy_wpf_ui.Models
{
    /// <summary>
    /// Request body for POST api/Result (CreateResult).
    /// </summary>
    public class CreateResultRequest
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;       // "Audio" or "Video"
        public string Outcome { get; set; } = string.Empty;     // e.g. "AI Manipulation Detected" / "No AI Manipulation detected"
        public decimal DetectionConfidence { get; set; }         // 0–100
        public string MediaSource { get; set; } = string.Empty; // App name (Zoom, Google Chrome)
        /// <summary>Local path to evidence/artifact folder for loading images in Session Details.</summary>
        public string? ArtifactPath { get; set; }
        public decimal? Duration { get; set; }
    }

    /// <summary>
    /// Response from CreateResult.
    /// </summary>
    public class CreateResultResponse
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public decimal DetectionConfidence { get; set; }
        public string MediaSource { get; set; } = string.Empty;
        public string? ArtifactPath { get; set; }
        public decimal? Duration { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response from GET api/Result (GetResults).
    /// </summary>
    public class GetResultsResponse
    {
        public System.Collections.Generic.List<ResultDto> Results { get; set; } = new System.Collections.Generic.List<ResultDto>();
        public int TotalCount { get; set; }
    }

    public class ResultDto
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Outcome { get; set; } = string.Empty;
        public decimal DetectionConfidence { get; set; }
        public string MediaSource { get; set; } = string.Empty;
        /// <summary>Local path to evidence/artifact folder for loading images.</summary>
        public string? ArtifactPath { get; set; }
        public decimal? Duration { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
