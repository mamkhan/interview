using BuildingBlocks.Seedwork;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Extensions.Hosting;

/// <summary>Maps the two deterministic failures every command slice can raise to problem details: a
/// command that fails validation is a 400 with the failures by property; an invariant the domain refuses
/// is a 422. Anything else is left to the default handler and answers 500 without detail.</summary>
public sealed class PlatformExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case CommandValidationException validation:
                await Results.ValidationProblem(
                        validation.Failures.GroupBy(f => f.Property).ToDictionary(g => g.Key, g => g.Select(f => f.Message).ToArray()),
                        detail: $"{validation.CommandName} failed validation.",
                        extensions: Trace(httpContext))
                    .ExecuteAsync(httpContext);
                return true;
            case DomainException domain:
                await Results.Problem(
                        title: "The request breaks a domain rule.",
                        detail: domain.Message,
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        extensions: Trace(httpContext))
                    .ExecuteAsync(httpContext);
                return true;
            default:
                return false;
        }
    }

    private static Dictionary<string, object?> Trace(HttpContext context) =>
        new() { ["traceIdentifier"] = context.TraceIdentifier };
}
