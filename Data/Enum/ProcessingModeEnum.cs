namespace Data.Enum
{
    /// <summary>
    /// Processing mode for resume applications
    /// </summary>
    public enum ProcessingModeEnum
    {
        /// <summary>
        /// Parse resume from file (first time upload)
        /// </summary>
        Parse,
        
        /// <summary>
        /// Score resume with existing parsed data (different job)
        /// </summary>
        Score,
        
        /// <summary>
        /// Clone result from existing application (same resume + same job)
        /// </summary>
        Clone,
    }
}
