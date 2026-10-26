using System.Linq.Expressions;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>
/// Binds a <see cref="PersonalizationSnapshotJson"/> match, written over the Snapshot string, to a query's own
/// Snapshot column, so a specification can combine it with predicates on the submission in one WHERE.
/// </summary>
internal static class SnapshotExpression
{
    /// <summary>The body of <paramref name="lambda"/> with its one parameter replaced by <paramref name="argument"/>.</summary>
    public static Expression BodyOf(LambdaExpression lambda, Expression argument) =>
        new Rebind(lambda.Parameters[0], argument).Visit(lambda.Body);

    /// <summary>Replaces one lambda parameter with another expression.</summary>
    private sealed class Rebind(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
