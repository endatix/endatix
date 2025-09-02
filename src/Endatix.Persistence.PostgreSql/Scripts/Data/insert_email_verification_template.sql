-- Delete existing email verification template if it exists
DELETE FROM public."EmailTemplates" WHERE "Id" = 1;

-- Insert the email verification template
INSERT INTO public."EmailTemplates" ("Id", "Name", "Subject", "HtmlContent", "PlainTextContent", "FromAddress", "CreatedAt", "ModifiedAt", "IsDeleted")
VALUES (
    1,
    'email-verification',
    'Verify your email to activate your Endatix account',
    '<html>
<head>
    <meta charset="utf-8">
    <title>Verify your email address</title>
</head>
<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">
        <h2 style="color: #2c3e50;">Verify your email address</h2>
        <p>Thank you for creating your Endatix account.</p>
        
        <p>To get started, please verify your email address by clicking the button below:</p>
        
        <div style="text-align: center; margin: 30px 0;">
            <a href="{{hubUrl}}/verify-email?token={{verificationToken}}" 
               style="background-color: #0066ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;">
                Verify email address
            </a>
        </div>

        <p>Or copy and paste this link into your browser:</p>
        <p style="word-break: break-all; color: #7f8c8d;">{{hubUrl}}/verify-email?token={{verificationToken}}</p>
        
        <p>This verification link will expire in 24 hours and can only be used once.</p>
        
        <p>If you didn’t create an Endatix account, you can safely ignore this email.</p>
        
        <p>– The Endatix Team</p>

        <hr style="margin: 30px 0; border: none; border-top: 1px solid #ecf0f1;">
        <p style="font-size: 12px; color: #7f8c8d;">
            This is an automated message, please do not reply to this email.
        </p>
    </div>
</body>
</html>',
    'Verify your email address

Thank you for creating your Endatix account.

To get started, please verify your email address by clicking the link below:

{{hubUrl}}/verify-email?token={{verificationToken}}

This verification link will expire in 24 hours and can only be used once.

If you didn’t create an Endatix account, you can safely ignore this email.

– The Endatix Team

---

This is an automated message. Please do not reply to this email.',
    'noreply@endatix.com',
    NOW(),
    NOW(),
    FALSE
);