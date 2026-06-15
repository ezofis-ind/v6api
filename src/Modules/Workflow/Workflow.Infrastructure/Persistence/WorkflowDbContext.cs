using Microsoft.EntityFrameworkCore;
using SaaSApp.MultiTenancy;
using SaaSApp.Workflow.Domain.Entities;

namespace SaaSApp.Workflow.Infrastructure.Persistence;

public sealed class WorkflowDbContext : DbContext
{
    public const string SchemaName = "workflow";

    private readonly ITenantProvider _tenantProvider;

    public DbSet<Domain.Entities.Workflow> Workflows => Set<Domain.Entities.Workflow>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowStepInstance> WorkflowStepInstances => Set<WorkflowStepInstance>();
    public DbSet<WorkflowApproval> WorkflowApprovals => Set<WorkflowApproval>();
    public DbSet<WorkflowSla> WorkflowSlas => Set<WorkflowSla>();
    public DbSet<WorkflowInstanceSla> WorkflowInstanceSlas => Set<WorkflowInstanceSla>();
    public DbSet<WorkflowComment> WorkflowComments => Set<WorkflowComment>();
    public DbSet<WorkflowAttachment> WorkflowAttachments => Set<WorkflowAttachment>();
    public DbSet<WorkflowForm> WorkflowForms => Set<WorkflowForm>();
    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();
    public DbSet<WorkflowSignature> WorkflowSignatures => Set<WorkflowSignature>();
    public DbSet<WorkflowDocument> WorkflowDocuments => Set<WorkflowDocument>();
    public DbSet<WorkflowEmail> WorkflowEmails => Set<WorkflowEmail>();
    public DbSet<WorkflowAiValidation> WorkflowAiValidations => Set<WorkflowAiValidation>();
    public DbSet<WorkflowPdfAnnotation> WorkflowPdfAnnotations => Set<WorkflowPdfAnnotation>();

    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<Domain.Entities.Workflow>(entity =>
        {
            entity.ToTable("Workflows");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.TriggerType).IsRequired();
            entity.Property(e => e.TriggerConfig).HasMaxLength(4000);
            entity.Property(e => e.Version).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ModifiedAtUtc);
            entity.Property(e => e.CreatedBy).IsRequired();
            entity.Property(e => e.ModifiedBy);
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.Property(e => e.RepositoryId).HasMaxLength(64);
            entity.Property(e => e.FormId).HasMaxLength(64);
            entity.HasIndex(e => new { e.TenantId, e.IsDeleted });
            entity.HasMany(w => w.Steps).WithOne().HasForeignKey(s => s.WorkflowId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(w => w.Sla).WithOne().HasForeignKey<WorkflowSla>(s => s.WorkflowId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowStep>(entity =>
        {
            entity.ToTable("WorkflowSteps");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkflowId).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.StepType).IsRequired();
            entity.Property(e => e.Order).IsRequired();
            entity.Property(e => e.Config).HasMaxLength(4000);
            entity.Property(e => e.IsRequired).IsRequired();
            entity.Property(e => e.AssignedToUserId);
            entity.Property(e => e.AssignedToRole).HasMaxLength(64);
            entity.Property(e => e.ApprovedNextStepId);
            entity.Property(e => e.RejectedNextStepId);
            entity.Property(e => e.ApprovalPolicy).IsRequired();
            entity.Property(e => e.ApproversJson).HasMaxLength(4000);
            entity.Property(e => e.ActivityId).HasMaxLength(128);
            entity.Property(e => e.StageType).HasMaxLength(64);
            entity.Property(e => e.ActionsJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.HasIndex(e => new { e.WorkflowId, e.Order });
        });

        modelBuilder.Entity<WorkflowInstance>(entity =>
        {
            entity.ToTable("WorkflowInstances");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.WorkflowId).IsRequired();
            entity.Property(e => e.WorkflowName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.WorkflowVersion).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CurrentStepInstanceId);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.StartedAtUtc);
            entity.Property(e => e.CompletedAtUtc);
            entity.Property(e => e.StartedBy).IsRequired();
            entity.Property(e => e.Context).HasMaxLength(4000);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(128);
            entity.Property(e => e.CustomerName).HasMaxLength(256);
            entity.Property(e => e.CustomerEmail).HasMaxLength(256);
            entity.Property(e => e.CustomerPhone).HasMaxLength(64);
            entity.Property(e => e.Department).HasMaxLength(128);
            entity.Property(e => e.Category).HasMaxLength(128);
            entity.Property(e => e.Priority).IsRequired();
            entity.Property(e => e.Tags).HasMaxLength(1000);
            entity.Property(e => e.CustomFieldsJson).HasMaxLength(4000);
            entity.Property(e => e.AssignedToUserId);
            entity.Property(e => e.AssignedToGroupId);
            entity.Property(e => e.LastActivityAtUtc);
            entity.Property(e => e.ViewCount).IsRequired();
            entity.Property(e => e.IsArchived).IsRequired();
            entity.Property(e => e.ArchivedAtUtc);
            entity.Property(e => e.SourceType).HasMaxLength(64);
            entity.Property(e => e.SourceId).HasMaxLength(256);
            entity.HasIndex(e => new { e.TenantId, e.WorkflowId });
            entity.HasIndex(e => new { e.TenantId, e.Status, e.IsArchived });
            entity.HasIndex(e => e.ReferenceNumber);
            entity.HasIndex(e => e.CustomerEmail);
            entity.HasMany(i => i.StepInstances).WithOne().HasForeignKey(s => s.WorkflowInstanceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(i => i.Comments).WithOne().HasForeignKey("WorkflowInstanceId").OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(i => i.Attachments).WithOne().HasForeignKey("WorkflowInstanceId").OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(i => i.Sla).WithOne().HasForeignKey<WorkflowInstanceSla>(s => s.WorkflowInstanceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowStepInstance>(entity =>
        {
            entity.ToTable("WorkflowStepInstances");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkflowInstanceId).IsRequired();
            entity.Property(e => e.WorkflowStepId).IsRequired();
            entity.Property(e => e.StepName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.StepType).IsRequired();
            entity.Property(e => e.Order).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.AssignedToUserId);
            entity.Property(e => e.AssignedToRole).HasMaxLength(64);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.StartedAtUtc);
            entity.Property(e => e.CompletedAtUtc);
            entity.Property(e => e.CompletedBy);
            entity.Property(e => e.Result).HasMaxLength(4000);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(e => new { e.WorkflowInstanceId, e.Order });
        });

        modelBuilder.Entity<WorkflowApproval>(entity =>
        {
            entity.ToTable("WorkflowApprovals");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.WorkflowInstanceId).IsRequired();
            entity.Property(e => e.StepInstanceId).IsRequired();
            entity.Property(e => e.RequestedBy).IsRequired();
            entity.Property(e => e.AssignedToUserId);
            entity.Property(e => e.AssignedToRole).HasMaxLength(64);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.RespondedAtUtc);
            entity.Property(e => e.RespondedBy);
            entity.Property(e => e.Comments).HasMaxLength(2000);
            entity.HasIndex(e => new { e.TenantId, e.AssignedToUserId, e.Status });
        });

        modelBuilder.Entity<WorkflowSla>(entity =>
        {
            entity.ToTable("WorkflowSlas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.WorkflowId).IsRequired();
            entity.Property(e => e.Priority).IsRequired();
            entity.Property(e => e.ResponseTimeMinutes).IsRequired();
            entity.Property(e => e.ResolutionTimeMinutes).IsRequired();
            entity.Property(e => e.EscalationTimeMinutes);
            entity.Property(e => e.EscalateToUserId);
            entity.Property(e => e.EscalateToRole).HasMaxLength(64);
            entity.Property(e => e.SendNotificationOnBreach).IsRequired();
            entity.Property(e => e.NotificationEmails).HasMaxLength(1000);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ModifiedAtUtc);
            entity.HasIndex(e => e.WorkflowId).IsUnique();
        });

        modelBuilder.Entity<WorkflowInstanceSla>(entity =>
        {
            entity.ToTable("WorkflowInstanceSlas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkflowInstanceId).IsRequired();
            entity.Property(e => e.Priority).IsRequired();
            entity.Property(e => e.ResponseDeadline).IsRequired();
            entity.Property(e => e.ResolutionDeadline).IsRequired();
            entity.Property(e => e.EscalationDeadline);
            entity.Property(e => e.ResponseAchievedAt);
            entity.Property(e => e.ResolutionAchievedAt);
            entity.Property(e => e.ResponseStatus).IsRequired();
            entity.Property(e => e.ResolutionStatus).IsRequired();
            entity.Property(e => e.IsEscalated).IsRequired();
            entity.Property(e => e.EscalatedAt);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.HasIndex(e => e.WorkflowInstanceId).IsUnique();
            entity.HasIndex(e => new { e.ResponseStatus, e.ResolutionStatus });
        });

        modelBuilder.Entity<WorkflowComment>(entity =>
        {
            entity.ToTable("WorkflowComments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.WorkflowInstanceId).IsRequired();
            entity.Property(e => e.StepInstanceId);
            entity.Property(e => e.Comments).HasMaxLength(4000).IsRequired();
            entity.Property(e => e.ExternalCommentsBy).HasMaxLength(256);
            entity.Property(e => e.ShowTo).IsRequired();
            entity.Property(e => e.EmbedJson).HasMaxLength(4000);
            entity.Property(e => e.EmbedStatus).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ModifiedAtUtc);
            entity.Property(e => e.CreatedBy).IsRequired();
            entity.Property(e => e.ModifiedBy);
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.WorkflowInstanceId, e.IsDeleted });
        });

        modelBuilder.Entity<WorkflowAttachment>(entity =>
        {
            entity.ToTable("WorkflowAttachments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.WorkflowInstanceId).IsRequired();
            entity.Property(e => e.StepInstanceId);
            entity.Property(e => e.RepositoryId);
            entity.Property(e => e.ItemId);
            entity.Property(e => e.FormJsonId).HasMaxLength(128);
            entity.Property(e => e.FileName).HasMaxLength(512);
            entity.Property(e => e.FilePath).HasMaxLength(1024);
            entity.Property(e => e.FileSize);
            entity.Property(e => e.ContentType).HasMaxLength(128);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ModifiedAtUtc);
            entity.Property(e => e.CreatedBy).IsRequired();
            entity.Property(e => e.ModifiedBy);
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.WorkflowInstanceId, e.IsDeleted });
        });

        modelBuilder.Entity<WorkflowForm>(entity =>
        {
            entity.ToTable("WorkflowForms");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.WorkflowInstanceId).IsRequired();
            entity.Property(e => e.StepInstanceId);
            entity.Property(e => e.WFormId).IsRequired();
            entity.Property(e => e.FormEntryId).IsRequired();
            entity.Property(e => e.FormData).HasMaxLength(4000);
            entity.Property(e => e.HasFormPdf).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ModifiedAtUtc);
            entity.Property(e => e.CreatedBy).IsRequired();
            entity.Property(e => e.ModifiedBy);
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.WorkflowInstanceId, e.IsDeleted });
        });

        modelBuilder.Entity<WorkflowTask>(entity =>
        {
            entity.ToTable("WorkflowTasks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.WorkflowInstanceId).IsRequired();
            entity.Property(e => e.StepInstanceId);
            entity.Property(e => e.WFormId).IsRequired();
            entity.Property(e => e.FormEntryId).IsRequired();
            entity.Property(e => e.TaskName).HasMaxLength(256);
            entity.Property(e => e.TaskDescription).HasMaxLength(2000);
            entity.Property(e => e.AssignedToUserId);
            entity.Property(e => e.DueDate);
            entity.Property(e => e.IsCompleted).IsRequired();
            entity.Property(e => e.CompletedAt);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ModifiedAtUtc);
            entity.Property(e => e.CreatedBy).IsRequired();
            entity.Property(e => e.ModifiedBy);
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.WorkflowInstanceId, e.IsDeleted });
            entity.HasIndex(e => new { e.TenantId, e.AssignedToUserId, e.IsCompleted });
        });

        modelBuilder.Entity<WorkflowSignature>(entity =>
        {
            entity.ToTable("WorkflowSignatures");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.WorkflowInstanceId).IsRequired();
            entity.Property(e => e.StepInstanceId);
            entity.Property(e => e.FileName).HasMaxLength(512).IsRequired();
            entity.Property(e => e.FilePath).HasMaxLength(1024);
            entity.Property(e => e.SignedBy).IsRequired();
            entity.Property(e => e.SignedAtUtc).IsRequired();
            entity.Property(e => e.SignatureData).HasMaxLength(4000);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ModifiedAtUtc);
            entity.Property(e => e.CreatedBy).IsRequired();
            entity.Property(e => e.ModifiedBy);
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.WorkflowInstanceId, e.IsDeleted });
        });

        modelBuilder.Entity<WorkflowDocument>(entity =>
        {
            entity.ToTable("WorkflowDocuments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.WorkflowId).IsRequired();
            entity.Property(e => e.WorkflowInstanceId);
            entity.Property(e => e.FileName).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Type).HasMaxLength(64);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.IsMandatory).IsRequired();
            entity.Property(e => e.FilePath).HasMaxLength(1024);
            entity.Property(e => e.UploadedAt);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ModifiedAtUtc);
            entity.Property(e => e.CreatedBy).IsRequired();
            entity.Property(e => e.ModifiedBy);
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.WorkflowId, e.IsDeleted });
            entity.HasIndex(e => new { e.TenantId, e.WorkflowInstanceId, e.IsDeleted });
        });

        modelBuilder.Entity<WorkflowEmail>(entity =>
        {
            entity.ToTable("WorkflowEmails");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.WorkflowInstanceId).IsRequired();
            entity.Property(e => e.StepInstanceId);
            entity.Property(e => e.EmailType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.EzMailId);
            entity.Property(e => e.MsgFileName).HasMaxLength(512);
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(512);
            entity.Property(e => e.Body).HasMaxLength(4000);
            entity.Property(e => e.AttachmentCount).IsRequired();
            entity.Property(e => e.AttachmentJson).HasMaxLength(4000);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ModifiedAtUtc);
            entity.Property(e => e.CreatedBy).IsRequired();
            entity.Property(e => e.ModifiedBy);
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.WorkflowInstanceId, e.IsDeleted });
        });

        modelBuilder.Entity<WorkflowAiValidation>(entity =>
        {
            entity.ToTable("WorkflowAiValidations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.WorkflowInstanceId).IsRequired();
            entity.Property(e => e.StepInstanceId);
            entity.Property(e => e.Type).HasMaxLength(64).IsRequired();
            entity.Property(e => e.AgentResponse).HasMaxLength(4000);
            entity.Property(e => e.FieldName).HasMaxLength(256);
            entity.Property(e => e.FormValue).HasMaxLength(1000);
            entity.Property(e => e.OcrValue).HasMaxLength(1000);
            entity.Property(e => e.ValidationStatus).HasMaxLength(64);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ModifiedAtUtc);
            entity.Property(e => e.CreatedBy).IsRequired();
            entity.Property(e => e.ModifiedBy);
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.WorkflowInstanceId, e.IsDeleted });
        });

        modelBuilder.Entity<WorkflowPdfAnnotation>(entity =>
        {
            entity.ToTable("WorkflowPdfAnnotations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.WorkflowInstanceId).IsRequired();
            entity.Property(e => e.StepInstanceId);
            entity.Property(e => e.RepositoryId);
            entity.Property(e => e.ItemId);
            entity.Property(e => e.AnnotationStatus).IsRequired();
            entity.Property(e => e.SettingsJson).HasMaxLength(4000);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.ModifiedAtUtc);
            entity.Property(e => e.CreatedBy).IsRequired();
            entity.Property(e => e.ModifiedBy);
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.WorkflowInstanceId, e.IsDeleted });
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId == null)
            throw new InvalidOperationException("Tenant context is required.");

        return base.SaveChangesAsync(cancellationToken);
    }
}
