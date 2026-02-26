using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Nuke.Common.IO;
using UnifyBuild.Nuke.Performance;
using UnifyBuild.Nuke.Tests.Fixtures;
using Xunit;

namespace UnifyBuild.Nuke.Tests.Unit;

public class DistributedBuildCacheTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TryGetCached_LocalHit_ReturnsTrue()
    {
        var cacheDir = _temp.CreateDirectory("cache");
        var outputDir = _temp.CreateDirectory("output");
        var localCache = new BuildCache((AbsolutePath)cacheDir);

        // Store something in local cache
        var sourceDir = _temp.CreateDirectory("source");
        File.WriteAllText(Path.Combine(sourceDir, "file.txt"), "hello");
        localCache.Store("test-key", (AbsolutePath)sourceDir);

        var distributed = new DistributedBuildCache(
            localCache,
            "https://cache.example.com",
            new HttpClient(new FakeHandler(HttpStatusCode.NotFound)));

        var result = distributed.TryGetCached("test-key", (AbsolutePath)outputDir);

        result.Should().BeTrue();
        distributed.CacheHits.Should().Be(1);
        distributed.CacheMisses.Should().Be(0);
    }

    [Fact]
    public void TryGetCached_NotFoundAnywhere_ReturnsFalse()
    {
        var cacheDir = _temp.CreateDirectory("cache");
        var outputDir = _temp.CreateDirectory("output");
        var localCache = new BuildCache((AbsolutePath)cacheDir);

        var distributed = new DistributedBuildCache(
            localCache,
            "https://cache.example.com",
            new HttpClient(new FakeHandler(HttpStatusCode.NotFound)));

        var result = distributed.TryGetCached("missing-key", (AbsolutePath)outputDir);

        result.Should().BeFalse();
        distributed.CacheMisses.Should().Be(1);
    }

    [Fact]
    public void TryGetCached_NetworkFailure_FallsBackGracefully()
    {
        var cacheDir = _temp.CreateDirectory("cache");
        var outputDir = _temp.CreateDirectory("output");
        var localCache = new BuildCache((AbsolutePath)cacheDir);

        var distributed = new DistributedBuildCache(
            localCache,
            "https://cache.example.com",
            new HttpClient(new FakeHandler(throwOnSend: true)),
            maxRetries: 1);

        var result = distributed.TryGetCached("some-key", (AbsolutePath)outputDir);

        result.Should().BeFalse();
        distributed.CacheMisses.Should().Be(1);
    }

    [Fact]
    public void Store_UploadFailure_StillStoresLocally()
    {
        var cacheDir = _temp.CreateDirectory("cache");
        var sourceDir = _temp.CreateDirectory("source");
        File.WriteAllText(Path.Combine(sourceDir, "output.dll"), "binary-content");

        var localCache = new BuildCache((AbsolutePath)cacheDir);
        var distributed = new DistributedBuildCache(
            localCache,
            "https://cache.example.com",
            new HttpClient(new FakeHandler(throwOnSend: true)),
            maxRetries: 1);

        distributed.Store("store-key", (AbsolutePath)sourceDir);

        // Local cache should still have the entry
        var verifyDir = _temp.CreateDirectory("verify");
        localCache.TryGetCached("store-key", (AbsolutePath)verifyDir).Should().BeTrue();
    }

    [Fact]
    public void CacheKeyUrl_ConstructedCorrectly()
    {
        var cacheDir = _temp.CreateDirectory("cache");
        var localCache = new BuildCache((AbsolutePath)cacheDir);

        var requestedUrls = new System.Collections.Generic.List<string>();
        var handler = new UrlCapturingHandler(requestedUrls, HttpStatusCode.NotFound);

        var distributed = new DistributedBuildCache(
            localCache,
            "https://cache.example.com/builds",
            new HttpClient(handler));

        var outputDir = _temp.CreateDirectory("output");
        distributed.TryGetCached("abc123", (AbsolutePath)outputDir);

        requestedUrls.Should().Contain("https://cache.example.com/builds/abc123");
    }

    [Fact]
    public void RetryLogic_RetriesOnTransientFailure()
    {
        var cacheDir = _temp.CreateDirectory("cache");
        var localCache = new BuildCache((AbsolutePath)cacheDir);

        int callCount = 0;
        var handler = new CountingHandler(() =>
        {
            callCount++;
            if (callCount < 3)
                throw new HttpRequestException("Transient failure");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var distributed = new DistributedBuildCache(
            localCache,
            "https://cache.example.com",
            new HttpClient(handler),
            maxRetries: 3);

        var outputDir = _temp.CreateDirectory("output");
        distributed.TryGetCached("retry-key", (AbsolutePath)outputDir);

        callCount.Should().Be(3, "should retry up to maxRetries times");
    }

    [Fact]
    public void GetStatsSummary_ReturnsFormattedString()
    {
        var cacheDir = _temp.CreateDirectory("cache");
        var localCache = new BuildCache((AbsolutePath)cacheDir);

        var distributed = new DistributedBuildCache(
            localCache,
            "https://cache.example.com",
            new HttpClient(new FakeHandler(HttpStatusCode.NotFound)));

        var outputDir = _temp.CreateDirectory("output");
        distributed.TryGetCached("miss1", (AbsolutePath)outputDir);
        distributed.TryGetCached("miss2", (AbsolutePath)outputDir);

        var summary = distributed.GetStatsSummary();

        summary.Should().Contain("0 hits");
        summary.Should().Contain("2 misses");
        summary.Should().Contain("0.0% hit rate");
    }

    /// <summary>
    /// Simple fake HTTP handler that returns a fixed status code or throws.
    /// </summary>
    private class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly bool _throwOnSend;

        public FakeHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _statusCode = statusCode;
        }

        public FakeHandler(bool throwOnSend)
        {
            _throwOnSend = throwOnSend;
            _statusCode = HttpStatusCode.InternalServerError;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_throwOnSend)
                throw new HttpRequestException("Network error");
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }

    /// <summary>
    /// Handler that captures requested URLs.
    /// </summary>
    private class UrlCapturingHandler : HttpMessageHandler
    {
        private readonly System.Collections.Generic.List<string> _urls;
        private readonly HttpStatusCode _statusCode;

        public UrlCapturingHandler(System.Collections.Generic.List<string> urls, HttpStatusCode statusCode)
        {
            _urls = urls;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }

    /// <summary>
    /// Handler that calls a factory function, allowing control over behavior per call.
    /// </summary>
    private class CountingHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _factory;

        public CountingHandler(Func<HttpResponseMessage> factory)
        {
            _factory = factory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_factory());
        }
    }
}
