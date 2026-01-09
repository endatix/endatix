using System.Security.Cryptography;
using System.Text;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Submissions;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// Service for generating and validating stateless submission access tokens.
/// Uses HMAC-SHA256 signature based on Azure Blob Storage SAS pattern.
/// </summary>
internal class SubmissionAccessTokenService : ISubmissionAccessTokenService
{
    private readonly byte[] _signingKeyBytes;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SubmissionAccessTokenService(
        IOptions<SubmissionAccessTokenOptions> options,
        IDateTimeProvider dateTimeProvider)
    {
        Guard.Against.Null(options);
        Guard.Against.Null(dateTimeProvider);

        var signingKey = options.Value.AccessTokenSigningKey;
        Guard.Against.NullOrWhiteSpace(signingKey);
        if (signingKey.Length < 32)
        {
            throw new ArgumentException("Signing key must be at least 32 characters.");
        }

        _signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Result<SubmissionAccessTokenDto> GenerateAccessToken(long submissionId, int expiryMinutes, IEnumerable<string> permissions)
    {
        Guard.Against.NegativeOrZero(submissionId);
        Guard.Against.NegativeOrZero(expiryMinutes);
        Guard.Against.NullOrEmpty(permissions);

        foreach (var permission in permissions)
        {
            if (!SubmissionAccessTokenPermissions.IsValid(permission))
            {
                return Result.Invalid(SubmissionAccessTokenErrors.ValidationErrors.InvalidPermission(permission));
            }
        }

        var expiresAt = _dateTimeProvider.Now.AddMinutes(expiryMinutes);
        var expiryUnix = expiresAt.ToUnixTimeSeconds();

        var permissionsCode = SubmissionAccessTokenPermissions.EncodeNames(permissions);

        var stringToSign = BuildStringToSign(submissionId, expiryUnix, permissionsCode);
        var signature = ComputeSignature(stringToSign);

        var token = $"{submissionId}.{expiryUnix}.{permissionsCode}.{signature}";

        return Result.Success(new SubmissionAccessTokenDto(token, expiresAt.UtcDateTime, permissions));
    }

    /// <inheritdoc />
    public Result<SubmissionAccessTokenClaims> ValidateAccessToken(string token)
    {
        Guard.Against.NullOrWhiteSpace(token);

        var parts = token.Split('.');
        if (parts.Length != 4)
        {
            return Result.Invalid(SubmissionAccessTokenErrors.ValidationErrors.InvalidToken);
        }
        
        if (!long.TryParse(parts[0], out var submissionId) ||
            !long.TryParse(parts[1], out var expiryUnix))
        {
            return Result.Invalid(SubmissionAccessTokenErrors.ValidationErrors.InvalidToken);
        }

        var permissionsCode = parts[2];
        var providedSignature = parts[3];

        var stringToSign = BuildStringToSign(submissionId, expiryUnix, permissionsCode);
        var expectedSignature = ComputeSignature(stringToSign);

        // Constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedSignature),
            Encoding.UTF8.GetBytes(expectedSignature)))
        {
            return Result.Invalid(SubmissionAccessTokenErrors.ValidationErrors.InvalidToken);
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiryUnix);
        if (_dateTimeProvider.Now > expiresAt)
        {
            return Result.Invalid(SubmissionAccessTokenErrors.ValidationErrors.TokenExpired);
        }

        var permissions = SubmissionAccessTokenPermissions.DecodeToNames(permissionsCode);
        if (!permissions.Any())
        {
            return Result.Invalid(SubmissionAccessTokenErrors.ValidationErrors.InvalidToken);
        }

        return Result.Success(new SubmissionAccessTokenClaims(
            submissionId,
            permissions,
            expiresAt.UtcDateTime));
    }

    private string BuildStringToSign(long submissionId, long expiryUnix, string permissionsCode)
    {
        return string.Join("\n",
            submissionId,
            expiryUnix,
            permissionsCode);
    }

    private string ComputeSignature(string message)
    {
        var hashBytes = HMACSHA256.HashData(_signingKeyBytes, Encoding.UTF8.GetBytes(message));
        var signature = Convert.ToBase64String(hashBytes);

        // Make URL-safe
        signature = signature.Replace('+', '-').Replace('/', '_').TrimEnd('=');

        // Truncate to 16 chars for shorter URLs (~96 bits entropy)
        return signature[..16];
    }
}
