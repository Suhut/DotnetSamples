using NpgsqlTypes;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL.ColumnWriters;

namespace SerilogUI.CusttomColumnWriter;

public class SpanIdColumnWriterBase : ColumnWriterBase
{
    public SpanIdColumnWriterBase(NpgsqlDbType dbType = NpgsqlDbType.Text) : base(dbType)
    {
        DbType = NpgsqlDbType.Text;
    }

    public override object GetValue(LogEvent logEvent, IFormatProvider formatProvider = null)
    {
        return logEvent.SpanId.ToString();
    }
}
