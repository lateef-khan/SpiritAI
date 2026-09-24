namespace SpiritAI.Chatwoot;

/// <summary>A <c>Table</c> or a chart as a Markdown table. A chart shows staff its numbers.</summary>
internal static class OpenUiData
{
    private const string LabelHeader = "Label";

    private static readonly Dictionary<string, string> ChartTitles = new(StringComparer.Ordinal)
    {
        ["BarChart"] = "Bar chart",
        ["HorizontalBarChart"] = "Bar chart",
        ["LineChart"] = "Line chart",
        ["AreaChart"] = "Area chart",
        ["RadarChart"] = "Radar chart",
        ["PieChart"] = "Pie chart",
        ["RadialChart"] = "Radial chart",
        ["SingleStackedBarChart"] = "Stacked bar chart",
        ["ScatterChart"] = "Scatter chart",
    };

    public static bool IsChart(string component) => ChartTitles.ContainsKey(component);

    /// <summary><c>Table([Col(label, data), ...])</c>: each column holds its own cells.</summary>
    public static string? Table(OpenUiProgram program, OpenUiValue.Call table)
    {
        var columns = program.Calls(table, "columns").ToList();

        if (columns.Count == 0)
        {
            return null;
        }

        var headers = columns.Select(col => program.Text(col, "label") ?? "").ToList();
        var cells = columns.Select(col => program.Items(col, "data").Select(program.Line).ToList()).ToList();
        var rows = Enumerable.Range(0, cells.Max(column => column.Count))
            .Select(i => (IReadOnlyList<string>)cells.Select(column => i < column.Count ? column[i] : "").ToList());

        return MarkdownText.Table(headers, rows);
    }

    public static string? Chart(OpenUiProgram program, OpenUiValue.Call chart)
    {
        var table = chart.Component switch
        {
            "PieChart" or "RadialChart" or "SingleStackedBarChart" => Values(program, chart),
            "ScatterChart" => Points(program, chart),
            _ => Series(program, chart),
        };

        return table is null ? null : $"_{ChartTitles[chart.Component]}_\n\n{table}";
    }

    /// <summary><c>labels</c> down the side, one column per <c>Series(category, values)</c>.</summary>
    private static string? Series(OpenUiProgram program, OpenUiValue.Call chart)
    {
        var labels = program.Items(chart, "labels").Select(program.Line).ToList();
        var series = program.Calls(chart, "series").ToList();

        if (labels.Count == 0)
        {
            return null;
        }

        var headers = new List<string> { program.Text(chart, "xLabel") ?? LabelHeader };
        headers.AddRange(series.Select(s => program.Text(s, "category") ?? ""));

        var values = series.Select(s => program.Items(s, "values").Select(program.Line).ToList()).ToList();
        var rows = labels.Select((label, i) =>
            (IReadOnlyList<string>)[label, .. values.Select(column => i < column.Count ? column[i] : "")]);

        return MarkdownText.Table(headers, rows);
    }

    /// <summary><c>labels</c> beside <c>values</c>.</summary>
    private static string? Values(OpenUiProgram program, OpenUiValue.Call chart)
    {
        var labels = program.Items(chart, "labels").Select(program.Line).ToList();
        var values = program.Items(chart, "values").Select(program.Line).ToList();

        if (labels.Count == 0)
        {
            return null;
        }

        var rows = labels.Select((label, i) => (IReadOnlyList<string>)[label, i < values.Count ? values[i] : ""]);

        return MarkdownText.Table([LabelHeader, "Value"], rows);
    }

    /// <summary>One row per <c>Point(x, y, z)</c>, under its <c>ScatterSeries</c> name.</summary>
    private static string? Points(OpenUiProgram program, OpenUiValue.Call chart)
    {
        var rows = program.Calls(chart, "datasets")
            .SelectMany(set => program.Calls(set, "points").Select(point => (IReadOnlyList<string>)
            [
                program.Text(set, "name") ?? "",
                program.Text(point, "x") ?? "",
                program.Text(point, "y") ?? "",
                program.Text(point, "z") ?? "",
            ]))
            .ToList();

        if (rows.Count == 0)
        {
            return null;
        }

        List<string> headers = ["Series", program.Text(chart, "xLabel") ?? "x", program.Text(chart, "yLabel") ?? "y"];

        if (rows.Any(row => row[3].Length > 0))
        {
            headers.Add("z");
        }

        return MarkdownText.Table(headers, rows);
    }
}
