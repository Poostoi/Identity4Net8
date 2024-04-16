using System.Security.Claims;
using Identity4net8.Data;
using Identity4net8.Models;
using IdentityModel;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity4net8;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        var assembly = typeof(Program).Assembly.GetName().Name;
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                               throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString,
                b => b.MigrationsAssembly(assembly)));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();
        
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<IdentityDbContext>();

        builder.Services.AddIdentityServer()
            .AddAspNetIdentity<ApplicationUser>()
            .AddConfigurationStore(options =>
            {
                options.ConfigureDbContext = b => b.UseNpgsql(connectionString,
                    opt => opt.MigrationsAssembly(assembly));
                options.DefaultSchema = "ConfigurationStore";
            })
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = b => b.UseNpgsql(connectionString,
                    opt => opt.MigrationsAssembly(assembly));
                options.DefaultSchema = "OperationalStore";
            }).AddDeveloperSigningCredential();
        builder.Services.AddRazorPages();

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        InitializeDatabase(app);
        EnsureSeedData(app);
        app.UseStaticFiles();
        app.UseRouting();
        app.UseIdentityServer();
        app.UseAuthorization();

        app.MapRazorPages()
            .RequireAuthorization();
        app.UseEndpoints(endpoints => { endpoints.MapDefaultControllerRoute(); });
        return app;
    }

    private static void InitializeDatabase(IApplicationBuilder app)
    {
        using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>()!.CreateScope())
        {
            serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();

            var context = serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
            context.Database.Migrate();

            foreach (var client in Config.Clients)
            {
                if (context.Clients.Any(_ => _.ClientId == client.ClientId))
                {
                    /*context.Clients.Update(client.ToEntity());*/
                }
                else
                    context.Clients.Add(client.ToEntity());
            }

            context.SaveChanges();


            foreach (var resource in Config.IdentityResources)
            {
                if (context.IdentityResources.Any(_ => _.Name == resource.Name))
                {
                    /*context.IdentityResources.Update(resource.ToEntity());*/
                }
                else
                    context.IdentityResources.Add(resource.ToEntity());
            }

            context.SaveChanges();


            foreach (var scope in Config.ApiScopes)
            {
                if (context.ApiScopes.Any(_ => _.Name == scope.Name))
                {
                    /*context.ApiScopes.Update(scope.ToEntity());*/
                }
                else
                    context.ApiScopes.Add(scope.ToEntity());
            }

            context.SaveChanges();
        }
    }

    public static void EnsureSeedData(IApplicationBuilder app)
    {
        var services = new ServiceCollection();
        services.AddLogging();


        using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>()!.CreateScope())
        {
            //var context = scope.ServiceProvider.GetService<IdentityDbContext>();
            //context.Database.Migrate();

            var userMgr = serviceScope.ServiceProvider.GetService<UserManager<ApplicationUser>>();
            ;
            var alice = userMgr.FindByNameAsync("alice").Result;
            if (alice == null)
            {
                alice = new ApplicationUser
                {
                    UserName = "alice",
                    Email = "AliceSmith@email.com",
                    EmailConfirmed = true,
                };
                var result = userMgr.CreateAsync(alice, "Pass123$").Result;
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.First().Description);
                }

                result = userMgr.AddClaimsAsync(alice, new Claim[]
                {
                    new Claim(JwtClaimTypes.Name, "Alice Smith"),
                    new Claim(JwtClaimTypes.GivenName, "Alice"),
                    new Claim(JwtClaimTypes.FamilyName, "Smith"),
                    new Claim(JwtClaimTypes.WebSite, "http://alice.com"),
                }).Result;
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.First().Description);
                }
                // Log.Debug("alice created");
            }
            else
            {
                // Log.Debug("alice already exists");
            }

            var bob = userMgr.FindByNameAsync("bob").Result;
            if (bob == null)
            {
                bob = new ApplicationUser
                {
                    UserName = "bob",
                    Email = "BobSmith@email.com",
                    EmailConfirmed = true
                };
                var result = userMgr.CreateAsync(bob, "Pass123$").Result;
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.First().Description);
                }

                result = userMgr.AddClaimsAsync(bob, new Claim[]
                {
                    new Claim(JwtClaimTypes.Name, "Bob Smith"),
                    new Claim(JwtClaimTypes.GivenName, "Bob"),
                    new Claim(JwtClaimTypes.FamilyName, "Smith"),
                    new Claim(JwtClaimTypes.WebSite, "http://bob.com"),
                    new Claim("location", "somewhere")
                }).Result;
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.First().Description);
                }
                //Log.Debug("bob created");
            }
            else
            {
                //Log.Debug("bob already exists");
            }
        }
    }
}