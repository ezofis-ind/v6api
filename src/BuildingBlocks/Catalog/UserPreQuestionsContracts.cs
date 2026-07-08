using System.Text.Json;

namespace SaaSApp.Catalog;

public sealed record PreQuestionAnswerDto(string Question, JsonElement Answer);

public sealed record UserPreQuestionsResponse(
    Guid UserId,
    Guid TenantId,
    IReadOnlyList<PreQuestionAnswerDto> Questions);
