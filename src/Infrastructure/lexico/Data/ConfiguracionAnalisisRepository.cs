// Data/ConfiguracionAnalisisRepository.cs
using System.Text.Json;
using Dapper;
using Lexico.Application.Contracts;

namespace Lexico.Infrastructure.Data;

public class ConfiguracionAnalisisRepository : IConfiguracionAnalisisRepository
{
    private readonly DapperConnectionFactory _factory;
    public ConfiguracionAnalisisRepository(DapperConnectionFactory factory) => _factory = factory;

    public async Task<HashSet<string>> GetStopwordsAsync(int idiomaId)
    {
        const string sql = @"
SELECT configuracion
FROM configuracion_analisis
WHERE idioma_id = @idiomaId AND tipo_configuracion = 'stopwords' AND activo = TRUE
ORDER BY fecha_actualizacion DESC
LIMIT 1;";
        using var con = _factory.Create();
        var json = await con.ExecuteScalarAsync<string?>(sql, new { idiomaId });
        if (string.IsNullOrWhiteSpace(json)) return new HashSet<string>();

        try
        {
            var arr = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return new HashSet<string>(arr.Select(x => x.ToLowerInvariant()));
        }
        catch
        {
            // Si el JSON está mal formado, devolvemos vacío para no romper el análisis
            return new HashSet<string>();
        }
    }

    public async Task<Dictionary<string, string>> GetRegexConfigAsync(int idiomaId)
    {
        const string sql = @"
SELECT configuracion
FROM configuracion_analisis
WHERE idioma_id = @idiomaId AND tipo_configuracion = 'regex' AND activo = TRUE
ORDER BY fecha_actualizacion DESC
LIMIT 1;";
        using var con = _factory.Create();
        var json = await con.ExecuteScalarAsync<string?>(sql, new { idiomaId });
        return ParseOrDefault(json);
    }

    public async Task<Dictionary<string, string>> GetGrammarRulesConfigAsync(int idiomaId)
    {
        const string sql = @"
SELECT configuracion
FROM configuracion_analisis
WHERE idioma_id = @idiomaId AND tipo_configuracion = 'grammar_rules' AND activo = TRUE
ORDER BY fecha_actualizacion DESC
LIMIT 1;";
        using var con = _factory.Create();
        var json = await con.ExecuteScalarAsync<string?>(sql, new { idiomaId });
        return ParseOrDefault(json);
    }

    // -----------------
    // Helpers privados
    // -----------------
    private static Dictionary<string, string> ParseOrDefault(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return dict != null
                ? new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // Si el JSON está mal formado, devolvemos diccionario vacío para no bloquear el deploy
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
