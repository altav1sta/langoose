using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;

namespace Langoose.Api.Middleware;

public sealed class AntiforgeryValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (!HttpMethods.IsPost(context.Request.Method) &&
            !HttpMethods.IsPut(context.Request.Method) &&
            !HttpMethods.IsPatch(context.Request.Method) &&
            !HttpMethods.IsDelete(context.Request.Method))
        {
            await next(context);
            return;
        }

        var requiresAuthorization = context.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;

        if (requiresAuthorization && !isAuthenticated)
        {
            await next(context);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
            await next(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
