using System.Text.Json;
using WebApiPatch.Api.Persons.Model;
using WebApiPatch.Extensions;

namespace WebApiPatch.Api.Persons
{

	public interface IPersonsManager
	{
		/// <summary>
		/// Get all Persons
		/// </summary>
		/// <returns>List of Persons</returns>
		public ICollection<Person> Get();

		/// <summary>
		/// Get Person by identifier
		/// </summary>
		/// <param name="id">Identifier</param>
		/// <returns>Person, by identifier. Returns null, if identifier is not found</returns>
		public Person? GetById(int id);

		/// <summary>
		/// Patch Person data for instance matching the identifier
		/// </summary>
		/// <param name="id">Identifier</param>
		/// <param name="data">Data for patching the Persin instance</param>
		/// <returns>Updated Person data. Returns null, if identifier is not found</returns>
		public Person? Patch(int id, JsonElement data);

		/// <summary>
		/// Adding new Person data to the list
		/// </summary>
		/// <param name="person"></param>
		/// <returns></returns>
		public Person Post(Person person);
	}

	public class PersonsManager : IPersonsManager
	{
		private static readonly ICollection<Person> Persons = new List<Person>
		{
			 new Person(1, "Jane", "Doe", "Nyhavn 1", "1051", "Copenhagen K", 23 ),
			 new Person(2, "John", "Doe", "Nyhavn 1", "1051", "Copenhagen K", 25 ),
			 new Person(3, "Hans-Christian", "Andersern", "Bangs Boder 29", "5000", "Odense C", 70 ),
		};

		public ICollection<Person> Get()
		{
			return Persons;
		}

		public Person? GetById(int id)
		{
			return Persons.Where(c => c.Id == id).FirstOrDefault();
		}

		public Person? Patch(int id, JsonElement data)
		{
			var person = GetById(id);
			if (person == null)
				return null;
			// Patch person, but ignore changes to property id, which is the internal value
			data.Patch(person, cfg => cfg.IgnoreProperties = new string[] { "id" });
			return person;
		}

		public Person Post(Person person)
		{
			if (person == null)
				throw new ArgumentNullException(nameof(person));

			if (person.Id != default)
				throw new Exception("Identifier must be 0 for new Person data");

			if (person.Id == default)
			{
				Persons.Add(person);
				person.Id = Persons.Count;
			}
			return person;
		}
	}
}