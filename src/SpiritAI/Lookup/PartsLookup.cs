using System.Globalization;
using System.Text.Json;

namespace SpiritAI.Lookup;

/// <summary>
/// Finds the parts of one machine, from whatever the person happened to say.
/// </summary>
/// <param name="invoke">The seam that calls one DAB tool.</param>
public sealed class PartsLookup(ToolInvoker invoke, ModelIndex models)
{
    /// <summary>The tool ids <c>spirit.yaml</c> aliases these DAB tools under.</summary>
    private const string FindModel = "find_model";

    private const string SearchParts = "search_parts";

    /// <summary>How many rows one answer carries, however many matched.</summary>
    private const int Cap = 20;

    private readonly ToolInvoker _invoke = invoke;
    private readonly ModelIndex _models = models;

    /// <summary>Finds the parts of one machine.</summary>
    /// <param name="productName">A product name such as <c>F63</c>, or nothing.</param>
    /// <param name="year">The year the machine was built, or nothing.</param>
    /// <param name="serialNo">A sixteen digit serial number, or nothing.</param>
    /// <param name="modelNo">An exact six digit model number, or nothing.</param>
    /// <param name="search">One word that narrows the list, such as <c>motor</c>, or nothing.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The parts, or the years still needed to find them.</returns>
    public async Task<PartsAnswer> FindAsync(
        string? productName,
        int? year,
        string? serialNo,
        string? modelNo,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(productName, year, serialNo, modelNo, cancellationToken)
            .ConfigureAwait(false);

        return resolved.Model is not { } model
            ? resolved.Answer!
            : await PartsOfAsync(model, search, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Works out which single model number the question is about.</summary>
    private async Task<(ModelYear? Model, PartsAnswer? Answer)> ResolveAsync(
        string? productName,
        int? year,
        string? serialNo,
        string? modelNo,
        CancellationToken cancellationToken)
    {
        // A serial number already carries its model number in its first six digits, so it settles
        // the question outright and costs no call.
        if (serialNo is { Length: > 0 } serial && SerialNumber.Parse(serial).ModelNo is { } fromSerial)
        {
            modelNo = fromSerial;
        }

        if (modelNo is { Length: > 0 } exact)
        {
            var named = await ModelsAsync(null, exact, cancellationToken).ConfigureAwait(false);

            return (named.FirstOrDefault() ?? ModelYear.Read(exact, string.Empty), null);
        }

        if (productName is not { Length: > 0 } name)
        {
            return (null, Nothing(
                "unknown_product",
                "No product name, model number or serial number was given."));
        }

        var models = await ModelsAsync(name, null, cancellationToken).ConfigureAwait(false);

        if (models.Count == 0)
        {
            return (null, Nothing("unknown_product", $"No model matches the name '{name}'."));
        }

        if (models.Count == 1)
        {
            return (models[0], null);
        }

        if (year is { } asked && ModelYear.Pick(models, asked) is { } picked)
        {
            return (picked, null);
        }

        // The database names a year for some rows and not others: of the six the LCR covers, one.
        // So when its own names cannot answer, the manuals do — and their answer is checked back
        // against these rows before it is used.
        var documented = await _models.FindAsync(name, year, cancellationToken).ConfigureAwait(false);

        if (documented is { Outcome: "model", ModelNo: { Length: > 0 } resolved }
            && models.FirstOrDefault(model => model.ModelNo == resolved) is { } confirmed)
        {
            return (confirmed, null);
        }

        var offered = documented.Years.Count > 0 ? documented.Years : ModelYear.Years(models);

        return (null, new PartsAnswer("needs_year", null, null, [], 0, offered, documented.Note));
    }

    /// <summary>Reads one model's parts.</summary>
    private async Task<PartsAnswer> PartsOfAsync(
        ModelYear model,
        string? search,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> arguments = new(StringComparer.Ordinal)
        {
            ["ModelNo"] = model.ModelNo,
            ["Top"] = Cap,
        };

        // @Search is matched as one piece of text, so a phrase matches nothing. One word only.
        if (FirstWord(search) is { } word)
        {
            arguments["Search"] = word;
        }

        var rows = await ReadAsync(SearchParts, arguments, cancellationToken).ConfigureAwait(false)
            ?? [];

        if (rows.Count == 0)
        {
            return new PartsAnswer(
                "no_parts",
                model.ModelNo,
                model.Name,
                [],
                0,
                [],
                search is { Length: > 0 }
                    ? $"Model {model.ModelNo} lists no part matching '{search}'."
                    : $"Model {model.ModelNo} lists no parts at all.");
        }

        var parts = rows.Select(LineOf).ToArray();
        var total = Number(rows[0], "TotalRows") ?? parts.Length;

        return new PartsAnswer(
            "parts",
            model.ModelNo,
            model.Name,
            parts,
            total,
            [],
            $"{parts.Length} of {total} rows for model {model.ModelNo}.");
    }

    /// <summary>Reads every model one name covers, or the one an exact number names.</summary>
    private async Task<IReadOnlyList<ModelYear>> ModelsAsync(
        string? name,
        string? modelNo,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> arguments = new(StringComparer.Ordinal) { ["Top"] = 100 };

        if (name is { Length: > 0 })
        {
            arguments["Name"] = name;
        }
        else
        {
            arguments["ModelNo"] = modelNo;
        }

        var rows = await ReadAsync(FindModel, arguments, cancellationToken).ConfigureAwait(false);

        return rows is null
            ? []
            : [.. rows.Select(row => ModelYear.Read(
                Text(row, "ModelNo") ?? string.Empty,
                Text(row, "ModelName"),
                Text(row, "ModelDesc")))];
    }

    /// <summary>Calls one tool and reads its rows, or nothing when it refused.</summary>
    private async Task<IReadOnlyList<JsonElement>?> ReadAsync(
        string toolId,
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return DabEnvelope.RowsOf(
                await _invoke(toolId, arguments, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A tool that throws is an empty answer, never a failed turn. The agent is told what
            // is missing and the person still hears something useful.
            return null;
        }
    }

    /// <summary>Takes the first word of a search term, because @Search matches no phrase.</summary>
    private static string? FirstWord(string? search)
        => search?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

    private static PartsAnswer Nothing(string outcome, string note)
        => new(outcome, null, null, [], 0, [], note);

    private static PartLine LineOf(JsonElement row)
        => new(
            Text(row, "SpNo") ?? string.Empty,
            Text(row, "Description") ?? string.Empty,
            Number(row, "Qty") ?? 0);

    private static string? Text(JsonElement row, string name)
        => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? Number(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null,
        };
    }
}
