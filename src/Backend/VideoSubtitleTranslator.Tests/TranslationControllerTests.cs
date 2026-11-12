using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VideoSubtitleTranslator.Api.Controllers;
using VideoSubtitleTranslator.Core.Interfaces;
using VideoSubtitleTranslator.Core.Models;

namespace VideoSubtitleTranslator.Tests;

public sealed class TranslationControllerTests
{
    [Fact]
    public async Task TranslatesTextInOrderAndReportsProviderFallbacks()
    {
        var service = new FakeTranslationService((segment, _) =>
        {
            segment.TranslatedText = segment.Text == "unavailable" ? segment.Text : $"fr:{segment.Text}";
            segment.TranslationFailed = segment.Text == "unavailable";
            return segment;
        });
        var controller = CreateController(service);

        var action = await controller.TranslateText(
            new TranslationController.TextTranslationRequest("fr", ["hello", "unavailable"]),
            CancellationToken.None);

        var response = Assert.IsType<TranslationController.TextTranslationResponse>(Assert.IsType<OkObjectResult>(action.Result).Value);
        Assert.Equal("fr", response.TargetLanguage);
        Assert.Equal(new[] { new TranslationController.TextTranslationItem("fr:hello", true), new TranslationController.TextTranslationItem("unavailable", false) }, response.Results);
        Assert.Equal(new[] { "hello", "unavailable" }, service.ReceivedTexts);
    }

    [Fact]
    public async Task RejectsRequestsFromNonLoopbackClients()
    {
        var controller = CreateController(new FakeTranslationService((segment, _) => segment), IPAddress.Parse("192.0.2.10"));

        var action = await controller.TranslateText(
            new TranslationController.TextTranslationRequest("fr", ["hello"]),
            CancellationToken.None);

        Assert.IsType<ForbidResult>(action.Result);
    }

    [Theory]
    [InlineData("invalid language", new[] { "hello" })]
    [InlineData("fr", new string[0])]
    public async Task RejectsInvalidLanguageOrEmptyTextList(string language, string[] texts)
    {
        var service = new FakeTranslationService((segment, _) => segment);
        var controller = CreateController(service);

        var action = await controller.TranslateText(
            new TranslationController.TextTranslationRequest(language, texts),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Empty(service.ReceivedTexts);
    }

    private static TranslationController CreateController(ITranslationService service, IPAddress? remoteAddress = null)
    {
        var controller = new TranslationController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.Connection.RemoteIpAddress = remoteAddress ?? IPAddress.Loopback;
        return controller;
    }

    private sealed class FakeTranslationService(Func<Segment, string, Segment> translate) : ITranslationService
    {
        public List<string> ReceivedTexts { get; } = [];

        public Task<IReadOnlyList<Segment>> TranslateAsync(
            IReadOnlyList<Segment> segments,
            string targetLanguage,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var result = segments.Select(segment =>
            {
                ReceivedTexts.Add(segment.Text);
                return translate(segment, targetLanguage);
            }).ToArray();
            return Task.FromResult<IReadOnlyList<Segment>>(result);
        }
    }
}