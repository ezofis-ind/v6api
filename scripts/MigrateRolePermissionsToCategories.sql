-- Collapse legacy category.action permission keys to category-only keys per role.
-- Safe to run multiple times (idempotent).
-- Run against each tenant database that has users.RolePermissions rows.

IF OBJECT_ID(N'users.RolePermissions', N'U') IS NULL
BEGIN
    PRINT 'users.RolePermissions does not exist — skipping.';
    RETURN;
END
GO

;WITH Collapsed AS (
    SELECT
        rp.[RoleId],
        rp.[TenantId],
        CASE
            WHEN CHARINDEX('.', rp.[PermissionKey]) > 0
                THEN LEFT(rp.[PermissionKey], CHARINDEX('.', rp.[PermissionKey]) - 1)
            ELSE rp.[PermissionKey]
        END AS [CategoryKey]
    FROM [users].[RolePermissions] rp
),
DistinctCategories AS (
    SELECT DISTINCT [RoleId], [TenantId], [CategoryKey]
    FROM Collapsed
    WHERE [CategoryKey] IS NOT NULL AND LTRIM(RTRIM([CategoryKey])) <> N''
)
INSERT INTO [users].[RolePermissions] ([RoleId], [TenantId], [PermissionKey])
SELECT dc.[RoleId], dc.[TenantId], dc.[CategoryKey]
FROM DistinctCategories dc
WHERE NOT EXISTS (
    SELECT 1
    FROM [users].[RolePermissions] existing
    WHERE existing.[RoleId] = dc.[RoleId]
      AND existing.[TenantId] = dc.[TenantId]
      AND existing.[PermissionKey] = dc.[CategoryKey]
);
GO

DELETE rp
FROM [users].[RolePermissions] rp
WHERE CHARINDEX('.', rp.[PermissionKey]) > 0;
GO

PRINT 'Role permissions migrated to category-only keys.';
GO
