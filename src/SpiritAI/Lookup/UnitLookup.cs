using System.Globalization;
using System.Text.Json;

namespace SpiritAI.Lookup;

/// <summary>
/// Calls one DAB tool.
/// </summary>
/// <remarks>
/// A delegate rather than <c>ToolRegistry</c> itself. The registry has an internal constructor and
/// cannot be built in a test, so this is the seam that lets the shaping below be tested without a
/// live database behind Tailscale.
/// </remarks>
/// <param name="toolId">The tool's id, as <c>spirit.yaml</c> aliases it.</param>
/// <param name="arguments">The tool's arguments, by name.</param>
/// <param name="cancellationToken">Cancels the call.</param>
/// <returns>Whatever the tool answered, as JSON.</returns>
public delegate ValueTask<JsonElement> ToolInvoker(
    string toolId,
    IReadOnlyDictionary<string, object?> arguments,
    CancellationToken cancellationToken);

/// <summary>
/// Reads one machine, or one work order, straight from the tools the agent uses.
/// </summary>
/// <remarks>
/// <para>
/// No model is in the loop. The panel this feeds is fixed, so nothing here has to be decided by
/// one — and a lookup that cannot invent a value is the whole reason the panel is worth having
/// beside an agent that can.
/// </para>
/// <para>
/// The tools are called at once and shaped afterwards. One failing does not empty the panel: the
/// section it fed is named in <see cref="UnitDocument.Unavailable" /> and the rest still render.
/// </para>
/// </remarks>
/// <param name="invoke">How a tool is called.</param>
public sealed class UnitLookup(ToolInvoker invoke)
{
    /// <summary>How many characters of a serial name the model.</summary>
    private const int ModelDigits = 6;

    private readonly ToolInvoker _invoke = invoke
        ?? throw new ArgumentNullException(nameof(invoke));

    /// <summary>Whether a string is a serial number.</summary>
    /// <param name="text">What the caller sent.</param>
    /// <returns><see langword="true"/> for sixteen digits and nothing else.</returns>
    public static bool IsSerial(string? text)
        => text is { Length: 16 } && text.All(char.IsAsciiDigit);

    /// <summary>Whether a string is a work order number, as in <c>845435-1</c>.</summary>
    /// <param name="text">What the caller sent.</param>
    /// <returns><see langword="true"/> for digits, one dash, then digits.</returns>
    public static bool IsOrderNumber(string? text)
    {
        if (text is null)
        {
            return false;
        }

        var dash = text.IndexOf('-', StringComparison.Ordinal);

        return dash > 0
            && dash < text.Length - 1
            && text.AsSpan(0, dash).ContainsOnlyDigits()
            && text.AsSpan(dash + 1).ContainsOnlyDigits();
    }

    /// <summary>Reads everything the panel shows for one machine.</summary>
    /// <param name="serial">Sixteen digits, already checked by the route.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>
    /// The document, or <see langword="null"/> when no unit carries that number. Both of the two
    /// tools that know a serial have to refuse it before that is concluded: one of them failing on
    /// its own is a section that could not be read, not a machine that does not exist.
    /// </returns>
    public async Task<UnitDocument?> ReadUnitAsync(string serial, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serial);

        var modelNo = serial[..ModelDigits];

        var history = ReadAsync("get_service_history_by_sn", new() { ["SerialNo"] = serial }, cancellationToken);

        var parts = ReadAsync("get_parts_by_sn", new() { ["SerialNo"] = serial }, cancellationToken);
        
        var warranty = ReadAsync(
            "read_records",
            new()
            {
                ["entity"] = "ModelWarranty",
                ["filter"] = $"ModelNo eq '{modelNo}'",
            },
            cancellationToken);

        await Task.WhenAll(history, parts, warranty).ConfigureAwait(false);

        var calls = await history.ConfigureAwait(false);

        var pieces = await parts.ConfigureAwait(false);
        
        var terms = await warranty.ConfigureAwait(false);

        // The stored procedure raises rather than returning nothing when a serial resolves to no
        // model, so a serial nobody has ever sold fails both of these rather than one.
        if (calls is null && pieces is null)
        {
            return null;
        }

        List<UnitSection> unavailable = [];

        if (calls is null)
        {
            unavailable.Add(UnitSection.Jobs);
            unavailable.Add(UnitSection.History);
        }

        if (pieces is null)
        {
            unavailable.Add(UnitSection.Parts);
        }

        var header = HeaderOf(serial, modelNo, calls, pieces);

        if (header is null)
        {
            unavailable.Add(UnitSection.Header);
        }

        var jobs = calls?.Select(JobOf).OrderByDescending(job => job.CalledOn).ToList();
        var covered = TermsOf(terms, header?.ModelVersion, header?.PurchasedOn);

        if (covered is null)
        {
            unavailable.Add(UnitSection.Warranty);
        }

        return new UnitDocument(
            serial,
            header,
            jobs?.Where(job => job.Status != JobStatus.Closed).ToList(),
            jobs,
            pieces?.Select(PartOf).ToList(),
            covered,
            unavailable);
    }

    /// <summary>Reads one work order and the part lines on it.</summary>
    /// <param name="orderNumber">The <c>845435-1</c> key, already checked by the route.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The order, or <see langword="null"/> when no order carries that number.</returns>
    public async Task<OrderDocument?> ReadOrderAsync(string orderNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderNumber);

        var rows = await ReadAsync(
            "get_work_orders",
            new() { ["OrderNumbers"] = orderNumber },
            cancellationToken).ConfigureAwait(false);

        // One flat rowset: the order's own columns repeat on every part line, and an order with no
        // parts still comes back as one row with the part columns null.
        if (rows is not { Count: > 0 })
        {
            return null;
        }

        var first = rows[0];

        return new OrderDocument(
            Text(first, "OrderNumber") ?? orderNumber,
            Number(first, "ServiceId") ?? 0,
            Number(first, "OrderId"),
            Text(first, "OrderType"),
            Moment(first, "OrderDate"),
            Moment(first, "AppointDate"),
            Moment(first, "Shippeddate"),
            Moment(first, "ClosedDate"),
            Text(first, "Tech"),
            Text(first, "ISPName"),
            Text(first, "ISPStatus"),
            Text(first, "DealerNo"),
            Text(first, "Trackno"),
            Text(first, "Notes"),
            [.. rows.Where(row => Text(row, "PartNo") is not null).Select(LineOf)]);
    }

    /// <summary>Calls one tool and reads its rows, or nothing when it refused.</summary>
    /// <param name="toolId">The tool to call.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The rows, or <see langword="null"/> when the tool failed or answered nothing.</returns>
    private async Task<IReadOnlyList<JsonElement>?> ReadAsync(
        string toolId,
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            return RowsOf(await _invoke(toolId, arguments, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A tool that throws is a section that cannot be drawn, never a request that fails. The
            // caller names the section instead, and the rest of the panel still renders.
            return null;
        }
    }

    /// <summary>Finds the rows in whichever envelope a DAB tool wrapped them in.</summary>
    /// <remarks>
    /// A stored procedure answers <c>{ status, value: { value: [...] } }</c> and
    /// <c>read_records</c> answers <c>{ result: { value: [...] } }</c>. A refusal answers
    /// <c>{ status: "error", error: { ... } }</c> and is read here as nothing.
    /// </remarks>
    /// <param name="payload">What the tool answered.</param>
    /// <returns>The rows, or <see langword="null"/> when the payload holds none.</returns>
    private static IReadOnlyList<JsonElement>? RowsOf(JsonElement payload)
    {
        var body = Unwrap(payload);

        if (body.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (body.TryGetProperty("status", out var status)
            && status.ValueKind == JsonValueKind.String
            && string.Equals(status.GetString(), "error", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        foreach (var outer in (string[])["value", "result"])
        {
            if (body.TryGetProperty(outer, out var wrapper)
                && wrapper.ValueKind == JsonValueKind.Object
                && wrapper.TryGetProperty("value", out var rows)
                && rows.ValueKind == JsonValueKind.Array)
            {
                return [.. rows.EnumerateArray()];
            }
        }

        return null;
    }

    /// <summary>Digs the JSON body out of whatever the tool layer handed back.</summary>
    /// <remarks>
    /// An MCP tool answers with content parts, and the part carrying the rows is a string of JSON.
    /// Depending on how the call was made, that arrives already parsed, as that string, or still
    /// inside its <c>content</c> array. All three are the same body.
    /// </remarks>
    /// <param name="payload">What the tool answered.</param>
    /// <returns>The body, parsed.</returns>
    private static JsonElement Unwrap(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array
            && content.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.Object } part
            && part.TryGetProperty("text", out var text))
        {
            return Unwrap(text);
        }

        if (payload.ValueKind != JsonValueKind.String)
        {
            return payload;
        }

        try
        {
            using var parsed = JsonDocument.Parse(payload.GetString() ?? string.Empty);

            return parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            return payload;
        }
    }

    /// <summary>Builds the pinned facts out of whichever tool answered.</summary>
    /// <param name="serial">The serial that was asked for.</param>
    /// <param name="modelNo">Its first six digits.</param>
    /// <param name="calls">The service history rows, or nothing.</param>
    /// <param name="pieces">The parts rows, or nothing.</param>
    /// <returns>The header, or <see langword="null"/> when neither tool answered a row.</returns>
    private static UnitHeader? HeaderOf(
        string serial,
        string modelNo,
        IReadOnlyList<JsonElement>? calls,
        IReadOnlyList<JsonElement>? pieces)
    {
        var call = calls is { Count: > 0 } ? calls[0] : (JsonElement?)null;
        var piece = pieces is { Count: > 0 } ? pieces[0] : (JsonElement?)null;

        if (call is null && piece is null)
        {
            return null;
        }

        // The history rows carry the dates and the parts rows do not, so the history wins wherever
        // both have an opinion and the parts list fills in the model when history is the one missing.
        return new UnitHeader(
            serial,
            modelNo,
            (call is { } c ? Number(c, "ModelVersion") : null) ?? (piece is { } p ? Number(p, "ModelVersion") : null),
            (call is { } c2 ? Text(c2, "ModelName") : null) ?? (piece is { } p2 ? Text(p2, "ModelName") : null),
            call is { } c3 ? Text(c3, "FG") : null,
            call is { } c4 && Flag(c4, "Sole") is true,
            call is { } c5 ? Text(c5, "MfgDate") : null,
            call is { } c6 ? Moment(c6, "PurchasedDate") : null,
            call is { } c7 ? Moment(c7, "SetupDate") : null);
    }

    /// <summary>Reads one service call.</summary>
    /// <param name="row">One row of the history rowset.</param>
    /// <returns>The call, as the panel lists it.</returns>
    private static UnitJob JobOf(JsonElement row)
    {
        var serviceId = Number(row, "ServiceId") ?? 0;
        var orderId = Number(row, "OrderId");
        var word = Text(row, "CaseStatus") ?? Text(row, "ServiceStatus");

        return new UnitJob(
            orderId is { } order ? $"{serviceId}-{order}" : serviceId.ToString(CultureInfo.InvariantCulture),
            serviceId,
            orderId,
            StatusOf(word),
            word,
            Moment(row, "CallDate"),
            Moment(row, "ServiceDate"),
            Text(row, "ServiceRep"),
            Text(row, "Description"),
            Text(row, "Solution"));
    }

    /// <summary>Reads the word a row spells its state with.</summary>
    /// <param name="word">The column's value, which may be anything at all.</param>
    /// <returns>Closed for a closed one, unknown for no word, open for any other word.</returns>
    private static JobStatus StatusOf(string? word)
        => string.IsNullOrWhiteSpace(word)
            ? JobStatus.Unknown
            : word.Equals("closed", StringComparison.OrdinalIgnoreCase) ? JobStatus.Closed : JobStatus.Open;

    /// <summary>Reads one line of the parts list.</summary>
    /// <param name="row">One row of the parts rowset.</param>
    /// <returns>The part.</returns>
    private static UnitPart PartOf(JsonElement row)
        => new(Text(row, "SpNo") ?? string.Empty, Text(row, "DyacoNo"), Text(row, "Description"), Number(row, "Qty"));

    /// <summary>Reads one part line off a work order.</summary>
    /// <param name="row">One row of the work order rowset.</param>
    /// <returns>The line.</returns>
    private static OrderLine LineOf(JsonElement row)
        => new(Text(row, "PartNo"), Text(row, "PartDesc"), Number(row, "ItemShipped"), Flag(row, "IsReturned") is true);

    /// <summary>Counts each of the model's warranty periods forward from the purchase date.</summary>
    /// <remarks>
    /// The periods are held in days, one column per category, and a model carries a row per version.
    /// A machine with no purchase date still lists what it is entitled to; it just cannot say when
    /// any of it runs out.
    /// </remarks>
    /// <param name="rows">The <c>ModelWarranty</c> rows for this model, or nothing.</param>
    /// <param name="version">Which revision this serial falls in.</param>
    /// <param name="purchased">When the machine was bought.</param>
    /// <returns>The terms, or <see langword="null"/> when the table could not be read.</returns>
    private static IReadOnlyList<WarrantyTerm>? TermsOf(
        IReadOnlyList<JsonElement>? rows,
        int? version,
        DateTimeOffset? purchased)
    {
        if (rows is null)
        {
            return null;
        }

        var row = rows.FirstOrDefault(r => version is null || Number(r, "Version") == version);

        if (row.ValueKind != JsonValueKind.Object)
        {
            row = rows.Count > 0 ? rows[^1] : default;
        }

        if (row.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var today = DateTimeOffset.UtcNow;

        return
        [
            .. new[]
            {
                ("Labor", "LaborPeriod"),
                ("Parts", "Part2Period"),
                ("Wear parts", "Part1Period"),
                ("Frame", "Part3Period"),
                ("Deck", "Deck"),
                ("Motor", "Motor"),
                ("Electronics", "Electronics"),
                ("Console", "Console"),
            }
            .Select(term => (term.Item1, Days: Number(row, term.Item2)))
            .Where(term => term.Days is > 0)
            .Select(term =>
            {
                var expires = purchased?.AddDays(term.Days!.Value);

                return new WarrantyTerm(term.Item1, term.Days!.Value, expires, expires is { } end ? end > today : null);
            }),
        ];
    }

    /// <summary>Reads a string column, treating an empty one as absent.</summary>
    private static string? Text(JsonElement row, string name)
        => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() is { Length: > 0 } text ? text : null
            : null;

    /// <summary>Reads a whole-number column.</summary>
    private static int? Number(JsonElement row, string name)
        => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
            ? number
            : null;

    /// <summary>Reads a boolean column.</summary>
    private static bool? Flag(JsonElement row, string name)
        => row.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    /// <summary>
    /// Reads a date column.
    /// </summary>
    /// <remarks>
    /// The database holds these with no time zone on them, so a zone has to be assumed to make a
    /// <see cref="DateTimeOffset" /> at all. UTC is assumed, which keeps every row consistent with
    /// every other; treat the time of day as indicative rather than exact.
    /// </remarks>
    /// <param name="row">The row.</param>
    /// <param name="name">The column.</param>
    /// <returns>The moment, or <see langword="null"/> when the column holds no date.</returns>
    private static DateTimeOffset? Moment(JsonElement row, string name)
        => Text(row, name) is { } text
            && DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed)
            ? new DateTimeOffset(parsed, TimeSpan.Zero)
            : null;
}

/// <summary>Small helpers this file would otherwise repeat.</summary>
internal static class LookupSpanExtensions
{
    /// <summary>Whether a span is one or more ASCII digits and nothing else.</summary>
    /// <param name="span">The span.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool ContainsOnlyDigits(this ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return false;
        }

        foreach (var character in span)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
