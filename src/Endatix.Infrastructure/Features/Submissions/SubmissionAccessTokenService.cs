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
    private readonly ISubmissionAccessTokenGate _gate;

    public SubmissionAccessTokenService(
        IOptions<SubmissionAccessTokenOptions> options,
        IDateTimeProvider dateTimeProvider,
        ISubmissionAccessTokenGate gate)
    {
        Guard.Against.Null(options);
        Guard.Against.Null(dateTimeProvider);
        Guard.Against.Null(gate);

        var signingKey = options.Value.AccessTokenSigningKey;
        Guard.Against.NullOrWhiteSpace(signingKey);
        if (signingKey.Length < 32)
        {
            throw new ArgumentException("Signing key must be at least 32 characters.");
        }

        _signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);
        _dateTimeProvider = dateTimeProvider;
        _gate = gate;
    }

    /// <inheritdoc />
    public Result<SubmissionAccessTokenDto> GenerateAccessToken(long submissionId, int expiryMinutes, IEnumerable<string> permissions) =>
        GenerateAccessToken(new SubmissionAccessTokenRequest(submissionId, expiryMinutes, permissions));

    /// <inheritdoc />
    public Result<SubmissionAccessTokenDto> GenerateAccessToken(SubmissionAccessTokenRequest request)
    {
        var invalid = Validate(request);
        if (invalid is not null)
        {
            return Result.Invalid(invalid);
        }

        var expiresAt = _dateTimeProvider.Now.AddMinutes(request.ExpiryMinutes);
        var fields = new SignedFields(
            request.SubmissionId,
            expiresAt.ToUnixTimeSeconds(),
            SubmissionAccessTokenPermissions.EncodeNames(request.Permissions),
            request.Stamp);

        return Result.Success(new SubmissionAccessTokenDto(fields.ToToken(Sign(fields)), expiresAt.UtcDateTime, request.Permissions));
    }

    /// <inheritdoc />
    public Result<SubmissionAccessTokenClaims> ValidateAccessToken(string token)
    {
        Guard.Against.NullOrWhiteSpace(token);

        if (!SignedFields.TryParse(token, out var fields, out var providedSignature))
        {
            return Result.Invalid(SubmissionAccessTokenErrors.ValidationErrors.InvalidToken);
        }

        // Constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedSignature),
            Encoding.UTF8.GetBytes(Sign(fields))))
        {
            return Result.Invalid(SubmissionAccessTokenErrors.ValidationErrors.InvalidToken);
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(fields.ExpiryUnix);
        if (_dateTimeProvider.Now > expiresAt)
        {
            return Result.Invalid(SubmissionAccessTokenErrors.ValidationErrors.TokenExpired);
        }

        var permissions = SubmissionAccessTokenPermissions.DecodeToNames(fields.PermissionsCode);
        if (!permissions.Any())
        {
            return Result.Invalid(SubmissionAccessTokenErrors.ValidationErrors.InvalidToken);
        }

        return Result.Success(new SubmissionAccessTokenClaims(
            fields.SubmissionId,
            permissions,
            expiresAt.UtcDateTime,
            fields.Stamp));
    }

    /// <inheritdoc />
    public Task<bool> IsCurrentAsync(SubmissionAccessTokenClaims claims, CancellationToken cancellationToken) =>
        _gate.IsCurrentAsync(claims, cancellationToken);

    private static ValidationError? Validate(SubmissionAccessTokenRequest request)
    {
        Guard.Against.NegativeOrZero(request.SubmissionId);
        Guard.Against.NegativeOrZero(request.ExpiryMinutes);
        Guard.Against.NullOrEmpty(request.Permissions);

        var invalidPermission = request.Permissions.FirstOrDefault(permission => !SubmissionAccessTokenPermissions.IsValid(permission));
        if (invalidPermission is not null)
        {
            return SubmissionAccessTokenErrors.ValidationErrors.InvalidPermission(invalidPermission);
        }

        return request.Stamp is null || SignedFields.IsValidStamp(request.Stamp)
            ? null
            : SubmissionAccessTokenErrors.ValidationErrors.InvalidStamp;
    }

    private string Sign(SignedFields fields)
    {
        var hashBytes = HMACSHA256.HashData(_signingKeyBytes, Encoding.UTF8.GetBytes(fields.ToStringToSign()));
        var signature = Convert.ToBase64String(hashBytes);

        // Make URL-safe
        signature = signature.Replace('+', '-').Replace('/', '_').TrimEnd('=');

        // Truncate to 16 chars for shorter URLs (~96 bits entropy)
        return signature[..16];
    }

    /// <summary>
    /// What a token signs, and its wire format: <c>{submissionId}.{expiry}.{permissions}.{signature}</c>, or with a
    /// stamp <c>{submissionId}.{expiry}.{permissions}.{stamp}.{signature}</c>. A token without a stamp signs the same
    /// string as before stamps existed.
    /// </summary>
    private sealed record SignedFields(long SubmissionId, long ExpiryUnix, string PermissionsCode, string? Stamp)
    {
        private const int MaxStampLength = 64;

        public static bool IsValidStamp(string stamp) =>
            stamp.Length is > 0 and <= MaxStampLength && stamp.All(char.IsAsciiHexDigitLower);

        public static bool TryParse(string token, out SignedFields fields, out string signature)
        {
            var parts = token.Split('.');
            fields = null!;
            signature = parts[^1];
            if (parts.Length is not (4 or 5)
                || !long.TryParse(parts[0], out var submissionId)
                || !long.TryParse(parts[1], out var expiryUnix))
            {
                return false;
            }

            fields = new SignedFields(submissionId, expiryUnix, parts[2], parts.Length == 5 ? parts[3] : null);
            return true;
        }

        public string ToStringToSign() => string.Join('\n', Parts());

        public string ToToken(string signature) => string.Join('.', [.. Parts(), signature]);

        private IEnumerable<string> Parts() => Stamp is null
            ? [SubmissionId.ToString(), ExpiryUnix.ToString(), PermissionsCode]
            : [SubmissionId.ToString(), ExpiryUnix.ToString(), PermissionsCode, Stamp];
    }
}
