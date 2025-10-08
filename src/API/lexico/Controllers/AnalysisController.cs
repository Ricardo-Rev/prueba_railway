using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Lexico.Application.Contracts;
using Lexico.Application.Rules;
using Lexico.Domain.Entities;

namespace Lexico.API.Controllers
{
    [ApiController]
    [Route("api/analisis")]
    public class AnalisisController : ControllerBase
    {
        private readonly IAnalysisService _service;
        private readonly IDocumentoRepository _docs;
        private readonly IConfiguracionAnalisisRepository _cfg;

        public AnalisisController(
            IAnalysisService service,
            IDocumentoRepository docs,
            IConfiguracionAnalisisRepository cfg)
        {
            _service = service;
            _docs = docs;
            _cfg = cfg;
        }

        /// Ejecuta el análisis (calcula aunque no se persista). Úsalo primero.
        [HttpPost("{documentoId:int}")]
        public async Task<IActionResult> Ejecutar(int documentoId, [FromQuery] string? idioma = null)
        {
            var doc = await _docs.GetByIdAsync(documentoId);
            if (doc == null) return NotFound(new { mensaje = "Documento no encontrado", documentoId });

            if (string.IsNullOrWhiteSpace(doc.ContenidoOriginal))
                return BadRequest(new { mensaje = "El documento no tiene contenido_original", documentoId });

            var result = await _service.EjecutarAsync(documentoId);

            return Ok(new
            {
                mensaje = "Análisis completado",
                documentoId,
                totalPalabras = result.TotalPalabras,
                palabrasUnicas = result.PalabrasUnicas
            });
        }

        /// Resumen inmediato (top/low/clases) sin depender de filas guardadas.
        [HttpGet("{documentoId:int}")]
        public async Task<IActionResult> Resumen(int documentoId, [FromQuery] string? idioma = null, [FromQuery] int topN = 20, [FromQuery] int lowN = 20)
        {
            var doc = await _docs.GetByIdAsync(documentoId);
            if (doc == null) return NotFound(new { mensaje = "Documento no encontrado", documentoId });

            if (string.IsNullOrWhiteSpace(doc.ContenidoOriginal))
                return BadRequest(new { mensaje = "El documento no tiene contenido_original", documentoId });

            // Reglas
            string code = idioma?.ToLowerInvariant()
                ?? InferCodigoIsoFromIdiomaId(doc.IdiomaId)
                ?? "es";
            var rules = RegexRulesFactory.For(code);

            // Stopwords
            var stop = await _cfg.GetStopwordsAsync(doc.IdiomaId);

            // Tokeniza y cuenta
            var counter = new Dictionary<string, int>(16_384, StringComparer.OrdinalIgnoreCase);
            foreach (var token in Tokenize(doc.ContenidoOriginal, rules.WordRegex))
            {
                if (counter.TryGetValue(token, out var c)) counter[token] = c + 1;
                else counter[token] = 1;
            }

            var total = counter.Values.Sum();
            var unicas = counter.Count;

            var sinStop = counter.Where(kv => !stop.Contains(kv.Key));
            var top = sinStop.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(topN).ToList();
            var low = sinStop.Where(kv => kv.Value == 1).OrderBy(kv => kv.Key).Take(lowN).ToList();

            var clasif = Classify(counter, rules);

            return Ok(new
            {
                documentoId,
                idioma = code,
                totalPalabras = total,
                palabrasUnicas = unicas,
                topFrecuentes = top.Select(x => new { palabra = x.Key, frecuencia = x.Value }),
                menosFrecuentes = low.Select(x => new { palabra = x.Key, frecuencia = x.Value }),
                pronombres = clasif.Where(x => x.Categoria == "pronombre_personal").Select(x => new { x.Palabra, x.Frecuencia }),
                verbos = clasif.Where(x => x.Categoria == "verbo").Select(x => new { x.Palabra, x.Frecuencia }),
                sustantivos = clasif.Where(x => x.Categoria == "sustantivo").Select(x => new { x.Palabra, x.Frecuencia })
            });
        }

        // ========================
        // Helpers del controlador
        // ========================
        private static string? InferCodigoIsoFromIdiomaId(int idiomaId)
        {
            return idiomaId switch
            {
                1 => "es",
                2 => "en",
                3 => "ru",
                _ => null
            };
        }

        private static IEnumerable<string> Tokenize(string text, string wordRegex)
        {
            var opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline;
            foreach (Match m in Regex.Matches(text ?? string.Empty, wordRegex, opts))
            {
                var t = m.Value.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(t)) yield return t;
            }
        }

        private static List<ClasificacionGramatical> Classify(
            IDictionary<string, int> counter,
            IRegexRules rules)
        {
            var list = new List<ClasificacionGramatical>();
            var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

            foreach (var kv in counter)
            {
                var w = kv.Key;
                var freq = kv.Value;

                if (Regex.IsMatch(w, rules.PronounsRegex, options))
                {
                    list.Add(new ClasificacionGramatical { Palabra = w, Categoria = "pronombre_personal", Frecuencia = freq });
                    continue;
                }
                if (Regex.IsMatch(w, rules.VerbRegex, options))
                {
                    list.Add(new ClasificacionGramatical { Palabra = w, Categoria = "verbo", Frecuencia = freq });
                }
                if (Regex.IsMatch(w, rules.NounRegex, options))
                {
                    list.Add(new ClasificacionGramatical { Palabra = w, Categoria = "sustantivo", Frecuencia = freq });
                }
            }
            return list;
        }
    }
}
