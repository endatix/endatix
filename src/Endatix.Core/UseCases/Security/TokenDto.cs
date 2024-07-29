using System;

namespace Endatix.Core.UseCases.Security;

public record class TokenDto(string Token, DateTime ExpireAt);
