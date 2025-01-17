﻿using System;
using NodaTime;
using Npgsql.BackendMessages;
using Npgsql.Internal;
using Npgsql.Internal.TypeHandling;
using Npgsql.PostgresTypes;
using BclTimestampTzHandler = Npgsql.Internal.TypeHandlers.DateTimeHandlers.TimestampTzHandler;
using static Npgsql.NodaTime.Internal.NodaTimeUtils;

namespace Npgsql.NodaTime.Internal
{
    sealed partial class TimestampTzHandler : NpgsqlSimpleTypeHandler<Instant>, INpgsqlSimpleTypeHandler<ZonedDateTime>,
        INpgsqlSimpleTypeHandler<OffsetDateTime>, INpgsqlSimpleTypeHandler<DateTimeOffset>,
        INpgsqlSimpleTypeHandler<DateTime>
    {
        readonly BclTimestampTzHandler _bclHandler;
        readonly bool _convertInfinityDateTime;

        public TimestampTzHandler(PostgresType postgresType, bool convertInfinityDateTime)
            : base(postgresType)
        {
            _convertInfinityDateTime = convertInfinityDateTime;
            _bclHandler = new BclTimestampTzHandler(postgresType, convertInfinityDateTime);
        }

        #region Read

        public override Instant Read(NpgsqlReadBuffer buf, int len, FieldDescription? fieldDescription = null)
            => ReadInstant(buf, _convertInfinityDateTime);

        internal static Instant ReadInstant(NpgsqlReadBuffer buf, bool convertInfinityDateTime)
            => buf.ReadInt64() switch
            {
                long.MaxValue when convertInfinityDateTime => Instant.MaxValue,
                long.MinValue when convertInfinityDateTime => Instant.MinValue,
                var value => DecodeInstant(value)
            };

        ZonedDateTime INpgsqlSimpleTypeHandler<ZonedDateTime>.Read(NpgsqlReadBuffer buf, int len, FieldDescription? fieldDescription)
            => Read(buf, len, fieldDescription).InUtc();

        OffsetDateTime INpgsqlSimpleTypeHandler<OffsetDateTime>.Read(NpgsqlReadBuffer buf, int len, FieldDescription? fieldDescription)
            => Read(buf, len, fieldDescription).WithOffset(Offset.Zero);

        DateTimeOffset INpgsqlSimpleTypeHandler<DateTimeOffset>.Read(NpgsqlReadBuffer buf, int len, FieldDescription? fieldDescription)
            => _bclHandler.Read<DateTimeOffset>(buf, len, fieldDescription);

        DateTime INpgsqlSimpleTypeHandler<DateTime>.Read(NpgsqlReadBuffer buf, int len, FieldDescription? fieldDescription)
            => _bclHandler.Read<DateTime>(buf, len, fieldDescription);

        #endregion Read

        #region Write

        public override int ValidateAndGetLength(Instant value, NpgsqlParameter? parameter)
            => 8;

        int INpgsqlSimpleTypeHandler<ZonedDateTime>.ValidateAndGetLength(ZonedDateTime value, NpgsqlParameter? parameter)
        {
            if (!LegacyTimestampBehavior && value.Zone != DateTimeZone.Utc)
            {
                throw new InvalidCastException(
                    $"Cannot write ZonedDateTime with Zone={value.Zone} to PostgreSQL type 'timestamp with time zone', only UTC is supported. " +
                    "See the Npgsql.EnableLegacyTimestampBehavior AppContext switch to enable legacy behavior.");
            }

            return 8;
        }

        public int ValidateAndGetLength(OffsetDateTime value, NpgsqlParameter? parameter)
        {
            if (!LegacyTimestampBehavior && value.Offset != Offset.Zero)
            {
                throw new InvalidCastException(
                    $"Cannot write OffsetDateTime with Offset={value.Offset} to PostgreSQL type 'timestamp with time zone', only offset 0 (UTC) is supported. " +
                    "See the Npgsql.EnableLegacyTimestampBehavior AppContext switch to enable legacy behavior.");
            }

            return 8;
        }

        public override void Write(Instant value, NpgsqlWriteBuffer buf, NpgsqlParameter? parameter)
            => WriteInstant(value, buf, _convertInfinityDateTime);

        internal static void WriteInstant(Instant value, NpgsqlWriteBuffer buf, bool convertInfinityDateTime)
        {
            if (convertInfinityDateTime)
            {
                if (value == Instant.MaxValue)
                {
                    buf.WriteInt64(long.MaxValue);
                    return;
                }

                if (value == Instant.MinValue)
                {
                    buf.WriteInt64(long.MinValue);
                    return;
                }
            }

            buf.WriteInt64(EncodeInstant(value));
        }

        void INpgsqlSimpleTypeHandler<ZonedDateTime>.Write(ZonedDateTime value, NpgsqlWriteBuffer buf, NpgsqlParameter? parameter)
            => Write(value.ToInstant(), buf, parameter);

        public void Write(OffsetDateTime value, NpgsqlWriteBuffer buf, NpgsqlParameter? parameter)
            => Write(value.ToInstant(), buf, parameter);

        int INpgsqlSimpleTypeHandler<DateTimeOffset>.ValidateAndGetLength(DateTimeOffset value, NpgsqlParameter? parameter)
            => _bclHandler.ValidateAndGetLength(value, parameter);

        void INpgsqlSimpleTypeHandler<DateTimeOffset>.Write(DateTimeOffset value, NpgsqlWriteBuffer buf, NpgsqlParameter? parameter)
            => _bclHandler.Write(value, buf, parameter);

        int INpgsqlSimpleTypeHandler<DateTime>.ValidateAndGetLength(DateTime value, NpgsqlParameter? parameter)
            => ((INpgsqlSimpleTypeHandler<DateTime>)_bclHandler).ValidateAndGetLength(value, parameter);

        void INpgsqlSimpleTypeHandler<DateTime>.Write(DateTime value, NpgsqlWriteBuffer buf, NpgsqlParameter? parameter)
            => _bclHandler.Write(value, buf, parameter);

        #endregion Write
    }
}
