// Provides deterministic simulator data for local runs and tests.
using DataRetriever.Application.Step1Load.Models;
using DataRetriever.Application.Step2Load.Models;
using DataRetriever.Application.Step3Load.Models;

namespace DataRetriever.Simulators;

public sealed class SimulatorSeedData
{
    public IReadOnlyList<Step1Dto> ConfiguredRows { get; } =
    [
        new("INT-001", "EXT1-AAA", "GBP", "2"),
        new("INT-002", "EXT1-BBB", "EUR", "1"),
        new("INT-003", "EXT1-CCC", "GBP", "2"),
        new("INT-004", null, "GBP", "1"),
        new("INT-005", "EXT1-DDD", null, "1"),
        new("INT-006", "EXT1-EEE", "USD", "1"),
        new("INT-007", "EXT1-FFF", "GBP", "1"),
        new("INT-008", "EXT1-GGG", "GBP", "1")
    ];

    public IReadOnlyDictionary<string, IReadOnlyList<Step2ResponseDto>> Step2Rows { get; } =
        new Dictionary<string, IReadOnlyList<Step2ResponseDto>>(StringComparer.OrdinalIgnoreCase)
        {
            ["EXT1-AAA"] =
            [
                new("EXT2-AAA-1", new DateOnly(2026, 01, 01)),
                new("EXT2-AAA-2", new DateOnly(2026, 02, 01)),
                new("EXT2-AAA-3", new DateOnly(2026, 03, 01))
            ],
            ["EXT1-BBB"] = [],
            ["EXT1-CCC"] =
            [
                new("EXT2-CCC-1", new DateOnly(2026, 01, 15))
            ],
            ["EXT1-EEE"] =
            [
                new("EXT2-EEE-1", new DateOnly(2026, 01, 20))
            ],
            ["EXT1-GGG"] =
            [
                new("EXT2-GGG-1", new DateOnly(2026, 04, 01))
            ]
        };

    public IReadOnlyDictionary<string, Step3ResponseItemDto> Step3Rows { get; } =
        new Dictionary<string, Step3ResponseItemDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["EXT2-AAA-3"] = new("ext2-aaa-3", "100.10", "200.20", "300.30"),
            ["EXT2-AAA-2"] = new("EXT2-AAA-2", "101.10", "N/A", "301.30"),
            ["EXT2-EEE-1"] = new("EXT2-EEE-1", "102.10", null, "302.30"),
            ["EXT2-GGG-1"] = new("EXT2-GGG-1", "103.10", "203.20", "303.30")
        };
}
