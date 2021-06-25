using GremlinTestApp.Extensions;
using GremlinTestApp.Gremlin.Vertex;
using GremlinTestApp.Interfaces;
using GremlinTestApp.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GremlinTestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<Program>(optional: true, reloadOnChange: true)
                .Build();

            var serviceProvider = new ServiceCollection()
                .AddLogging(options =>
                {
                    options.AddConsole();
                })
                .Configure<GremlinOptions>(configuration.GetSection(GremlinOptions.APPSETTINGS_KEY))
                .AddGremlinClient()
                .AddTransient<IPersonRepository, PersonGremlinRepository>()
                .BuildServiceProvider();

            var personRepository = serviceProvider.GetRequiredService<IPersonRepository>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            while (true)
            {
                logger.LogInformation("1 - GetPersonAsync");
                logger.LogInformation("2 - GetPersonKnowsAsync");
                logger.LogInformation("3 - UpdatePersonAsync");
                logger.LogInformation("4 - CreatePersonAsync");
                logger.LogInformation("5 - GetPeopleAsync");
                logger.LogInformation("6 - DeletePersonAsync");
                logger.LogInformation("X - Exit");

                logger.LogInformation("Enter your desired option:");

                var key = Console.ReadKey();
                if (key.Key.Equals(ConsoleKey.X))
                    break;

                Guid personId;
                PersonVertex? person = null;
                string? personName;
                uint personAge;

                try
                {
                    switch (key.Key)
                    {
                        case ConsoleKey.D1:
                            personId = ReadFromConsole<Guid>("PersonId:");
                            person = await personRepository.GetPersonAsync(personId);

                            if (person != null)
                                logger.LogInformation("Person [{0}] on Partition [{1}] has Name [{2}]", person.Id, person.Pk, person.Name);

                            break;
                        case ConsoleKey.D2:
                            personId = ReadFromConsole<Guid>("PersonId:");
                            person = await personRepository.GetPersonAsync(personId);

                            await foreach (var personKnowsTemp in personRepository.GetPersonKnowsAsync(personId))
                            {
                                logger.LogInformation("Edge [{0}] for Person [{1}] Knows [{2}] Since [{3}]", personKnowsTemp.Id, personKnowsTemp.OutVertex, personKnowsTemp.InVertex, personKnowsTemp.KnownSince);
                            }

                            break;
                        case ConsoleKey.D3:
                            personId = ReadFromConsole<Guid>("PersonId:");
                            personName = ReadFromConsole<string>("PersonName:");

                            var updatedPerson = await personRepository.UpdatePersonAsync(personId, personName);

                            if (updatedPerson != null)
                                logger.LogInformation("Person [{0}] on Partition [{1}] has been updated. New Name [{2}]", updatedPerson.Id, updatedPerson.Pk, updatedPerson.Name);

                            break;
                        case ConsoleKey.D4:
                            var pk = ReadFromConsole<string>("PartitionKey:");
                            personName = ReadFromConsole<string>("PersonName:");
                            personAge = ReadFromConsole<uint>("PersonAge:");

                            Console.WriteLine("Enter your the ids this new person knows (Guid,Guid):");

                            var personKnows = Console.ReadLine();
                            PersonVertex? newPerson = null;
                            if (string.IsNullOrEmpty(personKnows))
                            {
                                newPerson = await personRepository.CreatePersonAsync(pk, personName, personAge);
                            }
                            else
                            {
                                var personKnowsList = new List<Guid>();
                                var personKnowsSplit = personKnows.Split(',', StringSplitOptions.RemoveEmptyEntries);
                                foreach (var personKnowsSplitItem in personKnowsSplit)
                                {
                                    if (Guid.TryParse(personKnowsSplitItem, out var personKnowsId))
                                    {
                                        personKnowsList.Add(personKnowsId);
                                    }
                                    else
                                    {
                                        logger.LogWarning("Cannot add [{0}] to the new person", personKnowsSplitItem);
                                    }
                                }

                                newPerson = await personRepository.CreatePersonAsync(pk, personName, personAge, personKnowsList);
                            }

                            if (newPerson != null)
                            {
                                logger.LogInformation("Person [{0}] on Partition [{1}] has been created with Name [{2}]", newPerson.Id, newPerson.Pk, newPerson.Name);
                            }

                            break;
                        case ConsoleKey.D5:
                            await foreach (var listPerson in personRepository.GetPeopleAsync(0, 4))
                            {
                                logger.LogInformation("Person [{0}] on Partition [{1}] has Name [{2}]", listPerson.Id, listPerson.Pk, listPerson.Name);
                            }

                            break;
                        case ConsoleKey.D6:
                            personId = ReadFromConsole<Guid>("PersonId:");

                            await personRepository.DeletePersonAsync(personId);
                            logger.LogInformation("Person [{0}] has been deleted", personId);

                            break;
                        default:
                            break;
                    }
                }
                catch (BadInputException e)
                {
                    logger.LogWarning(e.Message);
                }
            }
        }

        private static T ReadFromConsole<T>(string message)
        {
            Console.WriteLine(message);
            var input = Console.ReadLine();

            if (string.IsNullOrEmpty(input))
                throw new BadInputException("You need to specify something");

            if (typeof(T) == typeof(Guid))
            {
                if (!Guid.TryParse(input, out var guid))
                    throw new BadInputException("Guid needs to be in the correct format");

                return (T)Convert.ChangeType(guid, typeof(T));
            }

            return (T)Convert.ChangeType(input, typeof(T));
        }
    }

    public class BadInputException : Exception
    {
        public BadInputException(string message) : base(message)
        {
        }
    }
}
