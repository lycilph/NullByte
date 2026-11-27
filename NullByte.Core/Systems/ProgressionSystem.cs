using NullByte.Core.Domain;
using NullByte.Core.Domain.Progression;
using NullByte.Core.Formulas;
using NullByte.Core.Services;
using NullByte.Core.Simulation;

namespace NullByte.Core.Systems;

/// <summary>
/// Moves the story forward: when the player meets the conditions of the next chapter, the chapter is
/// completed, its story is told and the features it grants are unlocked.
/// </summary>
/// <remarks>
/// Runs on the game thread, driven by <see cref="GameLoop"/>. Conditions are read from the game state, so
/// it does not matter how the player got there. Only the next chapter is ever checked, which keeps the
/// story in order. A game that is not a journey (<see cref="ProgressionState.IsUnrestricted"/>) is left alone.
/// </remarks>
public sealed class ProgressionSystem : ISimulationSystem
{
    private readonly GameState gameState;
    private readonly Journey journey;
    private readonly SkillFormulas skills;
    private readonly IGameOutput output;

    public ProgressionSystem(GameState gameState, Journey journey, SkillFormulas skills, IGameOutput output)
    {
        this.gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        this.journey = journey ?? throw new ArgumentNullException(nameof(journey));
        this.skills = skills ?? throw new ArgumentNullException(nameof(skills));
        this.output = output ?? throw new ArgumentNullException(nameof(output));

        RebuildUnlockedFeatures();
    }

    /// <summary>Raised on the game thread after a chapter has been completed and its story told.</summary>
    public event Action<Chapter>? ChapterCompleted;

    /// <inheritdoc />
    public void Update(TimeSpan delta)
    {
        var progression = gameState.Progression;

        if (progression.IsUnrestricted)
            return;

        // More than one chapter can complete in one update, for example when a loaded game is far ahead of its saved story.
        while (journey.NextChapter(progression) is { } chapter && AreMet(chapter))
        {
            progression.Complete(chapter);
            Tell(chapter);
            ChapterCompleted?.Invoke(chapter);
        }
    }

    /// <summary>
    /// Saves hold the completed chapter ids; what they unlock is read from the journey again, so
    /// a chapter that gains a feature in a later version also grants it to games that already finished it.
    /// </summary>
    private void RebuildUnlockedFeatures()
    {
        var progression = gameState.Progression;

        foreach (var id in progression.CompletedChapterIds)
        {
            if (journey.TryGet(id, out var chapter) && chapter is not null)
                progression.Grant(chapter);
        }
    }

    private bool AreMet(Chapter chapter) => chapter.Conditions.All(IsMet);

    private bool IsMet(ChapterCondition condition)
    {
        var player = gameState.Player;
        // Cloud servers are bought, not won, and home is rooted from the start, so neither counts as an achievement.
        var servers = gameState.Network.Servers.Where(server => !server.IsCloudServer && server.Id != player.HomeServerId);

        return condition.Kind switch
        {
            ConditionKind.MoneyAtLeast => player.Money >= condition.Amount,
            ConditionKind.HackingLevelAtLeast => skills.CalculateLevel(SkillKind.Hacking, player.Skills.GetExperience(SkillKind.Hacking)) >= condition.Amount,
            ConditionKind.ServersDiscoveredAtLeast => player.DiscoveredServerIds.Count >= condition.Amount,
            ConditionKind.ServersRootedAtLeast => servers.Count(server => server.HasRootAccess) >= condition.Amount,
            ConditionKind.ServersBackdooredAtLeast => servers.Count(server => server.HasBackdoor) >= condition.Amount,
            ConditionKind.BotnetNodesAtLeast => gameState.Botnet.NodeCount >= condition.Amount,
            ConditionKind.AtServer => IsAt(condition.Hostname),
            ConditionKind.RootOnServer => HasRoot(condition.Hostname),
            _ => false
        };
    }

    private bool IsAt(string? hostname) =>
        hostname is not null
        && gameState.Network.TryGetServer(gameState.Player.CurrentServerId, out var current)
        && current.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase);

    private bool HasRoot(string? hostname) =>
        hostname is not null
        && gameState.Network.TryGetServer(hostname, out var server)
        && server.HasRootAccess;

    private void Tell(Chapter chapter)
    {
        output.WriteLine();
        output.WriteLine($"-- {chapter.Title} --");

        foreach (var line in chapter.Story)
            output.WriteLine(line);

        output.WriteLine();
    }
}
