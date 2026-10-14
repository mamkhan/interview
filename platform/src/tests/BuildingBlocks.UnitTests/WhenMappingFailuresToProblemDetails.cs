using System.Text.Json;
using AwesomeAssertions;
using BuildingBlocks.Seedwork;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.UnitTests;

// The two deterministic failures a slice can raise, mapped once for every host by ServiceDefaults.
public class WhenMappingFailuresToProblemDetails
{
    [Fact]
    public async Task MapsAValidationFailureToA400WithTheFailuresByProperty()
    {
        var context = NewContext();
        var exception = new CommandValidationException("ReplayCapture",
            [new ValidationFailure("Market", "Market must be roi-esb."), new ValidationFailure("Content", "The request body cannot be empty.")]);

        var handled = await new PlatformExceptionHandler().TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var body = await BodyAsync(context);
        body.GetProperty("errors").GetProperty("Market")[0].GetString().Should().Be("Market must be roi-esb.");
        body.GetProperty("errors").GetProperty("Content")[0].GetString().Should().Be("The request body cannot be empty.");
        body.GetProperty("traceIdentifier").GetString().Should().Be("trace-1");
    }

    [Fact]
    public async Task MapsADomainRuleToA422WithTheRuleAsDetail()
    {
        var context = NewContext();

        var handled = await new PlatformExceptionHandler().TryHandleAsync(context,
            new RuleBroken("The file declares no MessageTypeCode."), TestContext.Current.CancellationToken);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        (await BodyAsync(context)).GetProperty("detail").GetString().Should().Be("The file declares no MessageTypeCode.");
    }

    [Fact]
    public async Task LeavesAnythingElseToTheDefaultHandler()
    {
        var handled = await new PlatformExceptionHandler().TryHandleAsync(NewContext(),
            new InvalidOperationException("a bug"), TestContext.Current.CancellationToken);

        handled.Should().BeFalse();
    }

    private sealed class RuleBroken(string message) : DomainException(message);

    private static DefaultHttpContext NewContext() => new()
    {
        TraceIdentifier = "trace-1",
        RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        Response = { Body = new MemoryStream() },
    };

    private static async Task<JsonElement> BodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return (await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken)).RootElement;
    }
}
