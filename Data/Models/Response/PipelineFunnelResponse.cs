using System.Collections.Generic;

namespace Data.Models.Response
{
    public class PipelineFunnelResponse
    {
        public List<PipelineStageResponse> Stages { get; set; } = new();
    }

    public class PipelineStageResponse
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal ConversionRate { get; set; }
    }
}

