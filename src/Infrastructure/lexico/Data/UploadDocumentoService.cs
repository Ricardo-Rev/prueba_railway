using System;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Lexico.Application.Contracts;
using Lexico.Domain.Entities;

namespace Lexico.Infrastructure.Data
{
    /// Inserta directo en la tabla `documentos` y devuelve el ID.
    /// Soporta Postgres, MySQL/MariaDB, SQL Server y SQLite.
    public class UploadDocumentoService : IUploadDocumentoService
    {
        private readonly DapperConnectionFactory _factory;

        public UploadDocumentoService(DapperConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<int> SubirAsync(Documento doc, CancellationToken ct = default)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            int usuarioId = GetInt(doc, "UsuarioId");
            string nombreArchivo = GetString(doc, "NombreArchivo") ?? "";
            string contenidoOriginal = GetString(doc, "ContenidoOriginal") ?? "";
            int idiomaId = GetInt(doc, "IdiomaId");
            int tamanoArchivo = FirstInt(doc, "TamanoArchivo", "TamañoArchivo", "TamanioArchivo");
            string hash = GetString(doc, "HashDocumento") ?? "";

            using var con = _factory.Create(); // Debe devolver IDbConnection del proveedor real
            if (con.State != ConnectionState.Open) con.Open();

            var provider = DetectProvider(con);
            var q = GetQuoter(provider);

            // Intentos de columna: con ñ -> sin ñ -> sin columna
            var attempts = new (string ColumnName, bool IncludeSize)[]
            {
                ("tamaño_archivo", true),
                ("tamano_archivo", true),
                ("", false)
            };

            foreach (var (column, includeSize) in attempts)
            {
                try
                {
                    var id = await TryInsertAsync(con, provider, q, usuarioId, nombreArchivo,
                        contenidoOriginal, idiomaId, tamanoArchivo, hash, column, includeSize, ct);

                    if (id > 0) return id;
                }
                catch
                {
                    // probamos la siguiente variante
                }
            }

            throw new InvalidOperationException("No fue posible insertar el documento (probados Postgres/MySQL/SQLServer/SQLite y variantes de columna tamaño_archivo). Revisa el motor y los nombres de columnas.");
        }

        // --------------------------------------------------------------------
        // SQL por proveedor con quoting correcto e ID de retorno portable
        // --------------------------------------------------------------------
        private static async Task<int> TryInsertAsync(
            IDbConnection con,
            DbProvider provider,
            (string L, string R) q,
            int usuarioId,
            string nombreArchivo,
            string contenidoOriginal,
            int idiomaId,
            int tamanoArchivo,
            string hash,
            string sizeColumnCandidate,
            bool includeSize,
            CancellationToken ct)
        {
            var table = $"{q.L}documentos{q.R}";
            var colUsuario = $"{q.L}usuario_id{q.R}";
            var colNombre  = $"{q.L}nombre_archivo{q.R}";
            var colTexto   = $"{q.L}contenido_original{q.R}";
            var colIdioma  = $"{q.L}idioma_id{q.R}";
            var colHash    = $"{q.L}hash_documento{q.R}";
            var colId      = $"{q.L}id{q.R}";
            string? colSize = null;

            if (includeSize)
            {
                if (!string.IsNullOrWhiteSpace(sizeColumnCandidate))
                    colSize = $"{q.L}{sizeColumnCandidate}{q.R}";
            }

            string columns, values;
            var p = new DynamicParameters();
            p.Add("@UsuarioId", usuarioId);
            p.Add("@NombreArchivo", nombreArchivo);
            p.Add("@ContenidoOriginal", contenidoOriginal);
            p.Add("@IdiomaId", idiomaId);
            p.Add("@HashDocumento", hash);

            if (colSize != null)
            {
                columns = $"{colUsuario}, {colNombre}, {colTexto}, {colIdioma}, {colSize}, {colHash}";
                values  = $"@UsuarioId, @NombreArchivo, @ContenidoOriginal, @IdiomaId, @TamanoArchivo, @HashDocumento";
                p.Add("@TamanoArchivo", tamanoArchivo);
            }
            else
            {
                columns = $"{colUsuario}, {colNombre}, {colTexto}, {colIdioma}, {colHash}";
                values  = $"@UsuarioId, @NombreArchivo, @ContenidoOriginal, @IdiomaId, @HashDocumento";
            }

            string sql;
            switch (provider)
            {
                case DbProvider.Postgres:
                    sql = $@"INSERT INTO {table} ({columns}) VALUES ({values}) RETURNING {colId};";
                    return await con.ExecuteScalarAsync<int>(new CommandDefinition(sql, p, cancellationToken: ct));

                case DbProvider.MySql:
                    sql = $@"INSERT INTO {table} ({columns}) VALUES ({values}); SELECT LAST_INSERT_ID();";
                    var lid = await con.ExecuteScalarAsync<long>(new CommandDefinition(sql, p, cancellationToken: ct));
                    return checked((int)lid);

                case DbProvider.SqlServer:
                    // Preferimos OUTPUT INSERTED.id, compatible con SQL Server
                    sql = $@"INSERT INTO {table} ({columns}) OUTPUT INSERTED.{colId} VALUES ({values});";
                    var sid = await con.ExecuteScalarAsync<int>(new CommandDefinition(sql, p, cancellationToken: ct));
                    if (sid > 0) return sid;

                    // Fallback SCOPE_IDENTITY()
                    sql = $@"INSERT INTO {table} ({columns}) VALUES ({values}); SELECT CAST(SCOPE_IDENTITY() as int);";
                    sid = await con.ExecuteScalarAsync<int>(new CommandDefinition(sql, p, cancellationToken: ct));
                    return sid;

                case DbProvider.Sqlite:
                    sql = $@"INSERT INTO {table} ({columns}) VALUES ({values}); SELECT last_insert_rowid();";
                    var zid = await con.ExecuteScalarAsync<long>(new CommandDefinition(sql, p, cancellationToken: ct));
                    return checked((int)zid);

                default:
                    throw new NotSupportedException("Proveedor de base de datos no soportado.");
            }
        }

        // --------------------------------------------------------------------
        // Detección básica del proveedor por tipo de conexión
        // --------------------------------------------------------------------
        private static DbProvider DetectProvider(IDbConnection con)
        {
            var t = con.GetType().FullName ?? string.Empty;

            if (t.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                return DbProvider.Postgres;
            if (t.Contains("MySql", StringComparison.OrdinalIgnoreCase))
                return DbProvider.MySql;
            if (t.Contains("SqlClient", StringComparison.OrdinalIgnoreCase))
                return DbProvider.SqlServer;
            if (t.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                return DbProvider.Sqlite;

            // Heurística por ConnectionString como fallback
            var cs = con.ConnectionString ?? "";
            if (cs.Contains("Host=", StringComparison.OrdinalIgnoreCase) || cs.Contains("Username=", StringComparison.OrdinalIgnoreCase))
                return DbProvider.Postgres;
            if (cs.Contains("Uid=", StringComparison.OrdinalIgnoreCase) || cs.Contains("Server=", StringComparison.OrdinalIgnoreCase) && cs.Contains("Database=", StringComparison.OrdinalIgnoreCase))
                return DbProvider.MySql;
            if (cs.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase) || cs.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase))
                return DbProvider.SqlServer;
            if (cs.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) && cs.Contains(".db", StringComparison.OrdinalIgnoreCase))
                return DbProvider.Sqlite;

            throw new NotSupportedException($"No se pudo detectar el proveedor de BD a partir de {t}.");
        }

        private static (string L, string R) GetQuoter(DbProvider provider)
        {
            return provider switch
            {
                DbProvider.Postgres => ("\"", "\""),     // "ident"
                DbProvider.MySql    => ("`", "`"),       // `ident`
                DbProvider.SqlServer=> ("[", "]"),       // [ident]
                DbProvider.Sqlite   => ("\"", "\""),     // "ident"
                _ => ("\"", "\"")
            };
        }

        // ---------------------
        // Helpers de reflexión
        // ---------------------
        private static string? GetString(object obj, string prop)
        {
            try
            {
                var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                var val = pi?.GetValue(obj);
                return val?.ToString();
            }
            catch { return null; }
        }

        private static int GetInt(object obj, string prop)
        {
            try
            {
                var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                var val = pi?.GetValue(obj);
                if (val == null) return 0;
                if (val is int i) return i;
                if (val is long l) return checked((int)l);
                if (int.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            catch { }
            return 0;
        }

        private static int FirstInt(object obj, params string[] props)
        {
            foreach (var p in props)
            {
                var v = GetInt(obj, p);
                if (v != 0) return v;
            }
            return 0;
        }

        private enum DbProvider
        {
            Postgres,
            MySql,
            SqlServer,
            Sqlite
        }
    }
}
