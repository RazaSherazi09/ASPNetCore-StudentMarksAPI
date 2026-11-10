using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MarksApi.Common;

public static class DbExceptionHelper
{
    public static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException pgEx &&
            pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return true;
        }

        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsForeignKeyViolation(DbUpdateException exception, out string? constraintName)
    {
        if (exception.InnerException is PostgresException pgEx &&
            pgEx.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            constraintName = pgEx.ConstraintName;
            return true;
        }

        constraintName = null;
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("foreign key", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("REFERENCE constraint", StringComparison.OrdinalIgnoreCase);
    }
}

