using MiniValidation;
using DemoMinimalAPI.Data;
using DemoMinimalAPI.Model;
using Microsoft.EntityFrameworkCore;
using NetDevPack.Identity;
using NetDevPack.Identity.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NetDevPack.Identity.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Minimal API Sample",
        Description = "Developed by Pedro Lustosa",
        Contact = new OpenApiContact { Name = "Pedro Lustosa", Email = "pedroeternalss@gmail.com", Url = new Uri ("https://www.linkedin.com/in/pedro-henrique-lustosa-e-silva-29b827144/")},
        License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Insira o token JWT desta maneira: Bearer {seu token}",
        Name = "Authorization",
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddDbContext<MinimalContextDb>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentityEntityFrameworkContextConfiguration(options => 
        options.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
        b => b.MigrationsAssembly("DemoMinimalAPI")));

builder.Services.AddIdentityConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration, "AppSettings");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RemoveProvider", policy => policy.RequireClaim("RemoveProvider"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthConfiguration();
app.UseHttpsRedirection();

app.MapPost("/register", [AllowAnonymous] async (
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IOptions<AppJwtSettings> appJwtSettings,
    RegisterUser registerUser) =>
{
    if (registerUser == null)
        return Results.BadRequest();

    if (!MiniValidator.TryValidate(registerUser, out var errors))
        return Results.ValidationProblem(errors);

    var user = new IdentityUser { UserName = registerUser.Email, Email = registerUser.Email, EmailConfirmed = true };
    var result = await userManager.CreateAsync(user, registerUser.Password);

    if (!result.Succeeded)
        return Results.BadRequest(result.Errors);

    var jwt = new JwtBuilder()
                    .WithUserManager(userManager)
                    .WithJwtSettings(appJwtSettings.Value)
                    .WithEmail(user.Email)
                    .WithJwtClaims()
                    .WithUserClaims()
                    .WithUserRoles()
                    .BuildUserResponse();
    return Results.Ok(jwt);

})  .ProducesValidationProblem()
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("RegisterUser")
    .WithTags("User");

app.MapPost("/login", [AllowAnonymous] async (
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IOptions<AppJwtSettings> appJwtSettings,
    LoginUser loginUser) =>
{
    if(loginUser == null)
        return Results.BadRequest("Usuário não informado");

    if (!MiniValidator.TryValidate(loginUser, out var error))
        return Results.ValidationProblem(error);

    var result = await signInManager.PasswordSignInAsync(loginUser.Email, loginUser.Password, false, true);

    if (result.IsLockedOut)
        return Results.BadRequest("Usuário bloqueado");

    if (!result.Succeeded)
        return Results.BadRequest("Usuário ou senha inválidos");

    var jwt = new JwtBuilder()
                    .WithUserManager(userManager)
                    .WithJwtSettings(appJwtSettings.Value)
                    .WithEmail(loginUser.Email)
                    .WithJwtClaims()
                    .WithUserClaims()
                    .WithUserRoles()
                    .BuildUserResponse();
    return Results.Ok(jwt);

})  .ProducesValidationProblem()
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("LoginUser")
    .WithTags("User");


app.MapGet("/provider", [AllowAnonymous] async 
    (MinimalContextDb context) =>
await context.Providers.ToListAsync())
    .WithName("GetProvider")
    .WithTags("Provider");

app.MapGet("/provider/{id}", async
    (Guid id, MinimalContextDb context) =>
await context.Providers.FindAsync(id)
    is Provider provider 
        ? Results.Ok(provider)
        : Results.NotFound())

    .Produces<Provider>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .WithName("GetProviderById")
    .WithTags("Provider");

app.MapPost("/provider", [Authorize] async (
    MinimalContextDb context, Provider provider) =>
{
    if(!MiniValidator.TryValidate(provider, out var errors))
        return Results.ValidationProblem(errors);

    context.Providers.Add(provider);
    var result = await context.SaveChangesAsync();

    return result > 0
     //? Results.Created($"/provider/{provider.Id}", provider)
         ? Results.CreatedAtRoute("GetProviderById", new { id = provider.Id }, provider)
         : Results.BadRequest("Houve um problema ao salvar");

})  .ProducesValidationProblem()
    .Produces<Provider>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("PostProvider")
    .WithTags("Provider");

app.MapPut("/provider/{id}", [Authorize] async (Guid id, MinimalContextDb context, Provider provider) =>
{
    var providerDB = await context.Providers.AsNoTracking<Provider>()
                                            .FirstOrDefaultAsync(f => f.Id == id); 
    if(providerDB == null) return Results.NotFound();

    if (!MiniValidator.TryValidate(provider, out var errors))
        return Results.ValidationProblem(errors);

    context.Providers.Update(provider);
    var result = await context.SaveChangesAsync();

    return result > 0
        ? Results.NoContent()
        : Results.BadRequest("Houve um problema ao atualizar o Fornecedor");

})  .ProducesValidationProblem()
    .Produces<Provider>(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status400BadRequest)
    .WithName("PutProvider")
    .WithTags("Provider");

app.MapDelete("/provider/{id}", [Authorize] async (Guid id, MinimalContextDb context) =>
{
    var provider = await context.Providers.FindAsync(id);
    if (provider == null) return Results.NotFound();

    context.Providers.Remove(provider);
    var result = await context.SaveChangesAsync();

    return result > 0
        ? Results.NoContent()
        : Results.BadRequest("Houve um problema ao remover o Fornecedor");

})  .Produces<Provider>(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status404NotFound)
    .RequireAuthorization("RemoveProvider")
    .WithName("DeleteProvider")
    .WithTags("Provider");

app.Run();