using Microsoft.EntityFrameworkCore;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Infrastructure.Exceptions
{
    public static class DbExceptionHelper
    {
        public static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // checking if the inner exception is a PostgresException and the SQLSTATE code indicates unique violation
            if (ex.InnerException is PostgresException pgEx)
            {
                // 23505 = unique_violation according to PostgreSQL SQLSTATE error codes
                // https://www.postgresql.org/docs/current/errcodes-appendix.html
                return pgEx.SqlState == PostgresErrorCodes.UniqueViolation;
            }

            return false;
        }
    }
}
