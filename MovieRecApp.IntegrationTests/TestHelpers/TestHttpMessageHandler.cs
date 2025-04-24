namespace MovieRecApp.IntegrationTests.TestHelpers;

public class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

    public TestHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return _responder(request);
    }
}