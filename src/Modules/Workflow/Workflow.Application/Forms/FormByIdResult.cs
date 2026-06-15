using System.Text.Json;

namespace SaaSApp.Workflow.Application.Forms;

public sealed record FormByIdResult(
    string Id,
    string Name,
    string? Description,
    string? Type,
    string? Layout,
    string PublishOption,
    string CreatedBy,
    string? ModifiedBy,
    DateTime? CreatedAt,
    DateTime? ModifiedAt,
    JsonElement? FormJson = null);
