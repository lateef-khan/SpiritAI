using System.Globalization;
using System.Text.Json;

namespace SpiritAI.Lookup;

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
            DabRow.Text(first, "OrderNumber") ?? orderNumber,
            DabRow.Number(first, "ServiceId") ?? 0,
            DabRow.Number(first, "OrderId"),
            DabRow.Text(first, "OrderType"),
            DabRow.Moment(first, "OrderDate"),
            DabRow.Moment(first, "AppointDate"),
            DabRow.Moment(first, "Shippeddate"),
            DabRow.Moment(first, "ClosedDate"),
            DabRow.Text(first, "Tech"),
            DabRow.Text(first, "ISPName"),
            DabRow.Text(first, "ISPStatus"),
            DabRow.Text(first, "DealerNo"),
            DabRow.Text(first, "Trackno"),
            DabRow.Text(first, "Notes"),
            [.. rows.Where(row => DabRow.Text(row, "PartNo") is not null).Select(LineOf)]);
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
            return DabEnvelope.RowsOf(await _invoke(toolId, arguments, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A tool that throws is a section that cannot be drawn, never a request that fails. The
            // caller names the section instead, and the rest of the panel still renders.
            return null;
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
            (call is { } c ? DabRow.Number(c, "ModelVersion") : null) ?? (piece is { } p ? DabRow.Number(p, "ModelVersion") : null),
            (call is { } c2 ? DabRow.Text(c2, "ModelName") : null) ?? (piece is { } p2 ? DabRow.Text(p2, "ModelName") : null),
            call is { } c3 ? DabRow.Text(c3, "FG") : null,
            call is { } c4 && DabRow.Flag(c4, "Sole") is true,
            call is { } c5 ? DabRow.Text(c5, "MfgDate") : null,
            call is { } c6 ? DabRow.Moment(c6, "PurchasedDate") : null,
            call is { } c7 ? DabRow.Moment(c7, "SetupDate") : null);
    }

    /// <summary>Reads one service call.</summary>
    /// <param name="row">One row of the history rowset.</param>
    /// <returns>The call, as the panel lists it.</returns>
    private static UnitJob JobOf(JsonElement row)
    {
        var serviceId = DabRow.Number(row, "ServiceId") ?? 0;
        var orderId = DabRow.Number(row, "OrderId");
        var word = DabRow.Text(row, "CaseStatus") ?? DabRow.Text(row, "ServiceStatus");

        return new UnitJob(
            orderId is { } order ? $"{serviceId}-{order}" : serviceId.ToString(CultureInfo.InvariantCulture),
            serviceId,
            orderId,
            StatusOf(word),
            word,
            DabRow.Moment(row, "CallDate"),
            DabRow.Moment(row, "ServiceDate"),
            DabRow.Text(row, "ServiceRep"),
            DabRow.Text(row, "Description"),
            DabRow.Text(row, "Solution"));
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
        => new(DabRow.Text(row, "SpNo") ?? string.Empty, DabRow.Text(row, "DyacoNo"), DabRow.Text(row, "Description"), DabRow.Number(row, "Qty"));

    /// <summary>Reads one part line off a work order.</summary>
    /// <param name="row">One row of the work order rowset.</param>
    /// <returns>The line.</returns>
    private static OrderLine LineOf(JsonElement row)
        => new(DabRow.Text(row, "PartNo"), DabRow.Text(row, "PartDesc"), DabRow.Number(row, "ItemShipped"), DabRow.Flag(row, "IsReturned") is true);

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

        var row = rows.FirstOrDefault(r => version is null || DabRow.Number(r, "Version") == version);

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
            .Select(term => (term.Item1, Days: DabRow.Number(row, term.Item2)))
            .Where(term => term.Days is > 0)
            .Select(term =>
            {
                var expires = purchased?.AddDays(term.Days!.Value);

                return new WarrantyTerm(term.Item1, term.Days!.Value, expires, expires is { } end ? end > today : null);
            }),
        ];
    }
}
