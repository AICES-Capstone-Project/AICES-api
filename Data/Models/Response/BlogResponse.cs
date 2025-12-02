using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response
{
    public class BlogResponse
    {
        public int BlogId { get; set; }
        public int UserId { get; set; }
        public string? AuthorName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}

