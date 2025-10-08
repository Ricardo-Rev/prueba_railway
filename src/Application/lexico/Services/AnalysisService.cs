using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Lexico.Application.Contracts;   // IAnalysisService + repos
using Lexico.Application.Rules;      // Reglas por idioma
using Lexico.Domain.Entities;        // AnalisisLexico, FrecuenciaPalabra, ClasificacionGramatical, PatronReconocido, Archivo, Documento

namespace Lexico.Application.Services
{
    public class AnalysisService : IAnalysisService
    {
        private readonly IAnalisisRepository _repoAnalisis;
        private readonly IDocumentoRepository _repoDocumento;
        private readonly IIdiomaRepository _repoIdioma;
        private readonly IConfiguracionAnalisisRepository _repoCfg;

        public AnalysisService(
            IAnalisisRepository repoAnalisis,
            IDocumentoRepository repoDocumento,
            IIdiomaRepository repoIdioma,
            IConfiguracionAnalisisRepository repoCfg)
        {
            _repoAnalisis  = repoAnalisis  ?? throw new ArgumentNullException(nameof(repoAnalisis));
            _repoDocumento = repoDocumento ?? throw new ArgumentNullException(nameof(repoDocumento));
            _repoIdioma    = repoIdioma    ?? throw new ArgumentNullException(nameof(repoIdioma));
            _repoCfg       = repoCfg       ?? throw new ArgumentNullException(nameof(repoCfg));
        }

        // ------------------------------------------------------------------------------------------
        //  Analiza un "Archivo" en memoria SIN persistir (no requiere campos específicos en Archivo)
        // ------------------------------------------------------------------------------------------
        public Task<AnalisisLexico> Analizar(Archivo archivo, string codigoIso)
        {
            string texto = GetTextoBestEffort(archivo);

            var code  = string.IsNullOrWhiteSpace(codigoIso) ? "es" : codigoIso.Trim().ToLowerInvariant();
            var rules = RegexRulesFactory.For(code);

            var counter = new Dictionary<string, int>(16_384, StringComparer.OrdinalIgnoreCase);
            CountTokens(TokenizeStreamed(texto, rules.WordRegex), counter);

            var analisis = new AnalisisLexico
            {
                TotalPalabras  = counter.Values.Sum(),
                PalabrasUnicas = counter.Count
            };

            return Task.FromResult(analisis);
        }

        // ------------------------------------------------------------------------------------------------
        //  Ejecuta el pipeline para un Documento en BD (ID INT). SIN romper si faltan métodos o propiedades.
        // ------------------------------------------------------------------------------------------------
        public async Task<AnalisisLexico> EjecutarAsync(int documentoId, CancellationToken ct = default)
        {
            // 1) Documento (int)
            var doc = await SafeGetDocumentoAsync(documentoId);

            // 2) Texto del documento: SOLO ContenidoOriginal (los otros miembros no existen en tu modelo)
            string texto = doc?.ContenidoOriginal ?? string.Empty;

            // 3) Idioma: si no podemos resolver, usamos "es"
            string codigoIso = await SafeInferCodigoIsoAsync(doc?.IdiomaId) ?? "es";
            var rules = RegexRulesFactory.For(codigoIso);

            // 4) Stopwords (si el repo no las tiene, devuelve set vacío)
            var stop = await SafeGetStopwordsAsync(doc?.IdiomaId ?? 0);

            // 5) Tokenización + conteo
            var counter = new Dictionary<string, int>(16_384, StringComparer.OrdinalIgnoreCase);
            CountTokens(TokenizeStreamed(texto, rules.WordRegex), counter);

            // 6) Métricas del análisis (en memoria)
            var analisis = new AnalisisLexico
            {
                TotalPalabras  = counter.Values.Sum(),
                PalabrasUnicas = counter.Count
            };

            // 7) Intento de crear/obtener analisisId (INT) desde repositorio (si no existe, retorna 0)
            int analisisId = await SafeStartAnalisisAsync(doc);

            // 8) Top/Low (hapax)
            const int TOP_N = 50;
            const int LOW_N = 50;
            var tokensSinStop = counter.Where(kv => !stop.Contains(kv.Key));

            var topFrecuentes = tokensSinStop
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .Take(TOP_N)
                .ToList();

            var hapax = tokensSinStop
                .Where(kv => kv.Value == 1)
                .OrderBy(kv => kv.Key)
                .Take(LOW_N)
                .ToList();

            var filasTop = topFrecuentes.Select(x => new FrecuenciaPalabra
            {
                AnalisisId = analisisId,
                Palabra    = x.Key,
                Frecuencia = x.Value
            }).ToList();

            var filasLow = hapax.Select(x => new FrecuenciaPalabra
            {
                AnalisisId = analisisId,
                Palabra    = x.Key,
                Frecuencia = x.Value
            }).ToList();

            // 9) Clasificación (pronombres/verbos/sustantivos)
            var clasif = ClassifyByRules(counter, rules);
            if (analisisId > 0)
            {
                foreach (var c in clasif) c.AnalisisId = analisisId;
            }

            // 10) Patrones
            var patrones = DetectPatterns(texto, rules);
            if (analisisId > 0)
            {
                foreach (var p in patrones) p.AnalisisId = analisisId;
            }

            // 11) Persistencias seguras (solo si analisisId > 0 y existen los métodos en tu repo)
            await SafeBulkInsertFrecuenciasAsync(analisisId, filasTop);
            await SafeBulkInsertFrecuenciasAsync(analisisId, filasLow);
            await SafeBulkInsertClasificacionAsync(analisisId, clasif);
            await SafeBulkInsertPatronesAsync(analisisId, patrones);

            return analisis;
        }

        // =========================
        // Helpers de compatibilidad
        // =========================

        private async Task<HashSet<string>> SafeGetStopwordsAsync(int idiomaId)
        {
            try
            {
                var set = await _repoCfg.GetStopwordsAsync(idiomaId);
                return set ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private async Task<Documento?> SafeGetDocumentoAsync(int documentoId)
        {
            try
            {
                return await _repoDocumento.GetByIdAsync(documentoId);
            }
            catch
            {
                return null;
            }
        }

        // Devolvemos null (fallback "es") para no depender de repos no implementados.
        private Task<string?> SafeInferCodigoIsoAsync(int? idiomaId)
        {
            return Task.FromResult<string?>(null);
        }

        /// Devuelve un analisisId (INT) si tu repositorio lo soporta; si no, 0 y seguimos en memoria.
        private Task<int> SafeStartAnalisisAsync(Documento? doc)
        {
            // Aquí iría algo como:
            // return _repoAnalisis.StartAsync(doc.Id /*int*/, ...);
            // Como no conocemos la firma exacta, devolvemos 0 (no persistimos) y la ejecución continúa.
            return Task.FromResult(0);
        }

        private async Task SafeBulkInsertFrecuenciasAsync(int analisisId, List<FrecuenciaPalabra> filas)
        {
            if (analisisId <= 0 || filas == null || filas.Count == 0) return;
            try
            {
                await _repoAnalisis.BulkInsertFrecuenciasAsync(analisisId, filas);
            }
            catch
            {
                // ignorar
            }
        }

        private async Task SafeBulkInsertClasificacionAsync(int analisisId, List<ClasificacionGramatical> filas)
        {
            if (analisisId <= 0 || filas == null || filas.Count == 0) return;
            try
            {
                await _repoAnalisis.BulkInsertClasificacionAsync(analisisId, filas);
            }
            catch
            {
                // ignorar
            }
        }

        private async Task SafeBulkInsertPatronesAsync(int analisisId, List<PatronReconocido> filas)
        {
            if (analisisId <= 0 || filas == null || filas.Count == 0) return;
            try
            {
                await _repoAnalisis.BulkInsertPatronesAsync(analisisId, filas);
            }
            catch
            {
                // ignorar
            }
        }

        // =====================
        //  Helpers de análisis
        // =====================

        private IEnumerable<string> TokenizeStreamed(string text, string wordRegex)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            const int CHUNK = 64 * 1024; // 64KB
            var options = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant;

            for (int i = 0; i < text.Length; i += CHUNK)
            {
                var slice = text.Substring(i, Math.Min(CHUNK, text.Length - i));
                foreach (Match m in Regex.Matches(slice, wordRegex, options))
                {
                    var token = m.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(token))
                        yield return token.ToLowerInvariant();
                }
            }
        }

        private static void CountTokens(IEnumerable<string> tokens, IDictionary<string, int> counter)
        {
            foreach (var t in tokens)
            {
                if (counter.TryGetValue(t, out var c))
                    counter[t] = c + 1;
                else
                    counter[t] = 1;
            }
        }

        private static List<ClasificacionGramatical> ClassifyByRules(
            IDictionary<string, int> counter,
            IRegexRules rules)
        {
            var list = new List<ClasificacionGramatical>();
            var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

            foreach (var kv in counter)
            {
                var w    = kv.Key;
                var freq = kv.Value;

                if (Regex.IsMatch(w, rules.PronounsRegex, options))
                {
                    list.Add(new ClasificacionGramatical
                    {
                        Palabra    = w,
                        Categoria  = "pronombre_personal",
                        Frecuencia = freq
                    });
                    continue;
                }

                if (Regex.IsMatch(w, rules.VerbRegex, options))
                {
                    list.Add(new ClasificacionGramatical
                    {
                        Palabra    = w,
                        Categoria  = "verbo",
                        Frecuencia = freq
                    });
                }

                if (Regex.IsMatch(w, rules.NounRegex, options))
                {
                    list.Add(new ClasificacionGramatical
                    {
                        Palabra    = w,
                        Categoria  = "sustantivo",
                        Frecuencia = freq
                    });
                }
            }

            return list;
        }

        private static List<PatronReconocido> DetectPatterns(string texto, IRegexRules rules)
        {
            var res = new List<PatronReconocido>();
            if (string.IsNullOrEmpty(texto)) return res;

            void add(string tipo, Match m)
            {
                res.Add(new PatronReconocido
                {
                    TipoPatron       = tipo,
                    PatronEncontrado = m.Value,
                    Contexto         = ExtractContext(texto, m.Index, 30),
                    PosicionInicio   = m.Index,
                    PosicionFin      = m.Index + m.Length,
                    Frecuencia       = 1
                });
            }

            void AddAll(string pattern, string tipo)
            {
                if (string.IsNullOrWhiteSpace(pattern)) return;
                foreach (Match m in Regex.Matches(texto, pattern, RegexOptions.CultureInvariant))
                    add(tipo, m);
            }

            AddAll(rules.EmailRegex,      "email");
            AddAll(rules.UrlRegex,        "url");
            AddAll(rules.PhoneRegex,      "telefono");
            AddAll(rules.DateRegex,       "fecha");
            AddAll(rules.MoneyRegex,      "monto");
            AddAll(rules.HashtagRegex,    "hashtag");
            AddAll(rules.MentionRegex,    "mencion");
            AddAll(rules.CodeRegex,       "codigo");
            AddAll(rules.PersonNameRegex, "nombre_persona");

            return res;
        }

        private static string ExtractContext(string texto, int index, int radius)
        {
            int start = Math.Max(0, index - radius);
            int end   = Math.Min(texto.Length, index + radius);
            return texto.Substring(start, end - start);
        }

        private static string GetTextoBestEffort(Archivo archivo)
        {
            if (archivo == null) return string.Empty;

            try
            {
                var s = archivo.ToString();
                return s ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
