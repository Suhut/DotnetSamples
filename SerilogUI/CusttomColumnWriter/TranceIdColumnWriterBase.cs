using NpgsqlTypes;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL.ColumnWriters;

namespace SerilogUI.CusttomColumnWriter;

public class TranceIdColumnWriterBase : ColumnWriterBase
{
    public TranceIdColumnWriterBase(NpgsqlDbType dbType = NpgsqlDbType.Text) : base(dbType)
    {
        DbType = NpgsqlDbType.Text;
    }

    public override object GetValue(LogEvent logEvent, IFormatProvider formatProvider = null)
    {
        return logEvent.TraceId.ToString();
    }
}
