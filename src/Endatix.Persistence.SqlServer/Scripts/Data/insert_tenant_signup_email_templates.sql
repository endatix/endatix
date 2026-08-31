-- Seed tenant waitlist email templates by stable template name.
-- Template bodies are assembled from single-line literals to keep scanners from
-- flagging raw newline characters inside SQL string literals.
IF OBJECT_ID('tempdb..#TenantSignupEmailTemplateSeed') IS NOT NULL
BEGIN
    DROP TABLE #TenantSignupEmailTemplateSeed;
END;

CREATE TABLE #TenantSignupEmailTemplateSeed
(
    Id BIGINT NOT NULL,
    Name NVARCHAR(256) NOT NULL,
    Subject NVARCHAR(512) NOT NULL,
    HtmlContent NVARCHAR(MAX) NOT NULL,
    PlainTextContent NVARCHAR(MAX) NOT NULL,
    FromAddress NVARCHAR(256) NOT NULL
);

INSERT INTO #TenantSignupEmailTemplateSeed (Id, Name, Subject, HtmlContent, PlainTextContent, FromAddress)
VALUES
(
    5,
    N'tenant-signup-request',
    N'New workspace request from {{requestEmail}}',
    CONCAT(
        N'<html>', NCHAR(10),
        N'<head>', NCHAR(10),
        N'    <meta charset="utf-8">', NCHAR(10),
        N'    <title>New workspace request</title>', NCHAR(10),
        N'</head>', NCHAR(10),
        N'<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">', NCHAR(10),
        N'    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">', NCHAR(10),
        N'        <h2 style="color: #2c3e50;">New workspace request</h2>', NCHAR(10),
        N'        <p>Someone requested an Endatix workspace from the public signup waitlist.</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <p><strong>Email:</strong> {{requestEmail}}</p>', NCHAR(10),
        N'        <p><strong>Company:</strong> {{companyName}}</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <div style="text-align: center; margin: 30px 0;">', NCHAR(10),
        N'            <a href="{{inboxUrl}}"', NCHAR(10),
        N'               style="background-color: #0066ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;">', NCHAR(10),
        N'                Review signup requests', NCHAR(10),
        N'            </a>', NCHAR(10),
        N'        </div>', NCHAR(10),
        N'', NCHAR(10),
        N'        <p>Or copy and paste this link into your browser:</p>', NCHAR(10),
        N'        <p style="word-break: break-all; color: #7f8c8d;">{{inboxUrl}}</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <p>– The Endatix Team</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <hr style="margin: 30px 0; border: none; border-top: 1px solid #ecf0f1;">', NCHAR(10),
        N'        <p style="font-size: 12px; color: #7f8c8d;">', NCHAR(10),
        N'            This is an automated message, please do not reply to this email.', NCHAR(10),
        N'        </p>', NCHAR(10),
        N'    </div>', NCHAR(10),
        N'</body>', NCHAR(10),
        N'</html>'
    ),
    CONCAT(
        N'New workspace request', NCHAR(10),
        N'', NCHAR(10),
        N'Someone requested an Endatix workspace from the public signup waitlist.', NCHAR(10),
        N'', NCHAR(10),
        N'Email: {{requestEmail}}', NCHAR(10),
        N'Company: {{companyName}}', NCHAR(10),
        N'', NCHAR(10),
        N'Review signup requests:', NCHAR(10),
        N'{{inboxUrl}}', NCHAR(10),
        N'', NCHAR(10),
        N'– The Endatix Team', NCHAR(10),
        N'', NCHAR(10),
        N'---', NCHAR(10),
        N'', NCHAR(10),
        N'This is an automated message. Please do not reply to this email.'
    ),
    N'noreply@endatix.com'
),
(
    6,
    N'tenant-signup-approved',
    N'Your Endatix workspace is ready',
    CONCAT(
        N'<html>', NCHAR(10),
        N'<head>', NCHAR(10),
        N'    <meta charset="utf-8">', NCHAR(10),
        N'    <title>Your workspace is ready</title>', NCHAR(10),
        N'</head>', NCHAR(10),
        N'<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">', NCHAR(10),
        N'    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">', NCHAR(10),
        N'        <h2 style="color: #2c3e50;">{{headline}}</h2>', NCHAR(10),
        N'        <p>{{bodyText}}</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <div style="text-align: center; margin: 30px 0;">', NCHAR(10),
        N'            <a href="{{signInUrl}}"', NCHAR(10),
        N'               style="background-color: #0066ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;">', NCHAR(10),
        N'                {{actionText}}', NCHAR(10),
        N'            </a>', NCHAR(10),
        N'        </div>', NCHAR(10),
        N'', NCHAR(10),
        N'        <p>Or copy and paste this link into your browser:</p>', NCHAR(10),
        N'        <p style="word-break: break-all; color: #7f8c8d;">{{signInUrl}}</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <p>– The Endatix Team</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <hr style="margin: 30px 0; border: none; border-top: 1px solid #ecf0f1;">', NCHAR(10),
        N'        <p style="font-size: 12px; color: #7f8c8d;">', NCHAR(10),
        N'            This is an automated message, please do not reply to this email.', NCHAR(10),
        N'        </p>', NCHAR(10),
        N'    </div>', NCHAR(10),
        N'</body>', NCHAR(10),
        N'</html>'
    ),
    CONCAT(
        N'{{headline}}', NCHAR(10),
        N'', NCHAR(10),
        N'{{bodyText}}', NCHAR(10),
        N'', NCHAR(10),
        N'{{signInUrl}}', NCHAR(10),
        N'', NCHAR(10),
        N'– The Endatix Team', NCHAR(10),
        N'', NCHAR(10),
        N'---', NCHAR(10),
        N'', NCHAR(10),
        N'This is an automated message. Please do not reply to this email.'
    ),
    N'noreply@endatix.com'
);

UPDATE target
SET
    Subject = seed.Subject,
    HtmlContent = seed.HtmlContent,
    PlainTextContent = seed.PlainTextContent,
    FromAddress = seed.FromAddress,
    ModifiedAt = GETUTCDATE(),
    IsDeleted = 0
FROM EmailTemplates AS target
INNER JOIN #TenantSignupEmailTemplateSeed AS seed ON target.Name = seed.Name;

INSERT INTO EmailTemplates (Id, Name, Subject, HtmlContent, PlainTextContent, FromAddress, CreatedAt, ModifiedAt, IsDeleted)
SELECT
    seed.Id,
    seed.Name,
    seed.Subject,
    seed.HtmlContent,
    seed.PlainTextContent,
    seed.FromAddress,
    GETUTCDATE(),
    GETUTCDATE(),
    0
FROM #TenantSignupEmailTemplateSeed AS seed
WHERE NOT EXISTS (
    SELECT 1 FROM EmailTemplates AS target WHERE target.Name = seed.Name
);

DROP TABLE #TenantSignupEmailTemplateSeed;
