using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SaaSApp.Api.Swagger;

public static class SwaggerDocExtensions
{
    public static void AddSaaSAppSwaggerDoc(this SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "SaaSApp API",
            Version = "v1",
            Description = @"
## Authentication

- **Signup** (`POST /api/Signup`): **No JWT required.** Create a new tenant (database + catalog). Optional `password` creates admin for Ezofis login.
- **Ezofis login** (`POST /api/auth/ezofis/login`): **No JWT required.** Email + password. Requires **X-Tenant-Id** header. If 2FA enabled, returns `tempToken`; call `POST /api/auth/2fa/complete` with code.
- **Social login** (`POST /api/auth/social/login`): **No JWT required.** Email + provider (`google` or `microsoft`). No password. Requires **X-Tenant-Id**. User must have matching `loginType` / `authStrategy` in tenant DB.
- **2FA complete** (`POST /api/auth/2fa/complete`): Complete Ezofis login after 2FA. Requires **X-Tenant-Id**.
- **2FA setup/enable/disable** (`POST /api/auth/2fa/setup`, `/enable`, `/disable`): Authenticated user can enable or disable TOTP 2FA.
- **My organizations** (`GET /api/me/tenants`): Returns tenants for current user (for org picker). Requires JWT.
- **All other endpoints** (Users, Admin): **JWT Bearer required.** Use Microsoft Entra ID, Auth0, or Ezofis JWT.

## Multi-tenancy

Each tenant has a dedicated database. Send **X-Tenant-Id** header to select organization, or use JWT `tid` claim.

**Login page flow (how to get X-Tenant-Id before login):**
1. User enters email
2. Call `GET /api/auth/tenants?email=...` (no auth required)
3. Response: list of organizations (tenantId, name, role). If one → show password. If multiple → show org picker, then password.
4. User enters password (and selects org if multiple)
5. Call `POST /api/auth/ezofis/login` with `X-Tenant-Id: {tenantId}` from step 2

**Other ways to get X-Tenant-Id:**
- **After signup:** The signup response includes `tenantId` — use that.
- **After login:** `GET /api/me/tenants` (with JWT) — for switching orgs.

**When is X-Tenant-Id required?**
- **Login and 2FA complete:** Always required (no JWT yet, so API needs to know which org DB to use).
- **Other endpoints (Users, 2FA setup, etc.):** Optional. If omitted, uses JWT `tid` claim. Send it when switching orgs or if your JWT doesn't have `tid`.
".Trim()
        });

        options.OperationFilter<SignupExampleFilter>();
        options.OperationFilter<FormCreateExampleFilter>();
        options.OperationFilter<WorkflowStartExampleFilter>();
        options.OperationFilter<ApAgentStatusExampleFilter>();
        options.OperationFilter<TenantHeaderOperationFilter>();

        // Include XML comments for better documentation
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        // JWT Bearer auth
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT from Ezofis login, Azure AD, or Auth0. Paste token (without 'Bearer' prefix)."
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    }
}

/// <summary>Adds X-Tenant-Id header parameter to operations that require tenant context.</summary>
public class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? "";
        if (path.IndexOf("Signup", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("auth/tenants", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("tenant/checkAuthenticate", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.IndexOf("tenant/validateOTP", StringComparison.OrdinalIgnoreCase) >= 0)
            return; // Pre-signup flows and tenant lookup do not need X-Tenant-Id

        var isLoginOrTwoFactorComplete = path.Contains("ezofis/login") ||
            path.Contains("2fa/complete") ||
            path.Contains("social/login", StringComparison.OrdinalIgnoreCase);

        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Tenant-Id",
            In = ParameterLocation.Header,
            Description = isLoginOrTwoFactorComplete
                ? "Required. Tenant GUID to select organization. Get from GET /api/auth/tenants?email=... (login page)."
                : "Optional. Tenant GUID to select organization. If omitted, uses JWT 'tid' claim. Send when switching orgs or in multi-tenant context.",
            Required = isLoginOrTwoFactorComplete,
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });
    }
}

/// <summary>Sample designer JSON for POST /api/form (Swagger Try it out).</summary>
public class FormCreateExampleFilter : IOperationFilter
{
    private const string SampleJson = """
        {
          "settings": {
            "general": {
              "name": "Test_Workflow_Form",
              "description": "Sample form for Swagger Try it out",
              "layout": "SINGLE",
              "type": "ENTRY",
              "superUser": ["9483F673-416A-43C8-B293-83A72025AF58"],
              "entryUser": ["9483F673-416A-43C8-B293-83A72025AF58"]
            },
            "publish": {
              "publishOption": "PUBLISHED"
            }
          },
          "panels": [
            {
              "id": "main-panel",
              "fields": [
                {
                  "id": "req-title",
                  "label": "Request Title",
                  "type": "TEXT",
                  "settings": {
                    "validation": { "fieldRule": "required" }
                  }
                },
                {
                  "id": "req-amount",
                  "label": "Amount",
                  "type": "NUMBER",
                  "settings": {}
                }
              ]
            }
          ],
          "secondaryPanels": [],
          "isDeleted": false
        }
        """;

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? "";
        if (!path.Equals("api/form", StringComparison.OrdinalIgnoreCase))
            return;
        if (!string.Equals(context.ApiDescription.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            return;

        operation.RequestBody ??= new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>()
        };
        operation.RequestBody.Content.TryAdd("application/json", new OpenApiMediaType
        {
            Schema = new OpenApiSchema { Type = "object" }
        });

        if (!operation.RequestBody.Content.TryGetValue("application/json", out var content))
            return;

        content.Example = OpenApiAnyFactory.CreateFromJson(SampleJson);
        content.Examples ??= new Dictionary<string, OpenApiExample>();
        content.Examples["published"] = new OpenApiExample
        {
            Summary = "Published form (TEXT + NUMBER)",
            Value = OpenApiAnyFactory.CreateFromJson(SampleJson)
        };
        content.Examples["draft"] = new OpenApiExample
        {
            Summary = "Draft (no fields required)",
            Value = OpenApiAnyFactory.CreateFromJson("""
                {
                  "settings": {
                    "general": {
                      "name": "Draft_Form_Sample",
                      "description": "",
                      "layout": "SINGLE",
                      "type": "ENTRY"
                    },
                    "publish": { "publishOption": "DRAFT" }
                  },
                  "panels": [],
                  "secondaryPanels": [],
                  "isDeleted": false
                }
                """)
        };
        content.Examples["panelsOnly"] = new OpenApiExample
        {
            Summary = "Panels only (no root settings; name from panel title)",
            Value = OpenApiAnyFactory.CreateFromJson("""
                {
                  "panels": [
                    {
                      "id": "panel-1",
                      "settings": { "title": "Invoice Approval", "description": "" },
                      "fields": [
                        {
                          "id": "field-upload",
                          "label": "Invoice Upload",
                          "type": "FILE_UPLOAD",
                          "settings": { "validation": { "fieldRule": "OPTIONAL" } }
                        }
                      ]
                    }
                  ]
                }
                """)
        };

        operation.Summary ??= "Create form from designer JSON";
        operation.Description = """
            **JWT required.** Send **X-Tenant-Id** header.

            Creates a form in the tenant database (v5 `POST /api/form` parity).

            **Name:** `settings.general.name`, or top-level `name`, or first panel `settings.title`; otherwise defaults to `Untitled Form`.

            Panels-only bodies (no root `settings`) are stored as sent and created as **DRAFT**.

            **PUBLISHED:** at least one field in `panels` (types other than PARAGRAPH, DIVIDER, LABEL).

            **Response 201:** form id string — use as `FormId` in workflow `InitiateUsing` (GUID or legacy id).

            Replace `superUser` / `entryUser` GUIDs with real user ids from your tenant.
            """.Trim();
    }
}

/// <summary>Sample JSON for POST /api/workflows/{id}/start (application/json).</summary>
public class WorkflowStartExampleFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(context.ApiDescription.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            return;

        var path = context.ApiDescription.RelativePath ?? "";
        var action = context.ApiDescription.ActionDescriptor.RouteValues.TryGetValue("action", out var a)
            ? a
            : null;

        var isMultipartStart = string.Equals(action, "StartWithFile", StringComparison.Ordinal)
            || (path.EndsWith("/start", StringComparison.OrdinalIgnoreCase)
                && path.IndexOf("/start/json", StringComparison.OrdinalIgnoreCase) < 0);

        if (isMultipartStart)
        {
            operation.Summary = "Start workflow (multipart — file upload)";
            operation.Description = """
                **Try it out:** set workflow `id`, then pick a file in the **file** field (optional).

                - **file** — attachment (invoice PDF, etc.). Uses `workflow.RepositoryId` from the workflow row.
                - **envType** — optional (default `trial`).
                - **context** — optional instance context text.

                Leave **file** empty to start without attachment.
                """.Trim();
            return;
        }

        if (!string.Equals(action, "StartWithJson", StringComparison.Ordinal) &&
            !path.EndsWith("/start/json", StringComparison.OrdinalIgnoreCase))
            return;

        if (operation.RequestBody?.Content != null &&
            operation.RequestBody.Content.TryGetValue("application/json", out var jsonContent))
        {
            jsonContent.Example = new OpenApiObject
            {
                ["envType"] = new OpenApiString("trial"),
                ["context"] = new OpenApiString("started from swagger")
            };
        }

        operation.Summary = "Start workflow (JSON — no file)";
        operation.Description = """
            JSON start without file. For file upload use **POST /api/workflows/{id}/start** (multipart).

            Optional `attachment` object with base64 `content`, `fileName`, `contentType`.
            """.Trim();
    }
}

/// <summary>Sample request/response for AP Agent progress and job status endpoints.</summary>
public class ApAgentStatusExampleFilter : IOperationFilter
{
    private const string ProgressSampleJson = """
        {
          "stage": "OCR_RUNNING",
          "message": "Running OCR on invoice PDF",
          "percent": 25
        }
        """;

    private const string StatusResponseSampleJson = """
        {
          "jobId": "12345",
          "workflowId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "instanceId": "f0e1d2c3-b4a5-6789-0123-456789abcdef",
          "hangfireStatus": "Processing",
          "stage": "OCR_RUNNING",
          "message": "Running OCR on invoice PDF",
          "percent": 25,
          "errorMessage": null,
          "updatedAtUtc": "2026-06-15T10:30:45.123Z",
          "isTerminal": false
        }
        """;

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? "";
        var method = context.ApiDescription.HttpMethod ?? "";

        if (path.Contains("ap-agent/progress", StringComparison.OrdinalIgnoreCase)
            && string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase))
        {
            SetProgressRequestExample(operation);
            operation.Summary ??= "Python AP Agent progress callback";
            operation.Description = """
                **Python team:** call this endpoint to publish live stage updates while OCR / extraction runs.

                Prefer the URL from `startPayload.apAgentProgressUrl` (instance-based).  
                Alternative: **PATCH** `/api/workflows/ap-agent/jobs/{jobId}/progress`.

                Returns **204** on success. Requires JWT + **X-Tenant-Id** (use `tenantId` from start payload).
                """.Trim();
            return;
        }

        if (path.Contains("ap-agent/jobs", StringComparison.OrdinalIgnoreCase)
            && path.Contains("progress", StringComparison.OrdinalIgnoreCase)
            && string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase))
        {
            SetProgressRequestExample(operation);
            operation.Summary ??= "Python AP Agent progress callback (by job id)";
            operation.Description = """
                **Python team:** same body as instance progress, but addressed by Hangfire `jobId`.

                Use when you have `apAgentJobId` from `startPayload` but not workflow/instance GUIDs in the URL.
                """.Trim();
            return;
        }

        if (path.Contains("ap-agent/jobs", StringComparison.OrdinalIgnoreCase)
            && !path.Contains("progress", StringComparison.OrdinalIgnoreCase)
            && string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            operation.Responses.TryGetValue("200", out var okResponse);
            okResponse ??= new OpenApiResponse { Description = "Current job status" };
            okResponse.Content ??= new Dictionary<string, OpenApiMediaType>();
            okResponse.Content.TryAdd("application/json", new OpenApiMediaType
            {
                Schema = new OpenApiSchema { Type = "object" }
            });
            if (okResponse.Content.TryGetValue("application/json", out var jsonContent))
            {
                jsonContent.Example = OpenApiAnyFactory.CreateFromJson(StatusResponseSampleJson);
            }

            operation.Responses["200"] = okResponse;
            operation.Summary ??= "Get AP Agent job status (frontend polling)";
            operation.Description = """
                **Frontend team:** poll this endpoint every **5 seconds** after workflow start returns `apAgentJobId`.

                Stop polling when `isTerminal` is `true` (`Succeeded`, `Failed`, or `Deleted`).

                Display `stage`, `message`, and `percent` in the UI.
                """.Trim();
        }
    }

    private static void SetProgressRequestExample(OpenApiOperation operation)
    {
        operation.RequestBody ??= new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>()
        };
        operation.RequestBody.Content.TryAdd("application/json", new OpenApiMediaType
        {
            Schema = new OpenApiSchema { Type = "object" }
        });

        if (!operation.RequestBody.Content.TryGetValue("application/json", out var content))
            return;

        content.Example = OpenApiAnyFactory.CreateFromJson(ProgressSampleJson);
        content.Examples ??= new Dictionary<string, OpenApiExample>();
        content.Examples["ocr"] = new OpenApiExample
        {
            Summary = "OCR in progress",
            Value = OpenApiAnyFactory.CreateFromJson(ProgressSampleJson)
        };
        content.Examples["extracting"] = new OpenApiExample
        {
            Summary = "Extracting invoice fields",
            Value = OpenApiAnyFactory.CreateFromJson("""
                {
                  "stage": "EXTRACTING",
                  "message": "Extracting vendor, date, and line items",
                  "percent": 60
                }
                """)
        };
        content.Examples["completed"] = new OpenApiExample
        {
            Summary = "Completed",
            Value = OpenApiAnyFactory.CreateFromJson("""
                {
                  "stage": "COMPLETED",
                  "message": "AP Agent finished successfully",
                  "percent": 100
                }
                """)
        };
        content.Examples["failed"] = new OpenApiExample
        {
            Summary = "Failed",
            Value = OpenApiAnyFactory.CreateFromJson("""
                {
                  "stage": "FAILED",
                  "message": "OCR could not read the document"
                }
                """)
        };
    }
}

public class SignupExampleFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.RelativePath?.Contains("Signup") != true || operation.RequestBody?.Content == null)
            return;

        if (operation.RequestBody.Content.TryGetValue("application/json", out var content))
        {
            content.Example = new OpenApiObject
            {
                ["tenantId"] = null,
                ["name"] = new OpenApiString("Arasu"),
                ["organizationName"] = new OpenApiString("Acme Corp"),
                ["email"] = new OpenApiString("arasu@acme.com"),
                ["password"] = new OpenApiString("YourSecurePassword123"),
                ["loginType"] = new OpenApiString("EZOFIS"),
                ["licenseType"] = new OpenApiInteger(3),
                ["firstName"] = new OpenApiString("Arasu"),
                ["lastName"] = new OpenApiString("K"),
                ["databaseName"] = null,
                ["signupSource"] = new OpenApiString("MobileApp"),
                ["platform"] = new OpenApiString("Android"),
                ["appVersion"] = new OpenApiString("1.0.0")
            };
        }

        operation.Summary = operation.Summary ?? "Sign up a new tenant";
        operation.Description = @"
**No authentication required.**

Creates a new tenant: provisions a dedicated database, runs migrations, and registers the tenant in the catalog.

- **tenantId**: Optional. Omit to auto-generate; provide only when linking to an existing identity (e.g. Entra ID tenant).
- **name**: Your name or contact name (required if organizationName is not set).
- **organizationName**: Company/org name; used as the tenant display name when set.
- **email**: Contact email (admin user; stored for Ezofis login).
- **password**: Optional. Admin password for Ezofis login.
- **loginType**: Optional. Login source/type, e.g. `EZOFIS`, `GOOGLE`, `MICROSOFT`. Defaults to `EZOFIS`.
- **licenseType**: Optional. `1` = DMS, `2` = Workflow, `3` = DMS+Workflow. Defaults to `3`.
- **firstName**, **lastName**: Optional. Admin profile.
- **databaseName**: Optional. Auto-derived (e.g. ezofis_Tenant_1) if omitted.
- **signupSource**: Where the signup came from: e.g. `MobileApp`, `Web`, `System`, `Api`.
- **platform**: Client platform: e.g. `iOS`, `Android`, `Web`, `Windows`.
- **appVersion**: Application version (for tracking).
".Trim();
    }
}
