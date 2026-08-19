using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WFE.Client.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var rabbitMqOptions = new RabbitMqOptions();
builder.Configuration.GetSection("RabbitMq").Bind(rabbitMqOptions);
builder.Services.AddSingleton(rabbitMqOptions);

var wfeApiOptions = new WfeApiOptions();
builder.Configuration.GetSection("WfeApi").Bind(wfeApiOptions);
builder.Services.AddSingleton(wfeApiOptions);

builder.Services.AddSingleton<PacketActivityLog>();

builder.Services.AddHttpClient<IWfeApiClient, WfeApiClient>((sp, http) =>
{
    var options = sp.GetRequiredService<WfeApiOptions>();
    http.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddSingleton<RabbitMqSubscriberService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMqSubscriberService>());

var autoAdvancerOptions = new TestAutoAdvancerOptions();
builder.Configuration.GetSection("TestAutoAdvancer").Bind(autoAdvancerOptions);
builder.Services.AddSingleton(autoAdvancerOptions);
builder.Services.AddHostedService<TestAutoAdvancerService>();

var app = builder.Build();

app.UseRouting();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
