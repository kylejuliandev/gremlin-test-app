using Gremlin.Net.Driver;
using GremlinTestApp.Gremlin.Edges;
using GremlinTestApp.Gremlin.Vertex;
using GremlinTestApp.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GremlinTestApp
{
    public class PersonGremlinRepository : IPersonRepository
    {
        private readonly ILogger<PersonGremlinRepository> _logger;
        private readonly IGremlinClient _client;

        public PersonGremlinRepository(ILogger<PersonGremlinRepository> logger, IGremlinClient client)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<PersonVertex?> GetPersonAsync(Guid id)
        {
            var personQuery = "g.V(_personId)";
            var queryBindings = new Dictionary<string, object>()
            {
                { "_personId", id }
            };

            var result = await ExecuteQueryAsync(personQuery, queryBindings);

            foreach (var person in result)
            {
                return await PersonVertex.MapAsync(person);
            }

            return default;
        }

        public IAsyncEnumerable<PersonKnowsEdge> GetPersonKnowsAsync(Guid id)
        {
            async IAsyncEnumerator<PersonKnowsEdge> ExecuteAsyncEnumerableQuery(CancellationToken cancelToken)
            {
                var personQuery = "g.V(_personId).outE().HasLabel('knows')";
                var queryBindings = new Dictionary<string, object>()
                {
                    { "_personId", id }
                };

                var result = await ExecuteQueryAsync(personQuery, queryBindings);

                foreach (var person in result)
                {
                    yield return await PersonKnowsEdge.MapAsync(person, cancelToken);
                }
            }

            return AsyncEnumerable.Create(ExecuteAsyncEnumerableQuery);
        }
        
        public async Task<PersonVertex?> UpdatePersonAsync(Guid id, string name)
        {
            var personQuery = @"g.V(_personId).property('name', _personName)";
            var queryBindings = new Dictionary<string, object>()
            {
                { "_personId", id },
                { "_personName", name }
            };

            var result = await ExecuteQueryAsync(personQuery, queryBindings);

            foreach (var person in result)
            {
                return await PersonVertex.MapAsync(person);
            }

            return default;
        }

        public async Task<PersonVertex?> CreatePersonAsync(string partitionKey, string name, uint age, IEnumerable<Guid>? knows = null)
        {
            var personQuery = new StringBuilder("g.addV('person').property('name', _personName).property('age', _personAge).property('pk', _personPk)");
            var queryBindings = new Dictionary<string, object>()
            {
                { "_personName", name },
                { "_personAge", age },
                { "_personPk", partitionKey }
            };

            if (knows != null)
            {
                for (int i = 0; i < knows.Count(); i++)
                {
                    personQuery.Append($".addE('knows').to(g.V(_personKnows{i})).property('knownSince', _personKnows{i}Since)");
                    queryBindings.Add($"_personKnows{i}", knows.ElementAt(i));
                    queryBindings.Add($"_personKnows{i}Since", DateTime.UtcNow);
                }

                personQuery.Append(".outV()"); // We want to return the Person Vertex, without this the Edge is returned
            }

            var result = await ExecuteQueryAsync(personQuery.ToString(), queryBindings);

            foreach (var person in result)
            {
                return await PersonVertex.MapAsync(person);
            }

            return default;
        }

        public async Task DeletePersonAsync(Guid id)
        {
            var personQuery = @"g.V(_personId).hasLabel('person').drop()";
            var queryBindings = new Dictionary<string, object>()
            {
                { "_personId", id }
            };

            _ = await ExecuteQueryAsync(personQuery, queryBindings);
        }

        public IAsyncEnumerable<PersonVertex> GetPeopleAsync(int page, int pageSize)
        {
            async IAsyncEnumerator<PersonVertex> ExecuteAsyncEnumerableQuery(CancellationToken cancelToken)
            {
                var peopleQuery = "g.V().hasLabel('person').range(_page, -1).limit(_pageSize)";
                var queryBindings = new Dictionary<string, object>()
                {
                    { "_page", page },
                    { "_pageSize", pageSize }
                };

                var resultSet = await ExecuteQueryAsync(peopleQuery, queryBindings);

                foreach (var person in resultSet)
                {
                    yield return await PersonVertex.MapAsync(person, cancelToken);
                }
            }

            return AsyncEnumerable.Create(ExecuteAsyncEnumerableQuery);
        }

        private async Task<ResultSet<Dictionary<string, object>>> ExecuteQueryAsync(string query, Dictionary<string, object> queryBindings)
        {
            var resultSet = await _client.SubmitAsync<Dictionary<string, object>>(query, queryBindings);

            DisplayStatusAttributes(resultSet.StatusAttributes);

            return resultSet;
        }

        private void DisplayStatusAttributes(IReadOnlyDictionary<string, object> statusAttributes)
        {
            var serverTimeMs = TimeSpan.Zero;
            double requestCharge = 0;
            var serverStatusCode = string.Empty;

            if (statusAttributes.ContainsKey("x-ms-status-code"))
            {
                var item = statusAttributes["x-ms-status-code"].ToString();
                if (!string.IsNullOrEmpty(item))
                {
                    serverStatusCode = item;
                }
            }

            if (statusAttributes.ContainsKey("x-ms-total-request-charge"))
            {
                var item = statusAttributes["x-ms-total-request-charge"].ToString();
                if (double.TryParse(item, out var cost))
                {
                    requestCharge = cost;
                }
            }

            if (statusAttributes.ContainsKey("x-ms-total-server-time-ms"))
            {
                var item = statusAttributes["x-ms-total-server-time-ms"].ToString();
                if (!string.IsNullOrEmpty(item) && double.TryParse(item, out var serverTime))
                {
                    serverTimeMs = TimeSpan.FromMilliseconds(serverTime);
                }
            }

            Console.WriteLine("Query executed in [{0}] using [{1}] RU/s and resulted in a Status [{2}]", serverTimeMs, requestCharge, serverStatusCode);
        }
    }
}
