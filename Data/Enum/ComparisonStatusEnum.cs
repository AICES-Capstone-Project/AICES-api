namespace Data.Enum
{
    /// <summary>
    /// Status for comparison process
    /// </summary>
    public enum ComparisonStatusEnum
    {
        /// <summary>
        /// Comparison is pending (waiting for AI)
        /// </summary>
        Pending,
        
        /// <summary>
        /// Comparison is being processed by AI
        /// </summary>
        Processing,
        
        /// <summary>
        /// Comparison completed successfully
        /// </summary>
        Completed,
        
        /// <summary>
        /// Comparison failed
        /// </summary>
        Failed
    }
}
