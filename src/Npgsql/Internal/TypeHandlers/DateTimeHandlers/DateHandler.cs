﻿using System;
using Npgsql.BackendMessages;
using Npgsql.Internal.TypeHandling;
using Npgsql.PostgresTypes;
using NpgsqlTypes;

namespace Npgsql.Internal.TypeHandlers.DateTimeHandlers
{
    /// <summary>
    /// A type handler for the PostgreSQL date data type.
    /// </summary>
    /// <remarks>
    /// See https://www.postgresql.org/docs/current/static/datatype-datetime.html.
    ///
    /// The type handler API allows customizing Npgsql's behavior in powerful ways. However, although it is public, it
    /// should be considered somewhat unstable, and may change in breaking ways, including in non-major releases.
    /// Use it at your own risk.
    /// </remarks>
    public partial class DateHandler : NpgsqlSimpleTypeHandlerWithPsv<DateTime, NpgsqlDate>
#if NET6_0_OR_GREATER
        , INpgsqlSimpleTypeHandler<DateOnly>
#endif
    {
        /// <summary>
        /// Whether to convert positive and negative infinity values to DateTime.{Max,Min}Value when
        /// a DateTime is requested
        /// </summary>
        readonly bool _convertInfinityDateTime;

        /// <summary>
        /// Constructs a <see cref="DateHandler"/>
        /// </summary>
        public DateHandler(PostgresType postgresType, bool convertInfinityDateTime)
            : base(postgresType)
            => _convertInfinityDateTime = convertInfinityDateTime;

        #region Read

        /// <inheritdoc />
        public override DateTime Read(NpgsqlReadBuffer buf, int len, FieldDescription? fieldDescription = null)
        {
            var npgsqlDate = ReadPsv(buf, len, fieldDescription);

            if (npgsqlDate.IsFinite)
                return (DateTime)npgsqlDate;
            if (!_convertInfinityDateTime)
                throw new InvalidCastException("Can't convert infinite date values to DateTime");
            if (npgsqlDate.IsInfinity)
                return DateTime.MaxValue;
            return DateTime.MinValue;
        }

        /// <remarks>
        /// Copied wholesale from Postgresql backend/utils/adt/datetime.c:j2date
        /// </remarks>
        protected override NpgsqlDate ReadPsv(NpgsqlReadBuffer buf, int len, FieldDescription? fieldDescription = null)
        {
            var binDate = buf.ReadInt32();

            return binDate switch
            {
                int.MaxValue => NpgsqlDate.Infinity,
                int.MinValue => NpgsqlDate.NegativeInfinity,
                _            => new NpgsqlDate(binDate + 730119)
            };
        }

        #endregion Read

        #region Write

        /// <inheritdoc />
        public override int ValidateAndGetLength(DateTime value, NpgsqlParameter? parameter) => 4;

        /// <inheritdoc />
        public override int ValidateAndGetLength(NpgsqlDate value, NpgsqlParameter? parameter) => 4;

        /// <inheritdoc />
        public override void Write(DateTime value, NpgsqlWriteBuffer buf, NpgsqlParameter? parameter)
        {
            NpgsqlDate value2;
            if (_convertInfinityDateTime)
            {
                if (value == DateTime.MaxValue)
                    value2 = NpgsqlDate.Infinity;
                else if (value == DateTime.MinValue)
                    value2 = NpgsqlDate.NegativeInfinity;
                else
                    value2 = new NpgsqlDate(value);
            }
            else
                value2 = new NpgsqlDate(value);

            Write(value2, buf, parameter);
        }

        /// <inheritdoc />
        public override void Write(NpgsqlDate value, NpgsqlWriteBuffer buf, NpgsqlParameter? parameter)
        {
            if (value == NpgsqlDate.NegativeInfinity)
                buf.WriteInt32(int.MinValue);
            else if (value == NpgsqlDate.Infinity)
                buf.WriteInt32(int.MaxValue);
            else
                buf.WriteInt32(value.DaysSinceEra - 730119);
        }

        #endregion Write

#if NET6_0_OR_GREATER
        DateOnly INpgsqlSimpleTypeHandler<DateOnly>.Read(NpgsqlReadBuffer buf, int len, FieldDescription? fieldDescription)
        {
            var npgsqlDate = ReadPsv(buf, len, fieldDescription);

            if (npgsqlDate.IsFinite)
                return (DateOnly)npgsqlDate;
            if (!_convertInfinityDateTime)
                throw new InvalidCastException("Can't convert infinite date values to DateOnly");
            if (npgsqlDate.IsInfinity)
                return DateOnly.MaxValue;
            return DateOnly.MinValue;
        }

        public int ValidateAndGetLength(DateOnly value, NpgsqlParameter? parameter) => 4;

        public void Write(DateOnly value, NpgsqlWriteBuffer buf, NpgsqlParameter? parameter)
        {
            NpgsqlDate value2;
            if (_convertInfinityDateTime)
            {
                if (value == DateOnly.MaxValue)
                    value2 = NpgsqlDate.Infinity;
                else if (value == DateOnly.MinValue)
                    value2 = NpgsqlDate.NegativeInfinity;
                else
                    value2 = new NpgsqlDate(value);
            }
            else
                value2 = new NpgsqlDate(value);

            Write(value2, buf, parameter);
        }
#endif
    }
}
