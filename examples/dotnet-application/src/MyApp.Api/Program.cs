using MyApp.Shared;

var request = new GreetingRequest("World");
var response = new GreetingResponse($"Hello, {request.Name}!");
Console.WriteLine(response.Message);
