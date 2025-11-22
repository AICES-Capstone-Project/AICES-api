using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Enum
{
    public enum SubscriptionStatusEnum
    {
        Pending ,   // Công ty vừa đăng ký, chờ admin duyệt hoặc chờ thanh toán
        Active ,    // Gói đang hoạt động
        Expired ,   // Gói đã hết hạn
        Canceled ,  // Bị hủy giữa chừng (do admin hoặc công ty)
        Failed ,    // Đăng kí thất bại
    }
}
