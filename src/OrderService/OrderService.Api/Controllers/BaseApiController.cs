public abstract class BaseApiController : ControllerBase
{
    protected Guid GetCustomerId()
    {
        var id = User.FindFirst("nameid")?.Value;
        if (string.IsNullOrEmpty(id)) throw new UnauthorizedAccessException("Missing customer id");
        return Guid.Parse(id);
    }

    protected string GetCustomerEmail()
    {
        var email = User.FindFirst("email")?.Value;
        if (string.IsNullOrEmpty(email)) throw new UnauthorizedAccessException("Missing email");
        return email;
    }
}
