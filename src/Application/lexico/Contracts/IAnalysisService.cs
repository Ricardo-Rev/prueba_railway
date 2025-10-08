using System.Threading;
using System.Threading.Tasks;
using Lexico.Domain.Entities;

namespace Lexico.Application.Contracts
{
    public interface IAnalysisService
    {
        /// Analiza un archivo en memoria (sin persistir). Seguro para usar en Railway.
        Task<AnalisisLexico> Analizar(Archivo archivo, string codigoIso);

        /// Ejecuta el pipeline para un Documento ya en BD (IDs INT).
        Task<AnalisisLexico> EjecutarAsync(int documentoId, CancellationToken ct = default);
    }
}
