using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

public abstract class BaseApiController : ControllerBase
{
    protected Guid GetCustomerId()
    {
        var id = User.FindFirst("nameid")?.Value;
        // Trả về Guid.Empty thay vì ném lỗi
        return Guid.TryParse(id, out var guid) ? guid : Guid.Empty; 
    }
    protected string GetCustomerEmail()
    {
        // Thử tìm bằng cả hai key phổ biến
        var email = User.FindFirst("email")?.Value 
                  ?? User.FindFirst(ClaimTypes.Email)?.Value; 
        
        // Trả về rỗng thay vì ném lỗi
        return email ?? string.Empty;
    }
}
