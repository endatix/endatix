-- Insert the forgot password template
INSERT INTO public."EmailTemplates" ("Id", "Name", "Subject", "HtmlContent", "PlainTextContent", "FromAddress", "CreatedAt", "ModifiedAt", "IsDeleted")
VALUES (
    2,
    'forgot-password',
    'Reset your password',
    '<html>
<head>
    <meta charset="utf-8">
    <title>Your Endatix password reset link</title>
</head>
<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">
        <h2 style="color: #2c3e50;">Your password reset link</h2>
        <p>We received a request to reset the password for your Endatix account.</p>
        
        <p>To reset your password, please click the button below:</p>
        
        <div style="text-align: center; margin: 30px 0;">
            <a href="{{hubUrl}}/reset-password?{{resetCodeQuery}}" 
               style="background-color: #0066ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;">
                Reset password
            </a>
        </div>

        <p>Or copy and paste this link into your browser:</p>
        <p style="word-break: break-all; color: #7f8c8d;">{{hubUrl}}/reset-password?{{resetCodeQuery}}</p>
        
        <p>This link will expire in 2 hours and can only be used once.</p>
        
        <p>If you didn’t request a password reset, you can safely ignore this email.</p>
        
        <p>– The Endatix Team</p>

        <hr style="margin: 30px 0; border: none; border-top: 1px solid #ecf0f1;">
        <p style="font-size: 12px; color: #7f8c8d;">
            This is an automated message, please do not reply to this email.
        </p>
    </div>
</body>
</html>',
    'Your Endatix password reset link

We received a request to reset the password for your Endatix account.

To reset your password, please copy and paste the link below into your browser:

{{hubUrl}}/reset-password?{{resetCodeQuery}}

This link will expire in 2 hours and can only be used once.

If you didn’t request a password reset, you can safely ignore this email.

– The Endatix Team

---

This is an automated message. Please do not reply to this email.',
    'noreply@endatix.com',
    NOW(),
    NOW(),
    FALSE
);