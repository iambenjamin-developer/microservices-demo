using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Ordering.Api.Customers;

/// <summary>
/// Binds <see cref="CurrentCustomer"/> from the validated token. It runs with the other parameters, before
/// <c>[ApiController]</c> checks the body, so a token that identifies nobody is a 401 even when the body is invalid.
/// </summary>
internal sealed class CurrentCustomerModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        bindingContext.Result = ModelBindingResult.Success(CurrentCustomer.FromPrincipal(bindingContext.HttpContext.User));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Uses <see cref="CurrentCustomerModelBinder"/> with a <see cref="BindingSource.Special"/> source: the value does
/// not come from the request, so OpenAPI does not list it as a parameter and <c>[ApiController]</c> does not infer
/// it as the body.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter, AllowMultiple = false)]
internal sealed class FromAccessTokenAttribute : ModelBinderAttribute
{
    public FromAccessTokenAttribute()
        : base(typeof(CurrentCustomerModelBinder)) =>
        BindingSource = BindingSource.Special;
}
