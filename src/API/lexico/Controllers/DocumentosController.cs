using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using Lexico.Application.Contracts;
using Lexico.Domain.Entities;

namespace Lexico.API.Controllers
{
    [ApiController]
    [Route("api/documentos")]
    public class DocumentosController : ControllerBase
    {
        private readonly IDocumentoRepository _repo;

        public DocumentosController(IDocumentoRepository repo)
        {
            _repo = repo;
        }

        // ====== SUBIR POR FORM-DATA (file, usuarioId, codigoIso) ======
        [HttpPost]
        [RequestSizeLimit(200_000_000)] // 200 MB
        public async Task<IActionResult> SubirPorForm(
            [FromForm] IFormFile file,
            [FromForm] int usuarioId,
            [FromForm] string codigoIso)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { mensaje = "Archivo vacío" });

            // Leer texto (UTF-8 BOM-aware)
            string contenido;
            using (var sr = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                contenido = await sr.ReadToEndAsync();

            var idiomaId = MapCodigoIsoToIdiomaId(codigoIso);

            // Construir entidad según lo que tengas en tu Domain
            var doc = new Documento
            {
                UsuarioId = usuarioId,
                NombreArchivo = file.FileName,
                ContenidoOriginal = contenido,
                IdiomaId = idiomaId,
                HashDocumento = ComputeSha256(contenido)
            };

            // Si tu entidad tiene 'TamanoArchivo' (o 'TamañoArchivo'), se setea. Si no, se ignora.
            TrySetProperty(doc, "TamanoArchivo", (int)file.Length);
            TrySetProperty(doc, "TamañoArchivo", (int)file.Length); // por si usas ñ

            // Persistir con el método que exista en tu repo
            int id;
            try
            {
                id = await PersistDocumentoAsync(doc);
            }
            catch (NotSupportedException nse)
            {
                return StatusCode(501, new { mensaje = nse.Message });
            }

            return Ok(new
            {
                mensaje = "Documento cargado",
                documentoId = id,
                idioma = codigoIso?.ToLowerInvariant(),
                hash = doc.HashDocumento
            });
        }

        // ====== GET por ID ======
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Obtener(int id)
        {
            var doc = await _repo.GetByIdAsync(id);
            if (doc == null) return NotFound(new { mensaje = "Documento no encontrado", id });

            // Intentar leer longitud si existe ContenidoOriginal
            int longitud = 0;
            try { longitud = doc.ContenidoOriginal?.Length ?? 0; } catch { /* ignore */ }

            return Ok(new
            {
                id = doc.Id,
                doc.NombreArchivo,
                doc.UsuarioId,
                doc.IdiomaId,
                longitud
            });
        }

        // ======================
        // Helpers
        // ======================

        private static int MapCodigoIsoToIdiomaId(string? iso)
        {
            switch ((iso ?? "").Trim().ToLowerInvariant())
            {
                case "es": return 1;
                case "en": return 2;
                case "ru": return 3;
                default:   return 0; // desconocido
            }
        }

        private static string ComputeSha256(string text)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        /// Intenta asignar una propiedad por nombre; si no existe o no es compatible, no hace nada.
        private static void TrySetProperty(object target, string propertyName, object value)
        {
            try
            {
                var pi = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (pi != null && pi.CanWrite)
                {
                    // conversión básica de tipos si hace falta
                    var finalValue = value;
                    if (value != null && pi.PropertyType != value.GetType())
                    {
                        finalValue = Convert.ChangeType(value, Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType);
                    }
                    pi.SetValue(target, finalValue);
                }
            }
            catch
            {
                // ignorar: la propiedad puede no existir o no ser asignable
            }
        }

        /// Busca dinámicamente el método del repositorio que tengas para insertar un documento.
        private async Task<int> PersistDocumentoAsync(Documento doc)
        {
            // Orden de prueba de nombres comunes
            var candidates = new[]
            {
                "CreateAsync",
                "InsertAsync",
                "AddAsync",
                "SaveAsync",
                "GuardarAsync",
                "CargarAsync",
                "SubirAsync"
            };

            var repoType = _repo.GetType();
            foreach (var name in candidates)
            {
                var mi = repoType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (mi == null) continue;

                // Debe ser Task<int> o Task con int result
                var returnType = mi.ReturnType;
                var isTask = typeof(Task).IsAssignableFrom(returnType);
                if (!isTask) continue;

                // Invocar
                var resultObj = mi.Invoke(_repo, new object[] { doc });
                if (resultObj is Task t)
                {
                    await t.ConfigureAwait(false);

                    // Si es Task<int>, leer Result mediante reflexión
                    var ti = t.GetType();
                    if (ti.IsGenericType && ti.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        var resProp = ti.GetProperty("Result");
                        if (resProp != null)
                        {
                            var resVal = resProp.GetValue(t);
                            if (resVal is int id) return id;
                        }
                    }
                }
            }

            // Si llegamos aquí, no encontramos ningún método compatible
            throw new NotSupportedException("El repositorio de documentos no expone un método Async para crear (Create/Insert/Add/Save/Guardar/Cargar/Subir).");
        }
    }
}
