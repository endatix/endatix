namespace Endatix.Api.Infrastructure.Cors;

/// <summary>
/// <inheritdoc/>
/// </summary>
public class CorsWildcardSearcher : IWildcardSearcher
{
    private static readonly char _matchAllSymbol = '*';
    private static readonly char _ignoreAllSymbol = '-';

    // <inheritdoc/>
    public CorsWildcardResult SearchForWildcard(IList<string> inputParams)
    {
        if (inputParams == null)
        {
            return CorsWildcardResult.None;
        }

        foreach (var param in inputParams)
        {
            if (string.IsNullOrWhiteSpace(param))
            {
                continue;
            }

            var trimmedParam = param.Trim();

            if (trimmedParam.Length == 1)
            {
                if (trimmedParam[0] == _matchAllSymbol)
                {
                    return CorsWildcardResult.MatchAll;
                }

                if (trimmedParam[0] == _ignoreAllSymbol)
                {
                    return CorsWildcardResult.IgnoreAll;
                }
            }
        }

        return CorsWildcardResult.None;
    }

}