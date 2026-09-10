using System.Text.Json.Serialization;

namespace SpiritAI.Lookup;

/// <summary>
/// The shapes the unit panel reads.
/// </summary>
/// <remarks>
/// Written once, here. The browser's client is generated from the OpenAPI document these produce,
/// so a field renamed here is a compile error there rather than a panel that quietly goes blank.
/// Every union is an enum for the same reason: a <c>string</c> would reach the browser as a
/// <c>string</c> and be narrowed back by hand.
/// </remarks>

/// <summary>One part of a unit document, named so a caller can say which one is missing.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UnitSection>))]
public enum UnitSection
{
    /// <summary>The pinned facts: model, dates, category.</summary>
    [JsonStringEnumMemberName("header")]
    Header,

    /// <summary>Service calls that are not closed.</summary>
    [JsonStringEnumMemberName("jobs")]
    Jobs,

    /// <summary>Every service call, newest first.</summary>
    [JsonStringEnumMemberName("history")]
    History,

    /// <summary>The model's parts list.</summary>
    [JsonStringEnumMemberName("parts")]
    Parts,

    /// <summary>What is still covered, and until when.</summary>
    [JsonStringEnumMemberName("warranty")]
    Warranty,
}

/// <summary>Where one service call has got to.</summary>
/// <remarks>
/// Derived, and the word it was derived from is kept beside it. <c>CustService</c> spells this
/// column several ways and one of them is the string <c>"fASLE"</c>; the panel shows the word it
/// was given and filters on the enum, so a spelling nobody has seen yet cannot hide a job.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<JobStatus>))]
public enum JobStatus
{
    /// <summary>Anything that names a state but not a closed one.</summary>
    [JsonStringEnumMemberName("open")]
    Open,

    /// <summary>Closed.</summary>
    [JsonStringEnumMemberName("closed")]
    Closed,

    /// <summary>The row names no state at all.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}

/// <summary>Everything the panel shows for one machine.</summary>
/// <param name="Serial">The serial that was asked for.</param>
/// <param name="Header">The pinned facts, or <see langword="null"/> when they could not be read.</param>
/// <param name="Jobs">Service calls that are not closed, newest first.</param>
/// <param name="History">Every service call, newest first.</param>
/// <param name="Parts">The model's parts list.</param>
/// <param name="Warranty">What is covered, and until when.</param>
/// <param name="Unavailable">
/// The sections that could not be read. A section named here is <see langword="null"/> above, and
/// the panel says so in that one place rather than rendering an empty tab that looks like an answer.
/// </param>
public sealed record UnitDocument(
    string Serial,
    UnitHeader? Header,
    IReadOnlyList<UnitJob>? Jobs,
    IReadOnlyList<UnitJob>? History,
    IReadOnlyList<UnitPart>? Parts,
    IReadOnlyList<WarrantyTerm>? Warranty,
    IReadOnlyList<UnitSection> Unavailable);

/// <summary>The facts that stay pinned above the tabs.</summary>
/// <param name="Serial">The machine's serial number.</param>
/// <param name="ModelNo">The six digits the serial starts with.</param>
/// <param name="ModelVersion">Which revision of that model this serial falls in.</param>
/// <param name="ModelName">The model as a person would say it, e.g. <c>SOLE WF80 2010</c>.</param>
/// <param name="Category">What kind of machine it is, e.g. <c>TREADMILL</c>.</param>
/// <param name="IsSole">Whether it is a Sole-branded machine.</param>
/// <param name="ManufacturedOn">
/// A month and a year, as the database holds it: <c>04/2010</c>. Not a date — the column is a
/// string and there is no day in it to invent.
/// </param>
/// <param name="PurchasedOn">When it was bought, which is what every warranty period counts from.</param>
/// <param name="SetUpOn">When it was set up.</param>
public sealed record UnitHeader(
    string Serial,
    string ModelNo,
    int? ModelVersion,
    string? ModelName,
    string? Category,
    bool IsSole,
    string? ManufacturedOn,
    DateTimeOffset? PurchasedOn,
    DateTimeOffset? SetUpOn);

/// <summary>One service call on this machine.</summary>
/// <param name="OrderNumber">The <c>845435-1</c> key, which is what staff say out loud.</param>
/// <param name="ServiceId">The case.</param>
/// <param name="OrderId">Which order within the case.</param>
/// <param name="Status">Where it has got to.</param>
/// <param name="StatusText">The word the database gave, kept because the enum above is derived.</param>
/// <param name="CalledOn">When the customer rang.</param>
/// <param name="ServicedOn">When it was worked on.</param>
/// <param name="Technician">Who took it.</param>
/// <param name="Summary">What the customer reported.</param>
/// <param name="Solution">What was done, when anybody wrote it down.</param>
public sealed record UnitJob(
    string OrderNumber,
    int ServiceId,
    int? OrderId,
    JobStatus Status,
    string? StatusText,
    DateTimeOffset? CalledOn,
    DateTimeOffset? ServicedOn,
    string? Technician,
    string? Summary,
    string? Solution);

/// <summary>One line of the model's parts list.</summary>
/// <param name="SpNo">The part number staff order by.</param>
/// <param name="DyacoNo">The factory's number for the same part, often empty.</param>
/// <param name="Description">What it is.</param>
/// <param name="Quantity">How many the machine has.</param>
public sealed record UnitPart(string SpNo, string? DyacoNo, string? Description, int? Quantity);

/// <summary>One warranty period, already counted forward from the purchase date.</summary>
/// <param name="Category">What it covers, e.g. <c>Labor</c> or <c>Motor</c>.</param>
/// <param name="Days">The period the model carries, in days.</param>
/// <param name="ExpiresOn">
/// When it runs out, or <see langword="null"/> when the machine has no purchase date to count from.
/// </param>
/// <param name="IsCovered">
/// Whether it is still covered today, or <see langword="null"/> when there is no date to judge.
/// </param>
public sealed record WarrantyTerm(string Category, int Days, DateTimeOffset? ExpiresOn, bool? IsCovered);

/// <summary>One work order, with the part lines it shipped.</summary>
/// <param name="OrderNumber">The <c>845435-1</c> key.</param>
/// <param name="ServiceId">The case.</param>
/// <param name="OrderId">Which order within the case.</param>
/// <param name="OrderType">Warranty, purchase, and so on.</param>
/// <param name="OrderedOn">When the order was raised.</param>
/// <param name="AppointedOn">When a visit was booked, when one was.</param>
/// <param name="ShippedOn">When it left.</param>
/// <param name="ClosedOn">When it was closed, when it has been.</param>
/// <param name="Technician">Who took it.</param>
/// <param name="IspName">The independent service provider, when one was used.</param>
/// <param name="IspStatus">Where that provider has got to.</param>
/// <param name="DealerNo">The dealer, when the order came through one.</param>
/// <param name="TrackingNo">The carrier's number.</param>
/// <param name="Notes">Whatever was typed on the order.</param>
/// <param name="Lines">The parts on it. Empty when the order shipped none.</param>
public sealed record OrderDocument(
    string OrderNumber,
    int ServiceId,
    int? OrderId,
    string? OrderType,
    DateTimeOffset? OrderedOn,
    DateTimeOffset? AppointedOn,
    DateTimeOffset? ShippedOn,
    DateTimeOffset? ClosedOn,
    string? Technician,
    string? IspName,
    string? IspStatus,
    string? DealerNo,
    string? TrackingNo,
    string? Notes,
    IReadOnlyList<OrderLine> Lines);

/// <summary>One part line on a work order.</summary>
/// <param name="PartNo">The part ordered.</param>
/// <param name="Description">What it is.</param>
/// <param name="Shipped">How many went out.</param>
/// <param name="IsReturned">Whether the old one was asked back.</param>
public sealed record OrderLine(string? PartNo, string? Description, int? Shipped, bool IsReturned);

/// <summary>
/// The machines one customer owns.
/// </summary>
/// <remarks>
/// The desk this replaces returned no contact details, and neither does this: a serial number is
/// what the caller needs, and a phone number is not theirs to read out.
/// </remarks>
/// <param name="Units">One row per machine, as the records hold them.</param>
/// <param name="TotalRows">How many machines matched in all, before any cap.</param>
/// <param name="Note">One line the agent may repeat about what happened.</param>
public sealed record CustomerUnits(
    IReadOnlyList<CustomerUnit> Units,
    int TotalRows,
    string Note);

/// <summary>
/// One machine a customer owns.
/// </summary>
/// <param name="SerialNo">The sixteen digit serial number.</param>
/// <param name="ModelNo">The six digit model number.</param>
/// <param name="ModelName">The model's name, as the records hold it.</param>
/// <param name="PurchasedOn">When it was bought, or <see langword="null"/> when no record says.</param>
/// <param name="SetUpOn">When it was set up, or <see langword="null"/> when no record says.</param>
/// <param name="OpenOrders">How many work orders on this machine are still open.</param>
/// <param name="TotalOrders">How many work orders it has had in all.</param>
public sealed record CustomerUnit(
    string SerialNo,
    string? ModelNo,
    string? ModelName,
    DateTimeOffset? PurchasedOn,
    DateTimeOffset? SetUpOn,
    int? OpenOrders,
    int? TotalOrders);
