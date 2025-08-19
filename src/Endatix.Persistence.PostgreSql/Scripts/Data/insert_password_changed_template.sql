-- Insert the password changed template
INSERT INTO
    public."EmailTemplates" (
        "Id",
        "Name",
        "Subject",
        "HtmlContent",
        "PlainTextContent",
        "FromAddress",
        "CreatedAt",
        "ModifiedAt",
        "IsDeleted"
    )
VALUES
    (
        3,
        'password-changed',
        'Your Endatix password has been updated',
        '<html>
<head>
    <meta charset="utf-8">
    <title>Your Endatix password has been updated</title>
</head>
<body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
    <div style="max-width: 600px; margin: 0 auto; padding: 20px;">
        <h2 style="color: #2c3e50;">Your Endatix password has been updated</h2>
        <p>We wanted to let you know that the password on your account was just changed.</p>
        
        <ul>
            <li>If you made this change, no further action is needed.</li>
            <li>If you did not make this change, please contact our support team immediately at <a href="mailto:support@endatix.com">support@endatix.com</a>.</li>
        </ul>

        <p>– The Endatix Team</p>

        <hr style="margin: 30px 0; border: none; border-top: 1px solid #ecf0f1;">
        <p style="font-size: 12px; color: #7f8c8d;">
            This is an automated message, please do not reply to this email.
        </p>
    </div>
</body>
</html>',
        'Your Endatix password has been updated

We wanted to let you know that the password on your account was just changed.

If you made this change, no further action is needed

If you did not make this change, please contact our support team immediately at support@endatix.com.

– The Endatix Team

---

This is an automated message. Please do not reply to this email.',
        'noreply@endatix.com',
        NOW (),
        NOW (),
        FALSE
    );