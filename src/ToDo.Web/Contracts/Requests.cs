using System.ComponentModel.DataAnnotations;
using ToDo.Domain;

namespace ToDo.Web.Contracts;

/// <summary>
/// Requests validate <em>shape</em>: is this well-formed enough to be worth passing on? The Domain
/// separately enforces the same bounds as invariants. The overlap is deliberate (docs/adr/0005) —
/// these attributes answer an HTTP caller, the Domain answers every caller.
/// </summary>
public sealed record CreateListRequest(
    [property: Required, StringLength(TodoTitle.MaxLength, MinimumLength = 1)] string Title);

public sealed record RenameListRequest(
    [property: Required, StringLength(TodoTitle.MaxLength, MinimumLength = 1)] string Title);

public sealed record AddItemRequest(
    [property: Required, StringLength(TodoTitle.MaxLength, MinimumLength = 1)] string Title,
    DateTimeOffset? DueDate);

/// <summary>Replaces both mutable fields. <c>dueDate: null</c> clears the due date.</summary>
public sealed record UpdateItemRequest(
    [property: Required, StringLength(TodoTitle.MaxLength, MinimumLength = 1)] string Title,
    DateTimeOffset? DueDate);
