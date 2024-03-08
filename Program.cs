using FastEndpoints;
using FastEndpoints.Swagger;
using Newtonsoft.Json.Serialization;

var bld = WebApplication.CreateBuilder();
bld.Services
   .AddFastEndpoints()
   .SwaggerDocument(p =>
	p.NewtonsoftSettings = s =>
	{
		s.ContractResolver = new DefaultContractResolver
		{
			NamingStrategy = new CamelCaseNamingStrategy()
		};
	});

var app = bld.Build();
app.UseFastEndpoints()
   .UseSwaggerGen(); //add this
app.Run();

public class PostUser : Endpoint<MyRequest, MyResponse>
{
	public override void Configure()
	{
		Post("/api/user/create");
		AllowAnonymous();
	}

	public override async Task HandleAsync(MyRequest req, CancellationToken ct)
	{
		var id = Guid.NewGuid();
		var succeeded = GlobalDataStore.DataStore.TryAdd(id, req);
		if (!succeeded)
		{
			await SendErrorsAsync(500, ct);
			return;
		}
		var response = MapToResponse(req, id); // Convert entity to MyResponse
		await SendCreatedAtAsync<GetUser>("GetUser", response, cancellation: ct); // Pass response instead of entity
	}


	private MyResponse MapToResponse(MyRequest entity, Guid id) => // Add this method to convert entity to MyResponse
	   new()
	   {
		   Id = id,
		   FullName = $"{entity.FirstName} {entity.LastName}",
		   IsOver18 = entity.Age >= 18
	   };
}

public class GetUsers : EndpointWithoutRequest<List<MyResponse>>
{
	public override void Configure()
	{
		Get("/api/users");
		AllowAnonymous();
	}

	public override async Task HandleAsync(CancellationToken ct)
	{
		var response = GlobalDataStore.DataStore.Values.Select(MapToResponse).ToList();
		await SendOkAsync(response, ct);
	}
	private static MyResponse MapToResponse(MyRequest entity, int location) =>
		new()
		{
			Id = GlobalDataStore.DataStore.Keys.ElementAt(location),
			FullName = $"{entity.FirstName} {entity.LastName}",
			IsOver18 = entity.Age >= 18
		};
}

public class GetUser : Endpoint<GetUserRequest, MyResponse>
{
	public override void Configure()
	{
		Verbs("GET");
		Routes("/api/users/{Id}");
		Description(x => { x.WithName("getUser"); }, clearDefaults: true); // <-------- clearDefaults here breaks the newtonsoft CamelCaseNamingStrategy
		AllowAnonymous();
	}

	public override async Task HandleAsync(GetUserRequest req, CancellationToken ct)
	{
		if (GlobalDataStore.DataStore.TryGetValue(req.Id, out var entity))
		{
			var response = MapToResponse(entity, req.Id);
			await SendOkAsync(response, ct);
		}
		else
		{
			await SendNotFoundAsync(ct);
		}
	}

	private static MyResponse MapToResponse(MyRequest entity, Guid guid) =>
		new()
		{
			Id = guid,
			FullName = $"{entity.FirstName} {entity.LastName}",
			IsOver18 = entity.Age >= 18
		};
}

public class GetUserRequest
{
	public Guid Id { get; set; }
}

public static class GlobalDataStore
{
	public static Dictionary<Guid, MyRequest> DataStore = new();
}

public class MyRequest
{
	public required string FirstName { get; set; }
	public required string LastName { get; set; }
	public int Age { get; set; }
}

public class MyResponse
{
	public Guid Id { get; set; }
	public required string FullName { get; set; }
	public bool IsOver18 { get; set; }
}