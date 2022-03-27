
namespace WebApiPatch.Api.Persons.Model
{
	public class Person
	{
		public Person(int id, string firstname, string lastname, string street, string postalCode, string city, int age)
		{
			this.Id = id;
			this.Firstname = firstname;
			this.Lastname = lastname;
			this.PostalCode = postalCode;
			this.Street = street;
			this.City = city;
			this.Age = age;

		}
		public int Id { get; set; }
		public string Firstname { get; set; }
		public string Lastname { get; set; }
		public string Street { get; set; }
		public string PostalCode { get; set; }
		public string City { get; set; }
		public int Age { get; set; }


	}
}