using System.Linq.Expressions;
using DARAK.Api.Interfaces;

namespace DARAK.Api.Helpers;

public static class CompoundAccessQueryExtensions
{
    public static IQueryable<T> ApplyCompoundAccess<T>(
        this IQueryable<T> query,
        CompoundAccessScope scope,
        Expression<Func<T, Guid>> compoundIdSelector)
    {
        if (!scope.IsAuthenticated)
        {
            return query.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return query;
        }

        if (scope.AllowedCompoundIds.Length == 0)
        {
            return query.Where(_ => false);
        }

        var containsExpression = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            [typeof(Guid)],
            Expression.Constant(scope.AllowedCompoundIds),
            compoundIdSelector.Body);

        return query.Where(Expression.Lambda<Func<T, bool>>(
            containsExpression,
            compoundIdSelector.Parameters));
    }
}
