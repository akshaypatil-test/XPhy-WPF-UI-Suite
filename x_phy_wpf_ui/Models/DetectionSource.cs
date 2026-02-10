namespace x_phy_wpf_ui
{
    /// <summary>
    /// Enumeration of available detection sources for deepfake detection.
    /// </summary>
    public enum DetectionSource
    {
        /// <summary>
        /// Video detection from Zoom conference calls
        /// </summary>
        ZoomConferenceVideo,
        
        /// <summary>
        /// Audio detection from Zoom conference calls
        /// </summary>
        ZoomConferenceAudio,
        
        /// <summary>
        /// Video detection from VLC media player web streams
        /// </summary>
        VLCWebStreamVideo,
        
        /// <summary>
        /// Audio detection from VLC media player web streams
        /// </summary>
        VLCWebStreamAudio,
        
        /// <summary>
        /// Video detection from YouTube web streams
        /// </summary>
        YouTubeWebStreamVideo,
        
        /// <summary>
        /// Audio detection from YouTube web streams
        /// </summary>
        YouTubeWebStreamAudio
    }
}
