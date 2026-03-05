using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Options;
using ReasoningAgents.Api.Sessions;
using ReasoningAgents.Application.Services;
using ReasoningAgents.Application.Sessions;
using ReasoningAgents.Domain.Agents;
using ReasoningAgents.Domain.Inputs;
using ReasoningAgents.Domain.Models;
using ReasoningAgents.Infrastructure.Configuration;
using ReasoningAgents.Infrastructure.Foundry;
using ReasoningAgents.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

//Bind configuration settings
builder.Services.Configure<AgentOptions>(
    builder.Configuration.GetSection("Agent"));

// Add services to the container.
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AgentOptions>>().Value);
builder.Services.AddSingleton<IAssessmentSessionStore, MemoryAssessmentSessionStore>();
builder.Services.AddSingleton(sp =>
{
    var opt = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    var credential = new DefaultAzureCredential();
    return new PersistentAgentsClient(opt.ProjectEndpoint, credential);
});
builder.Services
    .AddHttpClient("curator")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    });

builder.Services.AddTransient<IAgentStep<CertificationGoal, string>, FoundryAssessmentPreflightAgent>();

// Assessment concrete agents (for runtime selection)
builder.Services.AddTransient<FoundryAssessmentAgent>();
builder.Services.AddTransient<IAgentStep<AssessmentInput, string>, FoundryExamAssessmentAgent>();

//builder.Services.AddTransient<IAgentStep<AssessmentInput, string>, FoundryAssessmentAgent>();

// Critic / Curator / Planner
builder.Services.AddTransient<IAgentStep<CriticInput, CriticEvaluation>, FoundryCriticAgent>();
builder.Services.AddTransient<IAgentStep<CuratorInput, string>, FoundryCuratorAgent>();
builder.Services.AddTransient<IAgentStep<PlannerInput, string>, FoundryPlannerAgent>();

// Session service (mejor Scoped que Singleton)
builder.Services.AddScoped<IAssessmentSessionService, AssessmentSessionService>();

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCors(p => p.AddPolicy("corsapp", builder =>
{
    builder.WithOrigins("*").AllowAnyMethod().AllowAnyHeader();
}));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCors("corsapp");

app.MapControllers();

app.Run();
