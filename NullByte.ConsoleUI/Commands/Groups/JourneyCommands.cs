using NullByte.Core.Domain.Progression;
using NullByte.Core.Services;

namespace NullByte.ConsoleUI.Commands.Groups;

/// <summary>
/// Lets the player look back at the story so far and see what to do next.
/// </summary>
public sealed class JourneyCommands(ProgressionState progression, Journey journey, IGameOutput output) : ICommandGroup
{
    public void Register(CommandRegistry registry)
    {
        registry.Register(
            new CommandDefinition(
                "journey",
                "journey [chapter]",
                "Show your journey so far and what to do next. Give a chapter number to read it again.",
                JourneyAsync)
            { MaxArgs = 1 });
    }

    private async Task JourneyAsync(IReadOnlyList<string> arguments)
    {
        if (progression.IsUnrestricted)
        {
            output.WriteLine("There is no story in this game: everything is unlocked.");
            return;
        }

        if (arguments.Count == 1)
        {
            ReadChapter(arguments[0]);
            return;
        }

        ShowJourney();
    }

    private void ShowJourney()
    {
        var completed = journey.Chapters.Where(chapter => progression.HasCompleted(chapter.Id)).ToArray();

        output.WriteLine($"Journey: {completed.Length} of {journey.Chapters.Count} chapters");

        for (var i = 0; i < journey.Chapters.Count; i++)
        {
            var chapter = journey.Chapters[i];

            if (progression.HasCompleted(chapter.Id))
                output.WriteLine($"  {i + 1,2}. {chapter.Title}");
        }

        if (journey.NextChapter(progression) is { } next)
        {
            output.WriteLine();
            output.WriteLine(string.IsNullOrWhiteSpace(next.Hint) ? "Keep going." : next.Hint);
        }
        else
        {
            output.WriteLine();
            output.WriteLine("Your story is told, for now.");
        }
    }

    private void ReadChapter(string argument)
    {
        if (!int.TryParse(argument, out var number) || number < 1 || number > journey.Chapters.Count)
        {
            output.WriteLine($"There is no chapter '{argument}'. Chapters are numbered 1 to {journey.Chapters.Count}.");
            return;
        }

        var chapter = journey.Chapters[number - 1];

        // A chapter that has not happened yet stays a secret.
        if (!progression.HasCompleted(chapter.Id))
        {
            output.WriteLine($"You have not reached chapter {number} yet.");
            return;
        }

        output.WriteLine($"-- {chapter.Title} --");

        foreach (var line in chapter.Story)
            output.WriteLine(line);
    }
}
