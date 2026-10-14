using JobForge.Runner.Domain;
using Xunit;

namespace JobForge.Runner.Tests;

public class EnvelopeTests
{
    [Fact]
    public void TryParse_reads_pascal_case_fields_in_any_order()
    {
        var json = """{"ExecutionId":"11111111-1111-1111-1111-111111111111","JobId":"22222222-2222-2222-2222-222222222222","WebhookUrl":"http://example.local/h","Payload":"{\"k\":1}","Method":"POST","TimeoutSeconds":30,"MaxRetries":5,"Attempt":0,"TraceId":"abc"}""";
        var e = DispatchEnvelope.TryParse(json);
        Assert.NotNull(e);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), e!.ExecutionId);
        Assert.Equal("POST", e.Method);
        Assert.Equal(5, e.MaxRetries);
    }

    [Fact]
    public void TryParse_rejects_garbage() => Assert.Null(DispatchEnvelope.TryParse("not json"));

    [Fact]
    public void TryParse_rejects_missing_execution_id()
        => Assert.Null(DispatchEnvelope.TryParse("""{"JobId":"22222222-2222-2222-2222-222222222222"}"""));
}
