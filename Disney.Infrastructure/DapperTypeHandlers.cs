using System.Data;
using Dapper;
using Disney.Domain;

namespace Disney.Infrastructure;

internal static class DapperTypeHandlers
{
    public static void Register()
    {
        SqlMapper.AddTypeHandler(new DateOnlyHandler());
        SqlMapper.AddTypeHandler(new NullableDateOnlyHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyHandler());
        SqlMapper.AddTypeHandler(new NullableTimeOnlyHandler());
        SqlMapper.AddTypeHandler(new StringEnumHandler<CompanyRole>());
        SqlMapper.AddTypeHandler(new StringEnumHandler<CompanyVisitStatus>());
        SqlMapper.AddTypeHandler(new StringEnumHandler<NotificationDeliveryStatus>());
    }

    private sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value) => value switch
        {
            DateOnly date => date,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            string text => DateOnly.Parse(text),
            _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to DateOnly.")
        };

        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }
    }

    private sealed class NullableDateOnlyHandler : SqlMapper.TypeHandler<DateOnly?>
    {
        public override DateOnly? Parse(object value) =>
            value is null or DBNull ? null : new DateOnlyHandler().Parse(value);

        public override void SetValue(IDbDataParameter parameter, DateOnly? value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.HasValue
                ? value.Value.ToDateTime(TimeOnly.MinValue)
                : DBNull.Value;
        }
    }

    private sealed class TimeOnlyHandler : SqlMapper.TypeHandler<TimeOnly>
    {
        public override TimeOnly Parse(object value) => value switch
        {
            TimeOnly time => time,
            TimeSpan timeSpan => TimeOnly.FromTimeSpan(timeSpan),
            DateTime dateTime => TimeOnly.FromDateTime(dateTime),
            string text => TimeOnly.Parse(text),
            _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to TimeOnly.")
        };

        public override void SetValue(IDbDataParameter parameter, TimeOnly value)
        {
            parameter.DbType = DbType.Time;
            parameter.Value = value.ToTimeSpan();
        }
    }

    private sealed class NullableTimeOnlyHandler : SqlMapper.TypeHandler<TimeOnly?>
    {
        public override TimeOnly? Parse(object value) =>
            value is null or DBNull ? null : new TimeOnlyHandler().Parse(value);

        public override void SetValue(IDbDataParameter parameter, TimeOnly? value)
        {
            parameter.DbType = DbType.Time;
            parameter.Value = value.HasValue ? value.Value.ToTimeSpan() : DBNull.Value;
        }
    }

    private sealed class StringEnumHandler<TEnum> : SqlMapper.TypeHandler<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Parse(object value) =>
            value is string text &&
            Enum.TryParse<TEnum>(text, true, out var parsed)
                ? parsed
                : throw new InvalidCastException(
                    $"Cannot convert {value} to {typeof(TEnum).Name}.");

        public override void SetValue(IDbDataParameter parameter, TEnum value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString().ToLowerInvariant();
        }
    }
}
