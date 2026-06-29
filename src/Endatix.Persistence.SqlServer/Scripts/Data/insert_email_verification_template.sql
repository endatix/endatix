-- Seed email templates by stable template name.
-- Template bodies are assembled from single-line literals to keep scanners from
-- flagging raw newline characters inside SQL string literals.
IF OBJECT_ID('tempdb..#EmailTemplateSeed') IS NOT NULL
BEGIN
    DROP TABLE #EmailTemplateSeed;
END;

CREATE TABLE #EmailTemplateSeed
(
    Id BIGINT NOT NULL,
    Name NVARCHAR(256) NOT NULL,
    Subject NVARCHAR(512) NOT NULL,
    HtmlContent NVARCHAR(MAX) NOT NULL,
    PlainTextContent NVARCHAR(MAX) NOT NULL,
    FromAddress NVARCHAR(256) NOT NULL
);

INSERT INTO #EmailTemplateSeed (Id, Name, Subject, HtmlContent, PlainTextContent, FromAddress)
VALUES
(
    1,
    N'email-verification',
    N'Verify your email to activate your Endatix account',
    CONCAT(
        N'<html>', NCHAR(10),
        N'<head>', NCHAR(10),
        N'    <meta charset="utf-8">', NCHAR(10),
        N'    <title>Verify your email address</title>', NCHAR(10),
        N'</head>', NCHAR(10),
        N'<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">', NCHAR(10),
        N'    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">', NCHAR(10),
        N'        <h2 style="color: #2c3e50;">Verify your email address</h2>', NCHAR(10),
        N'        <p>Thank you for creating your Endatix account.</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <p>To get started, please verify your email address by clicking the button below:</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <div style="text-align: center; margin: 30px 0;">', NCHAR(10),
        N'            <a href="{{verificationUrl}}"', NCHAR(10),
        N'               style="background-color: #0066ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;">', NCHAR(10),
        N'                Verify email address', NCHAR(10),
        N'            </a>', NCHAR(10),
        N'        </div>', NCHAR(10),
        N'', NCHAR(10),
        N'        <p>Or copy and paste this link into your browser:</p>', NCHAR(10),
        N'        <p style="word-break: break-all; color: #7f8c8d;">{{verificationUrl}}</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <p>This verification link will expire in 24 hours and can only be used once.</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <p>If you didn’t create an Endatix account, you can safely ignore this email.</p>', NCHAR(10),
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
        N'Verify your email address', NCHAR(10),
        N'', NCHAR(10),
        N'Thank you for creating your Endatix account.', NCHAR(10),
        N'', NCHAR(10),
        N'To get started, please verify your email address by clicking the link below:', NCHAR(10),
        N'', NCHAR(10),
        N'{{verificationUrl}}', NCHAR(10),
        N'', NCHAR(10),
        N'This verification link will expire in 24 hours and can only be used once.', NCHAR(10),
        N'', NCHAR(10),
        N'If you didn’t create an Endatix account, you can safely ignore this email.', NCHAR(10),
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
    4,
    N'user-invitation',
    N'Accept your Endatix invitation',
    CONCAT(
        N'<html>', NCHAR(10),
        N'<head>', NCHAR(10),
        N'    <meta charset="utf-8">', NCHAR(10),
        N'    <title>Accept your Endatix invitation</title>', NCHAR(10),
        N'</head>', NCHAR(10),
        N'<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">', NCHAR(10),
        N'    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">', NCHAR(10),
        N'        <h2 style="color: #2c3e50;">{{headline}}</h2>', NCHAR(10),
        N'        <p>{{bodyText}}</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <div style="text-align: center; margin: 30px 0;">', NCHAR(10),
        N'            <a href="{{activationUrl}}"', NCHAR(10),
        N'               style="background-color: #0066ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;">', NCHAR(10),
        N'                {{actionText}}', NCHAR(10),
        N'            </a>', NCHAR(10),
        N'        </div>', NCHAR(10),
        N'', NCHAR(10),
        N'        <p>Or copy and paste this link into your browser:</p>', NCHAR(10),
        N'        <p style="word-break: break-all; color: #7f8c8d;">{{activationUrl}}</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <p>This invitation link will expire and can only be used once.</p>', NCHAR(10),
        N'        <p>If you were not expecting this invitation, you can safely ignore this email.</p>', NCHAR(10),
        N'', NCHAR(10),
        N'        <p>– The Endatix Team</p>', NCHAR(10),
        N'    </div>', NCHAR(10),
        N'</body>', NCHAR(10),
        N'</html>'
    ),
    CONCAT(
        N'Accept your Endatix invitation', NCHAR(10),
        N'', NCHAR(10),
        N'{{bodyText}}', NCHAR(10),
        N'', NCHAR(10),
        N'{{activationUrl}}', NCHAR(10),
        N'', NCHAR(10),
        N'This invitation link will expire and can only be used once.', NCHAR(10),
        N'', NCHAR(10),
        N'If you were not expecting this invitation, you can safely ignore this email.', NCHAR(10),
        N'', NCHAR(10),
        N'– The Endatix Team'
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
INNER JOIN #EmailTemplateSeed AS seed ON target.Name = seed.Name;

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
FROM #EmailTemplateSeed AS seed
WHERE NOT EXISTS (
    SELECT 1 FROM EmailTemplates AS target WHERE target.Name = seed.Name
);

DROP TABLE #EmailTemplateSeed;
