using System.Threading;
using System.Threading.Tasks;
using Lexico.Domain.Entities;

namespace Lexico.Application.Contracts
{
    /// Servicio específico para subir/insertar documentos (inserta y devuelve ID).
    public interface IUploadDocumentoService
    {
        Task<int> SubirAsync(Documento doc, CancellationToken ct = default);
    }
}
