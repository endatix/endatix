-- Seed tenant waitlist email templates by stable template name.
-- Template bodies are assembled from single-line literals to keep scanners from
-- flagging raw newline characters inside SQL string literals.
DROP TABLE IF EXISTS pg_temp.temp_tenant_signup_email_template_seed;

CREATE TEMP TABLE temp_tenant_signup_email_template_seed
(
    id BIGINT NOT NULL,
    name TEXT NOT NULL,
    subject TEXT NOT NULL,
    html_content TEXT NOT NULL,
    plain_text_content TEXT NOT NULL,
    from_address TEXT NOT NULL
);

INSERT INTO temp_tenant_signup_email_template_seed (id, name, subject, html_content, plain_text_content, from_address)
VALUES
(
    5,
    'tenant-signup-request',
    'New workspace request from {{requestEmail}}',
    array_to_string(ARRAY[
        '<html>',
        '<head>',
        '    <meta charset="utf-8">',
        '    <title>New workspace request</title>',
        '</head>',
        '<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">',
        '    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">',
        '        <h2 style="color: #2c3e50;">New workspace request</h2>',
        '        <p>Someone requested an Endatix workspace from the public signup waitlist.</p>',
        '',
        '        <p><strong>Email:</strong> {{requestEmail}}</p>',
        '        <p><strong>Company:</strong> {{companyName}}</p>',
        '',
        '        <div style="text-align: center; margin: 30px 0;">',
        '            <a href="{{inboxUrl}}"',
        '               style="background-color: #0066ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;">',
        '                Review signup requests',
        '            </a>',
        '        </div>',
        '',
        '        <p>Or copy and paste this link into your browser:</p>',
        '        <p style="word-break: break-all; color: #7f8c8d;">{{inboxUrl}}</p>',
        '',
        '        <p>– The Endatix Team</p>',
        '',
        '        <hr style="margin: 30px 0; border: none; border-top: 1px solid #ecf0f1;">',
        '        <p style="font-size: 12px; color: #7f8c8d;">',
        '            This is an automated message, please do not reply to this email.',
        '        </p>',
        '    </div>',
        '</body>',
        '</html>'
    ], CHR(10)),
    array_to_string(ARRAY[
        'New workspace request',
        '',
        'Someone requested an Endatix workspace from the public signup waitlist.',
        '',
        'Email: {{requestEmail}}',
        'Company: {{companyName}}',
        '',
        'Review signup requests:',
        '{{inboxUrl}}',
        '',
        '– The Endatix Team',
        '',
        '---',
        '',
        'This is an automated message. Please do not reply to this email.'
    ], CHR(10)),
    'noreply@endatix.com'
),
(
    6,
    'tenant-signup-approved',
    'Your Endatix workspace is ready',
    array_to_string(ARRAY[
        '<html>',
        '<head>',
        '    <meta charset="utf-8">',
        '    <title>Your workspace is ready</title>',
        '</head>',
        '<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">',
        '    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">',
        '        <h2 style="color: #2c3e50;">{{headline}}</h2>',
        '        <p>{{bodyText}}</p>',
        '',
        '        <div style="text-align: center; margin: 30px 0;">',
        '            <a href="{{signInUrl}}"',
        '               style="background-color: #0066ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;">',
        '                {{actionText}}',
        '            </a>',
        '        </div>',
        '',
        '        <p>Or copy and paste this link into your browser:</p>',
        '        <p style="word-break: break-all; color: #7f8c8d;">{{signInUrl}}</p>',
        '',
        '        <p>– The Endatix Team</p>',
        '',
        '        <hr style="margin: 30px 0; border: none; border-top: 1px solid #ecf0f1;">',
        '        <p style="font-size: 12px; color: #7f8c8d;">',
        '            This is an automated message, please do not reply to this email.',
        '        </p>',
        '    </div>',
        '</body>',
        '</html>'
    ], CHR(10)),
    array_to_string(ARRAY[
        '{{headline}}',
        '',
        '{{bodyText}}',
        '',
        '{{signInUrl}}',
        '',
        '– The Endatix Team',
        '',
        '---',
        '',
        'This is an automated message. Please do not reply to this email.'
    ], CHR(10)),
    'noreply@endatix.com'
);

UPDATE public."EmailTemplates" AS target
SET
    "Subject" = seed.subject,
    "HtmlContent" = seed.html_content,
    "PlainTextContent" = seed.plain_text_content,
    "FromAddress" = seed.from_address,
    "ModifiedAt" = NOW(),
    "IsDeleted" = FALSE
FROM temp_tenant_signup_email_template_seed AS seed
WHERE target."Name" = seed.name;

INSERT INTO public."EmailTemplates" ("Id", "Name", "Subject", "HtmlContent", "PlainTextContent", "FromAddress", "CreatedAt", "ModifiedAt", "IsDeleted")
SELECT
    seed.id,
    seed.name,
    seed.subject,
    seed.html_content,
    seed.plain_text_content,
    seed.from_address,
    NOW(),
    NOW(),
    FALSE
FROM temp_tenant_signup_email_template_seed AS seed
WHERE NOT EXISTS (
    SELECT 1 FROM public."EmailTemplates" AS target WHERE target."Name" = seed.name
);

DROP TABLE temp_tenant_signup_email_template_seed;
