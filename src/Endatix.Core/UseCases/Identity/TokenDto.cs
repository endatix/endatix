using System;

namespace Endatix.Core.UseCases.Identity;

public record class TokenDto(string Token, DateTime ExpireAt);
