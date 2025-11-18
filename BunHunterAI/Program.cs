using BunHunterAI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<DefectLogSystem.Services.JiraService>((sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>().GetSection("Jira");
    var baseUrl = cfg.GetValue<string>("BaseUrl")?.TrimEnd('/') ?? throw new InvalidOperationException("Jira:BaseUrl not configured");
    var email = cfg.GetValue<string>("Email") ?? "";
    var apiToken = cfg.GetValue<string>("ApiToken") ?? "";

    client.BaseAddress = new Uri(baseUrl);

    // Basic auth for Jira Cloud: email:apiToken -> base64
    var auth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

    // Prefer JSON responses
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
// Register services
builder.Services.AddHttpClient<BunHunterAI.Services.OpenAiClientService>();
builder.Services.AddSingleton<IOpenAiClientService, OpenAiClientService>();

//builder.Services.AddHttpClient<BunHunterAI.Services.SimpleOpenAiService>();
//builder.Services.AddSingleton<BunHunterAI.Services.ISimpleOpenAiService>(sp => sp.GetRequiredService<BunHunterAI.Services.SimpleOpenAiService>());

builder.Services.AddHttpClient<BunHunterAI.Services.JiraClientService>();
builder.Services.AddSingleton<BunHunterAI.Services.IJiraClientService>(sp => sp.GetRequiredService<BunHunterAI.Services.JiraClientService>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
