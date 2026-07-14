namespace SaaSApp.ActivityLog.Application;

/// <summary>Maps HTTP method/path/status to Event Log title, type, category, and severity.</summary>
public static class EventLogRouteMapper
{
    public const string SeverityInfo = "Info";
    public const string SeverityWarning = "Warning";
    public const string SeverityCritical = "Critical";

    public sealed record MappedEvent(
        string EventTitle,
        string EventType,
        string Category,
        string Severity);

    public static MappedEvent Map(
        string method,
        string path,
        int statusCode,
        EventLogSubject? subject = null)
    {
        var normalizedPath = (path ?? string.Empty).TrimEnd('/');
        var success = statusCode is >= 200 and < 300;
        var isServerError = statusCode >= 500;
        subject ??= new EventLogSubject();

        if (IsAuthLogin(method, normalizedPath))
            return MapAuthLogin(success, statusCode);

        var usersMapped = TryMapUsersModule(method, normalizedPath, statusCode, success, subject);
        if (usersMapped != null)
            return usersMapped;

        var workflowMapped = TryMapWorkflowModule(method, normalizedPath, statusCode, success, subject);
        if (workflowMapped != null)
            return workflowMapped;

        var formMapped = TryMapFormModule(method, normalizedPath, statusCode, success, subject);
        if (formMapped != null)
            return formMapped;

        var repoMapped = TryMapRepositoryModule(method, normalizedPath, statusCode, success, subject);
        if (repoMapped != null)
            return repoMapped;

        return MapGeneric(method, normalizedPath, statusCode, success, isServerError);
    }

    private static MappedEvent? TryMapUsersModule(
        string method,
        string path,
        int statusCode,
        bool success,
        EventLogSubject subject)
    {
        if (IsPutRoleMenus(method, path))
        {
            var id = ExtractSegmentAfter(path, "/api/users/roles/", "/menus");
            return MapWrite(success, WithSubject("Role menus updated", id), "Permission Changed", "Security", SeverityWarning, statusCode);
        }

        if (IsGetRoleById(method, path))
        {
            var id = ExtractGuidAfter(path, "/api/users/roles/");
            return MapWrite(success, WithSubject("Role details viewed", id), "Role Viewed", "User Management", SeverityInfo, statusCode);
        }

        if (IsPutRoleById(method, path))
        {
            var id = ExtractGuidAfter(path, "/api/users/roles/");
            return MapWrite(success, WithSubject("Role permissions updated", FirstNonEmpty(subject.RoleName, id)), "Permission Changed", "Security", SeverityWarning, statusCode);
        }

        if (IsGetRoles(method, path))
            return MapWrite(success, "Roles list viewed", "Roles Listed", "User Management", SeverityInfo, statusCode);

        if (IsPostRoles(method, path))
            return MapWrite(success, WithSubject("New role created", subject.RoleName), "Role Created", "User Management", SeverityInfo, statusCode);

        if (IsGetGroupById(method, path))
        {
            var id = ExtractGuidAfter(path, "/api/users/groups/");
            return MapWrite(success, WithSubject("Group details viewed", id), "Group Viewed", "User Management", SeverityInfo, statusCode);
        }

        if (IsPutGroupById(method, path))
        {
            var id = ExtractGuidAfter(path, "/api/users/groups/");
            return MapWrite(success, WithSubject("Group updated", FirstNonEmpty(subject.GroupName, id)), "Group Updated", "User Management", SeverityInfo, statusCode);
        }

        if (IsDeleteGroupById(method, path))
        {
            var id = ExtractGuidAfter(path, "/api/users/groups/");
            return MapWrite(success, WithSubject("Group deleted", id), "Group Deleted", "User Management", SeverityWarning, statusCode);
        }

        if (IsGetGroups(method, path))
            return MapWrite(success, "Groups list viewed", "Groups Listed", "User Management", SeverityInfo, statusCode);

        if (IsPostGroups(method, path))
            return MapWrite(success, WithSubject("New group created", subject.GroupName), "Group Created", "User Management", SeverityInfo, statusCode);

        if (IsGetPreQuestions(method, path))
        {
            var userId = ExtractGuidBeforeSuffix(path, "/api/users/", "/pre-questions");
            return MapWrite(success, WithSubject("User pre-questions viewed", userId), "PreQuestions Viewed", "User Management", SeverityInfo, statusCode);
        }

        if (IsPutPreQuestions(method, path))
        {
            var userId = ExtractGuidBeforeSuffix(path, "/api/users/", "/pre-questions");
            return MapWrite(success, WithSubject("User pre-questions updated", userId), "PreQuestions Updated", "User Management", SeverityInfo, statusCode);
        }

        if (IsPostUsers(method, path))
            return MapWrite(success, WithSubject("New user account created", FirstNonEmpty(subject.Email, subject.DisplayName)), "User Created", "User Management", SeverityInfo, statusCode);

        if (IsGetUsers(method, path))
            return MapWrite(success, "Users list viewed", "Users Listed", "User Management", SeverityInfo, statusCode);

        if (IsGetUserById(method, path))
        {
            var id = ExtractGuidAfter(path, "/api/users/");
            return MapWrite(success, WithSubject("User profile viewed", id), "User Viewed", "User Management", SeverityInfo, statusCode);
        }

        if (IsPutUserById(method, path))
        {
            var id = ExtractGuidAfter(path, "/api/users/");
            return MapWrite(success, WithSubject("User account updated", FirstNonEmpty(subject.Email, subject.DisplayName, id)), "User Updated", "User Management", SeverityInfo, statusCode);
        }

        if (IsDeleteUserById(method, path))
        {
            var id = ExtractGuidAfter(path, "/api/users/");
            return MapWrite(success, WithSubject("User account deleted", id), "User Deleted", "User Management", SeverityWarning, statusCode);
        }

        return null;
    }

    private static MappedEvent? TryMapWorkflowModule(
        string method,
        string path,
        int statusCode,
        bool success,
        EventLogSubject subject)
    {
        // Legacy routes
        if (HttpMethods.IsGet(method) && path.StartsWith("/api/workflow/listByUserId", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, "Workflows list viewed", "Workflows Listed", "Workflow", SeverityInfo, statusCode);

        if (HttpMethods.IsPost(method) && path.StartsWith("/api/workflow/inboxList", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, "Workflow inbox viewed", "Inbox Viewed", "Workflow", SeverityInfo, statusCode);

        if (HttpMethods.IsPost(method) && path.Equals("/api/workflow/all", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, "Workflows list viewed", "Workflows Listed", "Workflow", SeverityInfo, statusCode);

        if (!path.StartsWith("/api/workflows", StringComparison.OrdinalIgnoreCase))
            return null;

        if (IsWorkflowStepApprove(method, path))
        {
            var instanceId = ExtractWorkflowInstanceId(path);
            return MapWrite(success, WithSubject("Workflow step approved", instanceId), "Step Approved", "Workflow", SeverityInfo, statusCode);
        }

        if (IsWorkflowStepReject(method, path))
        {
            var instanceId = ExtractWorkflowInstanceId(path);
            return MapWrite(success, WithSubject("Workflow step rejected", instanceId), "Step Rejected", "Workflow", SeverityWarning, statusCode);
        }

        if (IsWorkflowMoveNext(method, path))
        {
            var instanceId = ExtractGuidAfter(path, "/api/workflows/instances/");
            return MapWrite(success, WithSubject("Workflow moved to next step", instanceId), "Step Advanced", "Workflow", SeverityInfo, statusCode);
        }

        if (IsWorkflowActions(method, path))
        {
            var instanceId = ExtractGuidAfter(path, "/api/workflows/instances/");
            return MapWrite(success, WithSubject("Workflow action performed", instanceId), "Workflow Action", "Workflow", SeverityInfo, statusCode);
        }

        if (IsWorkflowShareFile(method, path))
        {
            var instanceId = ExtractGuidAfter(path, "/api/workflows/instances/");
            return MapWrite(success, WithSubject("Workflow file shared", instanceId), "File Shared", "Workflow", SeverityInfo, statusCode);
        }

        if (IsWorkflowPublish(method, path))
        {
            var id = ExtractGuidBeforeSuffix(path, "/api/workflows/", "/publish");
            return MapWrite(success, WithSubject("Workflow published", id), "Workflow Published", "Workflow", SeverityInfo, statusCode);
        }

        if (IsWorkflowStart(method, path))
        {
            var id = ExtractWorkflowStartId(path);
            return MapWrite(success, WithSubject("Workflow started", id), "Workflow Started", "Workflow", SeverityInfo, statusCode);
        }

        if (HttpMethods.IsGet(method) && path.Equals("/api/workflows/inbox", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, "Workflow inbox viewed", "Inbox Viewed", "Workflow", SeverityInfo, statusCode);

        if (HttpMethods.IsGet(method) && path.Equals("/api/workflows/sent", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, "Workflow sent list viewed", "Sent Viewed", "Workflow", SeverityInfo, statusCode);

        if (HttpMethods.IsGet(method) && path.Equals("/api/workflows/completed", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, "Workflow completed list viewed", "Completed Viewed", "Workflow", SeverityInfo, statusCode);

        if (HttpMethods.IsPost(method) && path.Equals("/api/workflows", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, WithSubject("New workflow created", subject.Name), "Workflow Created", "Workflow", SeverityInfo, statusCode);

        if (HttpMethods.IsGet(method) && path.Equals("/api/workflows", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, "Workflows list viewed", "Workflows Listed", "Workflow", SeverityInfo, statusCode);

        if (HttpMethods.IsGet(method) && MatchesGuidSegment(path, "/api/workflows/"))
        {
            var id = ExtractGuidAfter(path, "/api/workflows/");
            return MapWrite(success, WithSubject("Workflow details viewed", id), "Workflow Viewed", "Workflow", SeverityInfo, statusCode);
        }

        if (HttpMethods.IsPut(method) && MatchesGuidSegment(path, "/api/workflows/"))
        {
            var id = ExtractGuidAfter(path, "/api/workflows/");
            return MapWrite(success, WithSubject("Workflow updated", FirstNonEmpty(subject.Name, id)), "Workflow Updated", "Workflow", SeverityInfo, statusCode);
        }

        if (HttpMethods.IsDelete(method) && MatchesGuidSegment(path, "/api/workflows/"))
        {
            var id = ExtractGuidAfter(path, "/api/workflows/");
            return MapWrite(success, WithSubject("Workflow deleted", id), "Workflow Deleted", "Workflow", SeverityWarning, statusCode);
        }

        return null;
    }

    private static MappedEvent? TryMapFormModule(
        string method,
        string path,
        int statusCode,
        bool success,
        EventLogSubject subject)
    {
        if (!path.StartsWith("/api/form", StringComparison.OrdinalIgnoreCase))
            return null;

        if ((HttpMethods.IsGet(method) || HttpMethods.IsPost(method))
            && path.Equals("/api/form/all", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, "Forms list viewed", "Forms Listed", "Forms", SeverityInfo, statusCode);

        if (HttpMethods.IsPost(method) && path.Equals("/api/form", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, WithSubject("New form created", subject.Name), "Form Created", "Forms", SeverityInfo, statusCode);

        if (HttpMethods.IsGet(method) && MatchesSingleSegment(path, "/api/form/", "all", "uploadMasterFile"))
        {
            var id = ExtractSegmentAfterPrefix(path, "/api/form/");
            return MapWrite(success, WithSubject("Form details viewed", id), "Form Viewed", "Forms", SeverityInfo, statusCode);
        }

        if (HttpMethods.IsPut(method) && MatchesSingleSegment(path, "/api/form/"))
        {
            var id = ExtractSegmentAfterPrefix(path, "/api/form/");
            return MapWrite(success, WithSubject("Form updated", FirstNonEmpty(subject.Name, id)), "Form Updated", "Forms", SeverityInfo, statusCode);
        }

        if (HttpMethods.IsDelete(method) && MatchesSingleSegment(path, "/api/form/"))
        {
            var id = ExtractSegmentAfterPrefix(path, "/api/form/");
            return MapWrite(success, WithSubject("Form deleted", id), "Form Deleted", "Forms", SeverityWarning, statusCode);
        }

        return null;
    }

    private static MappedEvent? TryMapRepositoryModule(
        string method,
        string path,
        int statusCode,
        bool success,
        EventLogSubject subject)
    {
        if (!path.StartsWith("/api/repositories", StringComparison.OrdinalIgnoreCase))
            return null;

        if (HttpMethods.IsGet(method) && path.Equals("/api/repositories/shared-with-me", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, "Shared repositories viewed", "Shared Viewed", "Repository", SeverityInfo, statusCode);

        if (HttpMethods.IsDelete(method) && path.StartsWith("/api/repositories/share/", StringComparison.OrdinalIgnoreCase))
        {
            var shareId = ExtractSegmentAfterPrefix(path, "/api/repositories/share/");
            return MapWrite(success, WithSubject("Repository share revoked", shareId), "Share Revoked", "Repository", SeverityWarning, statusCode);
        }

        if (IsRepositoryItemShare(method, path))
        {
            var itemId = ExtractRepositoryItemId(path);
            return MapWrite(success, WithSubject("Repository item shared", FirstNonEmpty(subject.Email, itemId)), "Item Shared", "Repository", SeverityInfo, statusCode);
        }

        if (IsRepositoryItemUpload(method, path))
        {
            var repoId = ExtractGuidAfter(path, "/api/repositories/");
            return MapWrite(success, WithSubject("Repository file uploaded", FirstNonEmpty(subject.FileName, repoId)), "File Uploaded", "Repository", SeverityInfo, statusCode);
        }

        if (IsRepositoryItemCreate(method, path))
        {
            var repoId = ExtractGuidAfter(path, "/api/repositories/");
            return MapWrite(success, WithSubject("Repository item created", FirstNonEmpty(subject.FileName, repoId)), "Item Created", "Repository", SeverityInfo, statusCode);
        }

        if (HttpMethods.IsPost(method) && path.Equals("/api/repositories", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, WithSubject("New repository created", subject.Name), "Repository Created", "Repository", SeverityInfo, statusCode);

        if (HttpMethods.IsGet(method) && path.Equals("/api/repositories", StringComparison.OrdinalIgnoreCase))
            return MapWrite(success, "Repositories list viewed", "Repositories Listed", "Repository", SeverityInfo, statusCode);

        if (HttpMethods.IsGet(method) && MatchesGuidSegment(path, "/api/repositories/"))
        {
            var id = ExtractGuidAfter(path, "/api/repositories/");
            return MapWrite(success, WithSubject("Repository details viewed", id), "Repository Viewed", "Repository", SeverityInfo, statusCode);
        }

        if (HttpMethods.IsPut(method) && MatchesGuidSegment(path, "/api/repositories/"))
        {
            var id = ExtractGuidAfter(path, "/api/repositories/");
            return MapWrite(success, WithSubject("Repository updated", FirstNonEmpty(subject.Name, id)), "Repository Updated", "Repository", SeverityInfo, statusCode);
        }

        return null;
    }

    private static string WithSubject(string baseTitle, string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return baseTitle;
        return $"{baseTitle}: {subject.Trim()}";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static MappedEvent MapAuthLogin(bool success, int statusCode)
    {
        if (success)
            return new MappedEvent("User logged in successfully", "Login", "Authentication", SeverityInfo);

        if (statusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden or >= 400)
            return new MappedEvent("Failed login attempt", "Security Event", "Security", SeverityCritical);

        return new MappedEvent("Failed login attempt", "Security Event", "Security", SeverityCritical);
    }

    private static MappedEvent MapWrite(
        bool success,
        string successTitle,
        string eventType,
        string category,
        string successSeverity,
        int statusCode)
    {
        if (success)
            return new MappedEvent(successTitle, eventType, category, successSeverity);

        var failSeverity = statusCode >= 500 ? SeverityCritical
            : string.Equals(category, "Security", StringComparison.OrdinalIgnoreCase) ? SeverityCritical
            : SeverityWarning;

        return new MappedEvent(
            $"Request failed: {successTitle}",
            eventType,
            category,
            failSeverity);
    }

    private static MappedEvent MapGeneric(
        string method,
        string path,
        int statusCode,
        bool success,
        bool isServerError)
    {
        var severity = isServerError ? SeverityCritical
            : statusCode >= 400 ? SeverityWarning
            : SeverityInfo;

        var title = success
            ? $"{method.ToUpperInvariant()} {path}"
            : $"Request failed: {method.ToUpperInvariant()} {path}";

        return new MappedEvent(title, "Api Request", "System", severity);
    }

    private static bool IsAuthLogin(string method, string path) =>
        HttpMethods.IsPost(method) && (
            path.Equals("/api/auth/ezofis/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/social/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/2fa/complete", StringComparison.OrdinalIgnoreCase));

    private static bool IsPostUsers(string method, string path) =>
        HttpMethods.IsPost(method) && path.Equals("/api/users", StringComparison.OrdinalIgnoreCase);

    private static bool IsGetUsers(string method, string path) =>
        HttpMethods.IsGet(method) && path.Equals("/api/users", StringComparison.OrdinalIgnoreCase);

    private static bool IsGetUserById(string method, string path) =>
        HttpMethods.IsGet(method) && MatchesGuidSegment(path, "/api/users/");

    private static bool IsPutUserById(string method, string path) =>
        HttpMethods.IsPut(method) && MatchesGuidSegment(path, "/api/users/");

    private static bool IsDeleteUserById(string method, string path) =>
        HttpMethods.IsDelete(method) && MatchesGuidSegment(path, "/api/users/");

    private static bool IsGetPreQuestions(string method, string path) =>
        HttpMethods.IsGet(method) && MatchesGuidThenSuffix(path, "/api/users/", "/pre-questions");

    private static bool IsPutPreQuestions(string method, string path) =>
        HttpMethods.IsPut(method) && MatchesGuidThenSuffix(path, "/api/users/", "/pre-questions");

    private static bool IsGetRoles(string method, string path) =>
        HttpMethods.IsGet(method) && path.Equals("/api/users/roles", StringComparison.OrdinalIgnoreCase);

    private static bool IsPostRoles(string method, string path) =>
        HttpMethods.IsPost(method) && path.Equals("/api/users/roles", StringComparison.OrdinalIgnoreCase);

    private static bool IsGetRoleById(string method, string path) =>
        HttpMethods.IsGet(method) && MatchesGuidSegment(path, "/api/users/roles/");

    private static bool IsPutRoleById(string method, string path)
    {
        if (!HttpMethods.IsPut(method))
            return false;

        if (!path.StartsWith("/api/users/roles/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.EndsWith("/menus", StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = path["/api/users/roles/".Length..];
        return Guid.TryParse(remainder, out _);
    }

    private static bool IsPutRoleMenus(string method, string path) =>
        HttpMethods.IsPut(method)
        && path.StartsWith("/api/users/roles/", StringComparison.OrdinalIgnoreCase)
        && path.EndsWith("/menus", StringComparison.OrdinalIgnoreCase);

    private static bool IsGetGroups(string method, string path) =>
        HttpMethods.IsGet(method) && path.Equals("/api/users/groups", StringComparison.OrdinalIgnoreCase);

    private static bool IsPostGroups(string method, string path) =>
        HttpMethods.IsPost(method) && path.Equals("/api/users/groups", StringComparison.OrdinalIgnoreCase);

    private static bool IsGetGroupById(string method, string path) =>
        HttpMethods.IsGet(method) && MatchesGuidSegment(path, "/api/users/groups/");

    private static bool IsPutGroupById(string method, string path) =>
        HttpMethods.IsPut(method) && MatchesGuidSegment(path, "/api/users/groups/");

    private static bool IsDeleteGroupById(string method, string path) =>
        HttpMethods.IsDelete(method) && MatchesGuidSegment(path, "/api/users/groups/");

    private static bool IsWorkflowStepApprove(string method, string path) =>
        HttpMethods.IsPost(method)
        && path.StartsWith("/api/workflows/instances/", StringComparison.OrdinalIgnoreCase)
        && path.EndsWith("/approve", StringComparison.OrdinalIgnoreCase);

    private static bool IsWorkflowStepReject(string method, string path) =>
        HttpMethods.IsPost(method)
        && path.StartsWith("/api/workflows/instances/", StringComparison.OrdinalIgnoreCase)
        && path.EndsWith("/reject", StringComparison.OrdinalIgnoreCase);

    private static bool IsWorkflowMoveNext(string method, string path) =>
        HttpMethods.IsPost(method)
        && path.StartsWith("/api/workflows/instances/", StringComparison.OrdinalIgnoreCase)
        && path.EndsWith("/move-next", StringComparison.OrdinalIgnoreCase);

    private static bool IsWorkflowActions(string method, string path) =>
        HttpMethods.IsPost(method)
        && path.StartsWith("/api/workflows/instances/", StringComparison.OrdinalIgnoreCase)
        && path.EndsWith("/actions", StringComparison.OrdinalIgnoreCase);

    private static bool IsWorkflowShareFile(string method, string path) =>
        HttpMethods.IsPost(method)
        && path.StartsWith("/api/workflows/instances/", StringComparison.OrdinalIgnoreCase)
        && path.EndsWith("/share-file", StringComparison.OrdinalIgnoreCase);

    private static bool IsWorkflowPublish(string method, string path) =>
        HttpMethods.IsPost(method) && MatchesGuidThenSuffix(path, "/api/workflows/", "/publish");

    private static bool IsWorkflowStart(string method, string path)
    {
        if (!HttpMethods.IsPost(method))
            return false;
        if (MatchesGuidThenSuffix(path, "/api/workflows/", "/start"))
            return true;
        if (MatchesGuidThenSuffix(path, "/api/workflows/", "/start/json"))
            return true;
        return false;
    }

    private static string? ExtractWorkflowStartId(string path)
    {
        var id = ExtractGuidBeforeSuffix(path, "/api/workflows/", "/start/json");
        if (id != null)
            return id;
        return ExtractGuidBeforeSuffix(path, "/api/workflows/", "/start");
    }

    private static string? ExtractWorkflowInstanceId(string path)
    {
        // /api/workflows/instances/{instanceId}/steps/{stepId}/approve|reject
        if (!path.StartsWith("/api/workflows/instances/", StringComparison.OrdinalIgnoreCase))
            return null;
        return ExtractGuidAfter(path, "/api/workflows/instances/");
    }

    private static bool IsRepositoryItemShare(string method, string path) =>
        HttpMethods.IsPost(method)
        && path.StartsWith("/api/repositories/", StringComparison.OrdinalIgnoreCase)
        && path.Contains("/items/", StringComparison.OrdinalIgnoreCase)
        && path.EndsWith("/share", StringComparison.OrdinalIgnoreCase);

    private static bool IsRepositoryItemUpload(string method, string path) =>
        HttpMethods.IsPost(method)
        && path.StartsWith("/api/repositories/", StringComparison.OrdinalIgnoreCase)
        && (path.EndsWith("/items/upload", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/items/upload-archive", StringComparison.OrdinalIgnoreCase));

    private static bool IsRepositoryItemCreate(string method, string path)
    {
        if (!HttpMethods.IsPost(method))
            return false;
        if (!path.StartsWith("/api/repositories/", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!path.EndsWith("/items", StringComparison.OrdinalIgnoreCase))
            return false;
        // /api/repositories/{guid}/items
        var mid = path["/api/repositories/".Length..];
        var slash = mid.IndexOf('/');
        if (slash < 0)
            return false;
        var idPart = mid[..slash];
        var rest = mid[(slash + 1)..];
        return Guid.TryParse(idPart, out _) && rest.Equals("items", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractRepositoryItemId(string path)
    {
        // /api/repositories/{repoId}/items/{itemId}/share
        const string marker = "/items/";
        var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;
        var after = path[(idx + marker.Length)..];
        var slash = after.IndexOf('/');
        if (slash >= 0)
            after = after[..slash];
        return string.IsNullOrWhiteSpace(after) ? null : after;
    }

    private static bool MatchesGuidSegment(string path, string prefix)
    {
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = path[prefix.Length..];
        if (remainder.Contains('/'))
            return false;

        return Guid.TryParse(remainder, out _);
    }

    private static bool MatchesSingleSegment(string path, string prefix, params string[] reserved)
    {
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = path[prefix.Length..];
        if (string.IsNullOrWhiteSpace(remainder) || remainder.Contains('/'))
            return false;

        foreach (var r in reserved)
        {
            if (remainder.Equals(r, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static bool MatchesGuidThenSuffix(string path, string prefix, string suffix)
    {
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var mid = path[prefix.Length..^suffix.Length];
        if (mid.EndsWith('/'))
            mid = mid[..^1];

        return Guid.TryParse(mid, out _);
    }

    private static string? ExtractGuidAfter(string path, string prefix)
    {
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var remainder = path[prefix.Length..];
        var slash = remainder.IndexOf('/');
        if (slash >= 0)
            remainder = remainder[..slash];

        return Guid.TryParse(remainder, out _) ? remainder : null;
    }

    private static string? ExtractSegmentAfterPrefix(string path, string prefix)
    {
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var remainder = path[prefix.Length..];
        var slash = remainder.IndexOf('/');
        if (slash >= 0)
            remainder = remainder[..slash];

        return string.IsNullOrWhiteSpace(remainder) ? null : remainder;
    }

    private static string? ExtractSegmentAfter(string path, string prefix, string suffix)
    {
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return null;

        var mid = path[prefix.Length..^suffix.Length];
        if (mid.EndsWith('/'))
            mid = mid[..^1];

        return string.IsNullOrWhiteSpace(mid) ? null : mid;
    }

    private static string? ExtractGuidBeforeSuffix(string path, string prefix, string suffix)
    {
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return null;

        var mid = path[prefix.Length..^suffix.Length];
        if (mid.EndsWith('/'))
            mid = mid[..^1];

        return Guid.TryParse(mid, out _) ? mid : null;
    }

    private static class StatusCodes
    {
        public const int Status401Unauthorized = 401;
        public const int Status403Forbidden = 403;
    }

    private static class HttpMethods
    {
        public static bool IsGet(string method) =>
            string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);

        public static bool IsPost(string method) =>
            string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);

        public static bool IsPut(string method) =>
            string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase);

        public static bool IsDelete(string method) =>
            string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);
    }
}
