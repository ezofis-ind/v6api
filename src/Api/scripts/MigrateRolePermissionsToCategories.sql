-- Migrate users.RolePermissions from category.action keys to category-only keys.
-- Run against each tenant database that has custom roles.
-- Safe to re-run: collapses dotted keys and removes duplicates.

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- Insert distinct category-only keys derived from legacy dotted (or already-category) values.
INSERT INTO users.RolePermissions (TenantId, RoleId, PermissionKey)
SELECT DISTINCT
    rp.TenantId,
    rp.RoleId,
    CASE
        WHEN CHARINDEX('.', rp.PermissionKey) > 0
            THEN LOWER(LEFT(rp.PermissionKey, CHARINDEX('.', rp.PermissionKey) - 1))
        ELSE LOWER(rp.PermissionKey)
    END AS CategoryKey
FROM users.RolePermissions rp
WHERE NOT EXISTS (
    SELECT 1
    FROM users.RolePermissions existing
    WHERE existing.TenantId = rp.TenantId
      AND existing.RoleId = rp.RoleId
      AND existing.PermissionKey = CASE
            WHEN CHARINDEX('.', rp.PermissionKey) > 0
                THEN LOWER(LEFT(rp.PermissionKey, CHARINDEX('.', rp.PermissionKey) - 1))
            ELSE LOWER(rp.PermissionKey)
        END
);

-- Remove legacy dotted keys and any non-canonical casing after the insert above.
DELETE FROM users.RolePermissions
WHERE CHARINDEX('.', PermissionKey) > 0
   OR PermissionKey <> LOWER(PermissionKey);

COMMIT TRANSACTION;
