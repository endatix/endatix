# Email Verification

This document describes the email verification functionality implemented in Endatix.

## Overview

When a new user registers, they are created with `EmailConfirmed = false` and a verification token is generated. The user must verify their email address by clicking a link or using the verification token before they can log in.

**Architecture Note**: The `EmailVerificationToken` entity is located in the `AppIdentityDbContext` (identity schema) to maintain proper foreign key relationships with the `AppUser` entity and ensure data integrity through cascade deletes when users are removed.

## Components

### 1. EmailVerificationToken Entity

Located in `src/Endatix.Core/Entities/Identity/EmailVerificationToken.cs`

- Represents a verification token for a user
- Contains token value, expiry time, and usage status
- Tokens expire after 24 hours by default
- Tokens can only be used once
- **Note**: Inherits from `BaseEntity` (not `TenantEntity`) since tokens are created before users are assigned to tenants

### 2. IEmailVerificationService Interface

Located in `src/Endatix.Core/Abstractions/IEmailVerificationService.cs`

Defines the contract for email verification operations:
- `CreateVerificationTokenAsync` - Creates a new verification token for a user
- `VerifyEmailAsync` - Verifies a user's email and invalidates the token

### 3. EmailVerificationService Implementation

Located in `src/Endatix.Infrastructure/Identity/EmailVerification/EmailVerificationService.cs`

Implements the email verification logic:
- Generates secure 256-bit tokens
- Manages token lifecycle (creation, expiration)
- Updates user verification status
- Handles token cleanup

### 4. SendVerificationEmail Use Case

Located in `src/Endatix.Core/UseCases/Identity/SendVerificationEmail/`

Handles the business logic for sending verification emails:
- Finds users by email address
- Validates user verification status
- Creates new verification tokens
- Implements security measures to prevent email enumeration

### 5. API Endpoints

Located in `src/Endatix.Api/Endpoints/Auth/`

#### Verify Email Endpoint
- **POST** `/api/auth/verify-email`
- Accepts a verification token
- Returns the user ID on success
- Returns appropriate HTTP status codes:
  - `200 OK` - Email verified successfully (returns user ID)
  - `400 Bad Request` - Invalid or expired token (with problem details)
  - `404 Not Found` - Token not found
- Anonymous access (no authentication required)

#### Send Verification Email Endpoint
- **POST** `/api/auth/send-verification-email`
- Accepts an email address
- Creates a new verification token and sends it via email (when email sending is implemented)
- Returns appropriate HTTP status codes:
  - `200 OK` - Verification email sent successfully (or would be sent if email sending is implemented)
  - `400 Bad Request` - Invalid email address (with problem details)
- Anonymous access (no authentication required)
- **Security Note**: Always returns success to prevent email enumeration attacks

## Configuration

Email verification settings can be configured in `appsettings.json`:

```json
{
  "Endatix": {
    "EmailVerification": {
      "TokenExpiryInHours": 24
    }
  }
}
```

## Database Schema

The `EmailVerificationTokens` table is located in the `identity` schema and contains:
- `Id` - Primary key
- `UserId` - User identifier (foreign key to `AspNetUsers.Id`)
- `Token` - Verification token value (64 characters)
- `ExpiresAt` - Token expiration timestamp
- `IsUsed` - Whether the token has been used
- `CreatedAt` - Token creation timestamp
- `ModifiedAt` - Last modification timestamp
- `DeletedAt` - Soft delete timestamp (nullable)
- `IsDeleted` - Soft delete flag

**Note**: The table is located in the `AppIdentityDbContext` (identity schema) rather than the main `AppDbContext` to maintain proper foreign key relationships with the `AppUser` entity and ensure data integrity through cascade deletes.

## Usage Flow

1. **User Registration**
   - User registers with email and password
   - User is created with `EmailConfirmed = false`
   - Verification token is generated and stored
   - Token should be sent to user via email (not implemented in this version)

2. **Email Verification**
   - User receives verification token (via email)
   - User calls `POST /api/auth/verify-email` with the token
   - System validates token (not expired, not used, user exists, user not verified)
   - If valid, user's `EmailConfirmed` is set to `true` and token is marked as used
   - Returns the user ID on success
   - User can now log in

3. **Send Verification Email** (if resending is needed)
   - User requests a verification email by calling `POST /api/auth/send-verification-email` with their email address
   - System validates email format and checks if user exists and is not verified
   - If valid, a new verification token is generated and the old one is invalidated
   - New token is sent via email (when email sending is implemented)
   - Always returns success to prevent email enumeration attacks

4. **Login**
   - User attempts to log in
   - System checks `EmailConfirmed` status
   - Only verified users can log in

## API Response Format

### Success Response (200 OK)
```json
{
  "value": "12345"
}
```
Where `12345` is the user ID as a string.

### Error Response (400 Bad Request)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "There was a problem with your request.",
  "status": 400,
  "detail": "User is already verified"
}
```

### Error Response (404 Not Found)
```http
HTTP/1.1 404 Not Found
```

## Security Considerations

- Tokens are cryptographically secure (256-bit random)
- Tokens expire after 24 hours
- Tokens can only be used once
- Tokens are invalidated when user is already verified
- No sensitive information is exposed in tokens
- Tokens are not tenant-scoped since they're created before tenant assignment

## Testing

The implementation includes comprehensive tests:
- Entity tests: `tests/Endatix.Core.Tests/Entities/Identity/EmailVerificationTokenTests.cs`
- Service tests: `tests/Endatix.Infrastructure.Tests/Identity/EmailVerification/EmailVerificationServiceTests.cs`
- Use case tests: `tests/Endatix.Core.Tests/UseCases/Identity/SendVerificationEmail/SendVerificationEmailHandlerTests.cs`
- API tests: `tests/Endatix.Api.Tests/Endpoints/Auth/VerifyEmailTests.cs`
- Send API tests: `tests/Endatix.Api.Tests/Endpoints/Auth/SendVerificationEmailTests.cs`

## Future Enhancements

1. **Email Sending**: Implement actual email sending for verification tokens
2. **Rate Limiting**: Prevent abuse of verification endpoints
3. **Audit Logging**: Track verification attempts and failures
4. **Custom Expiry**: Allow different expiry times for different user types 