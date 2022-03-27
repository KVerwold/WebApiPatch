using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebApiPatch.Api.Persons.Model;

namespace WebApiPatch.Api.Persons;

[ApiController]
[Route("api/[controller]")]
public class PersonsController : ControllerBase
{
	private readonly ILogger<PersonsController> _logger;
	private readonly IPersonsManager _manager;

	public PersonsController(ILogger<PersonsController> logger, IPersonsManager manager)
	{
		_logger = logger;
		_manager = manager;
	}

	[HttpGet()]
	public ActionResult<ICollection<Person>> Get()
	{
		return Ok(_manager.Get());
	}

	[HttpGet("{id:int}")]
	public ActionResult<Person> GetById(int id)
	{
		var person = _manager.GetById(id);
		if (person == null)
			return NotFound();
		return Ok(person);
	}

	[HttpPatch("{id:int}")]
	public ActionResult<Person> Patch(int id, JsonElement data)
	{
		var person = _manager.Patch(id, data);
		if (person == null)
			return NotFound();
		return Ok(person);
	}

	[HttpPost()]
	public ActionResult<ICollection<Person>> Post(Person person)
	{
		return Ok(_manager.Post(person));
	}



}
