using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using Lexico.Application.Contracts;
using Lexico.Domain.Entities;

namespace Lexico.API.Controllers
{
    [ApiController]
    [Route("api/documentos")]
    public class DocumentosController : ControllerBase
    {
        private readonly IDocumentoRepository _repo;              // para GetById/List
        private readonly IUploadDocumentoService _uploader;       // para insertar

        public DocumentosController(IDocumentoRepository repo, IUploadDocumentoService uploader)
        {
            _repo = repo;
            _uploader = uploader;
        }

        // ====== SUBIR POR FORM-DATA (file, usuarioId, codigoIso) ======
        [HttpPost]
        [RequestSizeLimit(200_000_000)] // 200 MB
        public async Task<IActionResult> SubirPorForm(
            [FromForm] IFormFile file,
            [FromForm] int usuarioId,
            [FromForm(Name = "codigoIso")] string codigoIso)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { mensaje = "Archivo vacío" });

            string contenido;
            using (var sr = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                contenido = await sr.ReadToEndAsync();

            var idiomaId = MapCodigoIsoToIdiomaId(codigoIso);

            var doc = new Documento
            {
                UsuarioId = usuarioId,
                NombreArchivo = file.FileName,
                ContenidoOriginal = contenido,
                IdiomaId = idiomaId,
                HashDocumento = ComputeSha256(contenido)
            };

            // setear tamaño si la propiedad existe (independiente de ñ)
            TrySetAny(doc, (int)file.Length, "TamanoArchivo", "TamañoArchivo", "TamanioArchivo");

            var id = await _uploader.SubirAsync(doc, HttpContext.RequestAborted);

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

            int longitud = 0;
            try { longitud = doc.ContenidoOriginal?.Length ?? 0; } catch { }

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

        private static int MapCodigoIsoToIdiomaId(string? iso) => (iso ?? "").Trim().ToLowerInvariant() switch
        {
            "es" => 1,
            "en" => 2,
            "ru" => 3,
            _ => 0
        };

        private static string ComputeSha256(string text)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static void TrySetAny(object target, object value, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                try
                {
                    var pi = target.GetType().GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                    if (pi != null && pi.CanWrite)
                    {
                        var dst = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
                        var final = value;
                        if (value != null && dst != value.GetType())
                            final = Convert.ChangeType(value, dst);
                        pi.SetValue(target, final);
                        return;
                    }
                }
                catch { /* ignore and try next */ }
            }
        }
    }
}
