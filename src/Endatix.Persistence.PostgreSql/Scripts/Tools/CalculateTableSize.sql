-- ============================================================================
-- CALCULATE TABLE SIZE SCRIPT
-- ============================================================================
-- This script calculates the size and number of submissions and submission versions,
-- grouped by FormId. It also provides the total size for both tables.
-- ============================================================================

-- 1. Submissions Statistics by FormId
SELECT 
    s."FormId",
    f."Name" AS "FormName",
    COUNT(*) AS "SubmissionCount",
    pg_size_pretty(SUM(pg_column_size(s))) AS "EstimatedDataSize",
    SUM(pg_column_size(s)) AS "DataSizeInBytes"
FROM "Submissions" s
LEFT JOIN "Forms" f ON s."FormId" = f."Id"
GROUP BY s."FormId", f."Name"
ORDER BY "SubmissionCount" DESC;

-- 2. SubmissionVersions Statistics by FormId
SELECT 
    s."FormId",
    f."Name" AS "FormName",
    COUNT(sv.*) AS "VersionCount",
    pg_size_pretty(SUM(pg_column_size(sv))) AS "EstimatedDataSize",
    SUM(pg_column_size(sv)) AS "DataSizeInBytes"
FROM "SubmissionVersions" sv
JOIN "Submissions" s ON sv."SubmissionId" = s."Id"
LEFT JOIN "Forms" f ON s."FormId" = f."Id"
GROUP BY s."FormId", f."Name"
ORDER BY "VersionCount" DESC;

-- 3. Total Table Sizes
SELECT 
    relname AS "TableName",
    pg_size_pretty(pg_total_relation_size(relid)) AS "TotalSizeIncludingIndexes",
    pg_size_pretty(pg_relation_size(relid)) AS "TableSize",
    pg_size_pretty(pg_total_relation_size(relid) - pg_relation_size(relid)) AS "IndexSize"
FROM pg_catalog.pg_statio_user_tables
WHERE relname IN ('Submissions', 'SubmissionVersions')
ORDER BY pg_total_relation_size(relid) DESC;
