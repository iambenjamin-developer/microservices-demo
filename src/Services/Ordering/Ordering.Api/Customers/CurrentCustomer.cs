using Microsoft.Extensions.Options;

namespace Ordering.Api.Customers;

/// <summary>
/// The point of sale placing or reading orders, bound as an endpoint parameter (<see cref="BindAsync"/>).
/// </summary>
/// <remarks>
/// TODO(phase 6): read the id and e-mail from the JWT claims validated by the Gateway and remove the headers.
/// Until then the optional <c>X-Customer-Id</c> / <c>X-Customer-Email</c> headers select the customer (handy to
/// test isolation between customers), falling back to the configured demo point of sale.
/// </remarks>
public sealed record CurrentCustomer(string Id, string Email)
{
    public const string IdHeader = "X-Customer-Id";
    public const string EmailHeader = "X-Customer-Email";

    public static ValueTask<CurrentCustomer?> BindAsync(HttpContext context)
    {
        var demoCustomer = context.RequestServices.GetRequiredService<IOptions<DemoCustomerOptions>>().Value;

        var id = context.Request.Headers[IdHeader].ToString();
        var email = context.Request.Headers[EmailHeader].ToString();

        return ValueTask.FromResult<CurrentCustomer?>(new CurrentCustomer(
            string.IsNullOrWhiteSpace(id) ? demoCustomer.Username : id.Trim(),
            string.IsNullOrWhiteSpace(email) ? demoCustomer.Email : email.Trim()));
    }
}

/// <summary>Demo point of sale (section <c>DemoUsers:PointOfSale</c>, shared with the Gateway in phase 6).</summary>
public sealed class DemoCustomerOptions
{
    public const string SectionName = "DemoUsers:PointOfSale";

    public string Username { get; set; } = "bar";

    public string Email { get; set; } = "bar@example.com";
}
