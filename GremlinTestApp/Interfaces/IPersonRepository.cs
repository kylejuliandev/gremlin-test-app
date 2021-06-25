using GremlinTestApp.Gremlin.Edges;
using GremlinTestApp.Gremlin.Vertex;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GremlinTestApp.Interfaces
{
    public interface IPersonRepository
    {
        Task<PersonVertex?> GetPersonAsync(Guid id);

        IAsyncEnumerable<PersonKnowsEdge> GetPersonKnowsAsync(Guid id);

        Task<PersonVertex?> UpdatePersonAsync(Guid id, string name);

        Task<PersonVertex?> CreatePersonAsync(string partitionKey, string name, uint age, IEnumerable<Guid>? knows = null);

        Task DeletePersonAsync(Guid id);

        IAsyncEnumerable<PersonVertex> GetPeopleAsync(int page, int pageSize);
    }
}
