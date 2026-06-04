-- Seed email templates by stable template name.
-- Template bodies are assembled from single-line literals to keep scanners from
-- flagging raw newline characters inside SQL string literals.
DROP TABLE IF EXISTS pg_temp.temp_email_template_seed;

CREATE TEMP TABLE temp_email_template_seed
(
    id BIGINT NOT NULL,
    name TEXT NOT NULL,
    subject TEXT NOT NULL,
    html_content TEXT NOT NULL,
    plain_text_content TEXT NOT NULL,
    from_address TEXT NOT NULL
);

INSERT INTO temp_email_template_seed (id, name, subject, html_content, plain_text_content, from_address)
VALUES
(
    1,
    'email-verification',
    'Verify your email to activate your Endatix account',
    array_to_string(ARRAY[
        '<html>',
        '<head>',
        '    <meta charset="utf-8">',
        '    <title>Verify your email address</title>',
        '</head>',
        '<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">',
        '    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">',
        '        <h2 style="color: #2c3e50;">Verify your email address</h2>',
        '        <p>Thank you for creating your Endatix account.</p>',
        '',
        '        <p>To get started, please verify your email address by clicking the button below:</p>',
        '',
        '        <div style="text-align: center; margin: 30px 0;">',
        '            <a href="{{verificationUrl}}"',
        '               style="background-color: #0066ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;">',
        '                Verify email address',
        '            </a>',
        '        </div>',
        '',
        '        <p>Or copy and paste this link into your browser:</p>',
        '        <p style="word-break: break-all; color: #7f8c8d;">{{verificationUrl}}</p>',
        '',
        '        <p>This verification link will expire in 24 hours and can only be used once.</p>',
        '',
        '        <p>If you didn’t create an Endatix account, you can safely ignore this email.</p>',
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
        'Verify your email address',
        '',
        'Thank you for creating your Endatix account.',
        '',
        'To get started, please verify your email address by clicking the link below:',
        '',
        '{{verificationUrl}}',
        '',
        'This verification link will expire in 24 hours and can only be used once.',
        '',
        'If you didn’t create an Endatix account, you can safely ignore this email.',
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
    4,
    'user-invitation',
    'Accept your Endatix invitation',
    array_to_string(ARRAY[
        '<html>',
        '<head>',
        '    <meta charset="utf-8">',
        '    <title>Accept your Endatix invitation</title>',
        '</head>',
        '<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">',
        '    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">',
        '        <h2 style="color: #2c3e50;">{{headline}}</h2>',
        '        <p>{{bodyText}}</p>',
        '',
        '        <div style="text-align: center; margin: 30px 0;">',
        '            <a href="{{activationUrl}}"',
        '               style="background-color: #0066ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;">',
        '                {{actionText}}',
        '            </a>',
        '        </div>',
        '',
        '        <p>Or copy and paste this link into your browser:</p>',
        '        <p style="word-break: break-all; color: #7f8c8d;">{{activationUrl}}</p>',
        '',
        '        <p>This invitation link will expire and can only be used once.</p>',
        '        <p>If you were not expecting this invitation, you can safely ignore this email.</p>',
        '',
        '        <p>– The Endatix Team</p>',
        '    </div>',
        '</body>',
        '</html>'
    ], CHR(10)),
    array_to_string(ARRAY[
        'Accept your Endatix invitation',
        '',
        '{{bodyText}}',
        '',
        '{{activationUrl}}',
        '',
        'This invitation link will expire and can only be used once.',
        '',
        'If you were not expecting this invitation, you can safely ignore this email.',
        '',
        '– The Endatix Team'
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
FROM temp_email_template_seed AS seed
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
FROM temp_email_template_seed AS seed
WHERE NOT EXISTS (
    SELECT 1 FROM public."EmailTemplates" AS target WHERE target."Name" = seed.name
);

DROP TABLE temp_email_template_seed;