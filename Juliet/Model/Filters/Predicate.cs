namespace Juliet.Model.Filters;

public class Predicate : Query
{
    public Predicate(FilterField name, FilterOperator @operator, object value)
    {
        Name = name;
        Operator = @operator;
        Value = value;
    }

    public FilterField Name { get; init; }

    public FilterOperator Operator { get; init; }

    /// <summary>
    /// <see cref="Query"/> or field_specific_json_value
    /// </summary>
    public object Value { get; init; }
}
