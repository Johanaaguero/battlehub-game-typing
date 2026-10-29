
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using TypingBattle.Api.Hubs;
using TypingBattle.Api.Results;

namespace TypingBattle.UnitTests.Hubs;

public sealed class TypingMatchServiceMoqTests
{
    [Fact]
    public async Task EndMatchAsync_CallsSaveAsyncOnce()
    {
        var store = new TypingMatchStore();
        var results = new Mock<IResultsService>();

        results
            .Setup(x => x.SaveAsync(
                It.IsAny<SaveResultRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaveResultOutcome.AlreadyExists("match-1"));

        var service = new TypingMatchService(
            store, results.Object, TimeProvider.System);

        service.TryJoin("match-1", "user-1", "Ana");

        var outcome = await service.EndMatchAsync(
            new EndMatchRequest("match-1", "user-1", null, null, null, null, null));

        Assert.IsType<SaveResultOutcome.AlreadyExists>(outcome);

        results.Verify(x => x.SaveAsync(
            It.Is<SaveResultRequest>(r => r.MatchId == "match-1"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EndMatchAsync_ReturnsOutcomeFromResultsService()
    {
        var store = new TypingMatchStore();
        var results = new Mock<IResultsService>();

        var expected = new SaveResultOutcome.Invalid(
            new Dictionary<string, string[]>
            {
                ["players"] = new[] { "Invalid players" }
            });

        results
            .Setup(x => x.SaveAsync(
                It.IsAny<SaveResultRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var service = new TypingMatchService(
            store, results.Object, TimeProvider.System);

        service.TryJoin("match-2", "user-1", "Ana");

        var actual = await service.EndMatchAsync(
            new EndMatchRequest("match-2", "user-1", null, null, null, null, null));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task EndMatchAsync_NotifiesWhenNotifierIsConfigured()
    {
        var store = new TypingMatchStore();
        var results = new Mock<IResultsService>();
        var notifier = new Mock<IMatchFinishedNotifier>();

        results
            .Setup(x => x.SaveAsync(
                It.IsAny<SaveResultRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaveResultOutcome.AlreadyExists("match-3"));

        notifier
            .Setup(x => x.NotifyMatchFinishedAsync(
                It.IsAny<string>(),
                It.IsAny<SaveResultOutcome>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new TypingMatchService(
            store, results.Object, TimeProvider.System, notifier.Object);

        service.TryJoin("match-3", "user-1", "Ana");

        await service.EndMatchAsync(
            new EndMatchRequest("match-3", "user-1", null, null, null, null, null));

        notifier.Verify(x => x.NotifyMatchFinishedAsync(
            "match-3",
            It.IsAny<SaveResultOutcome>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EndMatchAsync_ReturnsOutcomeEvenIfNotifierThrows()
    {
        var store = new TypingMatchStore();
        var results = new Mock<IResultsService>();
        var notifier = new Mock<IMatchFinishedNotifier>();

        var expected = new SaveResultOutcome.AlreadyExists("match-4");

        results
            .Setup(x => x.SaveAsync(
                It.IsAny<SaveResultRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        notifier
            .Setup(x => x.NotifyMatchFinishedAsync(
                It.IsAny<string>(),
                It.IsAny<SaveResultOutcome>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification failed"));

        var service = new TypingMatchService(
            store, results.Object, TimeProvider.System, notifier.Object);

        service.TryJoin("match-4", "user-1", "Ana");

        var actual = await service.EndMatchAsync(
            new EndMatchRequest("match-4", "user-1", null, null, null, null, null));

        Assert.Same(expected, actual);
    }
}
