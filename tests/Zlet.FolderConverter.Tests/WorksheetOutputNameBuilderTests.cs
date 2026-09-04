using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class WorksheetOutputNameBuilderTests
{
    [Fact]
    public void Build_creates_deterministic_names_and_resolves_collisions()
    {
        var result = WorksheetOutputNameBuilder.Build(
            "sales",
            ["Summary", "Sales:2026", "Sales?2026", "CON", "Trailing. "],
            ".csv");

        Assert.Equal("sales__Summary.csv", result[0]);
        Assert.Equal("sales__Sales_2026.csv", result[1]);
        Assert.Equal("sales__Sales_2026-2.csv", result[2]);
        Assert.Equal("sales___CON.csv", result[3]);
        Assert.Equal("sales__Trailing.csv", result[4]);
    }

    [Fact]
    public void Build_is_case_insensitive_when_resolving_collisions()
    {
        var result = WorksheetOutputNameBuilder.Build(
            "book",
            ["Data", "data", "DATA"],
            "tsv");

        Assert.Equal(
            ["book__Data.tsv", "book__data-2.tsv", "book__DATA-3.tsv"],
            result);
    }
}
