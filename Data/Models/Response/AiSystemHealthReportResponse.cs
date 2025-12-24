namespace Data.Models.Response
{
    /// <summary>
    /// Chỉ số Sức khỏe AI & Lỗi hệ thống (System & AI Health)
    /// 4 chỉ số chính:
    /// 1. Tỷ lệ Screening Thành công (AI Success Rate)
    /// 2. Tỷ lệ Lỗi Parsing (Parsing Error Rate)
    /// 3. Phân tích nguyên nhân lỗi
    /// 4. Thời gian xử lý trung bình (Latency)
    /// </summary>
    public class AiSystemHealthReportResponse
    {
        /// <summary>
        /// 1. Tỷ lệ Screening Thành công (AI Success Rate)
        /// % số lượng CV đã được phân tích thành công trên tổng số CV tải lên
        /// </summary>
        public decimal SuccessRate { get; set; }

        /// <summary>
        /// 2. Tỷ lệ Lỗi Parsing (Parsing Error Rate)
        /// % số lượng CV bị lỗi phân tích
        /// </summary>
        public decimal ErrorRate { get; set; }

        /// <summary>
        /// 3. Phân tích nguyên nhân lỗi chi tiết - Danh sách các loại lỗi
        /// </summary>
        public List<ErrorReasonItem> ErrorReasons { get; set; } = new();

        /// <summary>
        /// 4. Thời gian xử lý trung bình (Average Latency) - tính bằng giây
        /// Mất bao nhiêu giây để AI chấm điểm xong 1 CV
        /// </summary>
        public decimal AverageProcessingTimeSeconds { get; set; }
    }

    /// <summary>
    /// Chi tiết một loại lỗi trong phân tích nguyên nhân lỗi
    /// </summary>
    public class ErrorReasonItem
    {
        /// <summary>
        /// Loại lỗi (Lỗi định dạng, Lỗi ngôn ngữ, Lỗi cấu trúc, Lỗi khác)
        /// </summary>
        public string ErrorType { get; set; } = string.Empty;

        /// <summary>
        /// Số lượng CV bị lỗi này
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Tỷ lệ phần trăm so với tổng lỗi
        /// </summary>
        public decimal Percentage { get; set; }
    }
}
