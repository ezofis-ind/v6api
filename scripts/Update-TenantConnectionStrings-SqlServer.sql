-- Run against catalog database (ezofis_catalog_new).
-- Tenant rows store their own ConnectionString (server name from signup time).
-- After moving SQL Server or changing appsettings, update stored tenant connection strings.

-- Example: old instance -> new instance (edit names to match your environment)
UPDATE catalog.Tenants
SET ConnectionString = REPLACE(ConnectionString, N'EZOFISNEW\SQLEXPRESS01', N'EZ001\SQLEXPRESS')
WHERE ConnectionString LIKE N'%EZOFISNEW\SQLEXPRESS01%';

-- Verify
SELECT Id, Name, LEFT(ConnectionString, 120) AS ConnectionStringPreview
FROM catalog.Tenants;
