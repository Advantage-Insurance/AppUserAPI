using AppUserAPI.Models;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------
// Configure Windows Authentication
// ---------------------------------------------------------
builder.Services
    .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorization();

// ---------------------------------------------------------
// Register current-user resolver
// ---------------------------------------------------------
builder.Services.AddScoped<ICurrentUserResolver, CurrentUserResolver>();

// Connect to SQL Server.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AVI_COMMON_Database")));

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ---------------------------------------------------------
// Enable authentication and authorization middleware
// ---------------------------------------------------------
app.UseAuthentication();
app.UseAuthorization();


// ---------------------------------------------------------
// Route groups
// ---------------------------------------------------------

// Create a route group for application users.
var applicationUsers = app.MapGroup("/applicationusers");

// Create a route group for applications.
var applications = app.MapGroup("/applications");

// Create a route group for authentication/current-user endpoints.
var auth = app.MapGroup("/auth");

// Create a route group for the current authenticated user.
var me = app.MapGroup("/me")
    .RequireAuthorization();


// ---------------------------------------------------------
// Authentication endpoints
// ---------------------------------------------------------

// GET /auth/whoami
auth.MapGet(
    "/whoami",
    GetCurrentUser)
    .RequireAuthorization();

// GET /auth/currentuser
auth.MapGet(
    "/currentuser",
    GetCurrentSecurityUser)
    .RequireAuthorization();

// GET /me/applications/5
me.MapGet(
    "/applications/{applicationId}",
    GetCurrentUserApplication);

// ---------------------------------------------------------
// Application endpoints
// ---------------------------------------------------------

// GET /applications
applications.MapGet(
    "/",
    GetApplications);

// GET /applications/3/roles
applications.MapGet(
    "/{applicationId}/roles",
    GetApplicationRoles);

// ---------------------------------------------------------
// Application-user endpoints
// ---------------------------------------------------------

// GET /applicationusers
applicationUsers.MapGet(
    "/",
    GetAllApplicationUsers);

// GET /applicationusers/application/5
applicationUsers.MapGet(
    "/application/{applicationId}",
    GetApplicationUsersByApplication);

// GET /applicationusers/application/5/user/10
applicationUsers.MapGet(
    "/application/{applicationId}/user/{userId}",
    GetApplicationUser);

// GET /applicationusers/user/10
applicationUsers.MapGet(
    "/user/{userId}",
    GetApplicationUserByUserId);


// ---------------------------------------------------------
// User-management endpoints
// ---------------------------------------------------------

// GET /users
app.MapGet(
    "/users",
    GetUsers);

// POST /users
app.MapPost(
    "/users",
    CreateUser)
    .RequireAuthorization();

// POST /users/10/reactivate
app.MapPost(
    "/users/{userId}/reactivate",
    ReactivateUser)
    .RequireAuthorization();

// POST /users/10/applications
app.MapPost(
    "/users/{userId}/applications",
    AddUserToApplication)
    .RequireAuthorization();

// POST /users/10/applications/5/deactivate
app.MapPost(
    "/users/{userId}/applications/{applicationId}/deactivate",
    DeactivateUserFromApplication)
    .RequireAuthorization();

// POST /users/10/applications/5/reactivate
app.MapPost(
    "/users/{userId}/applications/{applicationId}/reactivate",
    ReactivateUserFromApplication)
    .RequireAuthorization();

// PUT /users/10/applications/5/role
app.MapPut(
    "/users/{userId}/applications/{applicationId}/role",
    ChangeUserApplicationRole)
    .RequireAuthorization();

// POST /applications/2/roles
applications.MapPost(
    "/{applicationId}/roles",
    CreateApplicationRole)
    .RequireAuthorization();

// PUT /applications/2/roles/19/description
applications.MapPut(
    "/{applicationId}/roles/{roleId}/description",
    UpdateApplicationRoleDescription)
    .RequireAuthorization();

// PUT /applications/2/description
applications.MapPut(
    "/{applicationId}/description",
    UpdateApplicationDescription)
    .RequireAuthorization();

// POST /applications
applications.MapPost(
    "/",
    CreateApplication)
    .RequireAuthorization();

// PUT /users/1/profile
app.MapPut(
    "/users/{userId}/profile",
    UpdateUserProfile)
    .RequireAuthorization();

app.Run();


// ---------------------------------------------------------
// GET the currently authenticated Windows user
// Example:
// GET /auth/whoami
//
// This endpoint shows the raw authenticated identity
// supplied by Windows Authentication.
// ---------------------------------------------------------
static IResult GetCurrentUser(HttpContext context)
{
    var user = context.User;

    return TypedResults.Ok(new
    {
        IsAuthenticated = user.Identity?.IsAuthenticated ?? false,
        AuthenticationType = user.Identity?.AuthenticationType,
        Name = user.Identity?.Name,
        Claims = user.Claims.Select(c => new
        {
            c.Type,
            c.Value
        })
    });
}


// ---------------------------------------------------------
// GET the currently authenticated user resolved against
// Security.Users
// Example:
// GET /auth/currentuser
//
// Windows identity:
// AD\Domingo.DeJesus
//
// resolves to:
//
// Security.Users.UserName = domingo.dejesus
// ---------------------------------------------------------
static async Task<IResult> GetCurrentSecurityUser(
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    CancellationToken cancellationToken)
{
    var user = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    return user is not null
        ? TypedResults.Ok(user)
        : TypedResults.NotFound();
}

// ---------------------------------------------------------
// GET the current authenticated user's access to one
// application
// Example:
// GET /me/applications/3
//
// Resolves the authenticated Windows user against
// Security.Users and determines whether the user has
// active access to the requested application.
// ---------------------------------------------------------
static async Task<IResult> GetCurrentUserApplication(
    int applicationId,
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    AppDbContext db,
    CancellationToken cancellationToken)
{
    var currentUser = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    if (currentUser is null)
    {
        return Results.Problem(
            detail: "The authenticated Windows user is not registered in the User Management system.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    var applicationUser = await db.ApplicationUsers
        .AsNoTracking()
        .FirstOrDefaultAsync(
            x =>
                x.ApplicationID == applicationId &&
                x.UserID == currentUser.UserID,
            cancellationToken);

    if (applicationUser is null)
    {
        return TypedResults.Ok(
            new CurrentUserApplicationAccess
            {
                UserID = currentUser.UserID,
                UserName = currentUser.UserName,
                ApplicationID = applicationId,
                HasAccess = false,
                IsUserActive = currentUser.IsActive,
                IsApplicationAccessActive = false,
                RoleID = null,
                RoleName = null
            });
    }

    var hasAccess =
        applicationUser.IsUserActive &&
        applicationUser.IsApplicationAccessActive;

    return TypedResults.Ok(
        new CurrentUserApplicationAccess
        {
            UserID = applicationUser.UserID,
            UserName = applicationUser.UserName,
            ApplicationID = applicationUser.ApplicationID,
            ApplicationCode = applicationUser.ApplicationCode,
            HasAccess = hasAccess,
            IsUserActive = applicationUser.IsUserActive,
            IsApplicationAccessActive =
                applicationUser.IsApplicationAccessActive,
            RoleID = applicationUser.RoleID,
            RoleName = applicationUser.RoleName
        });
}

// ---------------------------------------------------------
// GET all applications
// ---------------------------------------------------------
static async Task<IResult> GetApplications(AppDbContext db)
{
    var applications = await db.Applications
        .AsNoTracking()
        .OrderBy(x => x.ApplicationName)
        .ToListAsync();

    return TypedResults.Ok(applications);
}

// ---------------------------------------------------------
// GET all roles for one application
// Example:
// GET /applications/3/roles
// ---------------------------------------------------------
static async Task<IResult> GetApplicationRoles(
    int applicationId,
    AppDbContext db)
{
    var roles = await db.Roles
        .AsNoTracking()
        .Where(x => x.ApplicationID == applicationId)
        .OrderBy(x => x.RoleName)
        .ToListAsync();

    return TypedResults.Ok(roles);
}


// ---------------------------------------------------------
// GET all rows from Security.vwApplicationUsers
// ---------------------------------------------------------
static async Task<IResult> GetAllApplicationUsers(AppDbContext db)
{
    var users = await db.ApplicationUsers
        .AsNoTracking()
        .ToListAsync();

    return TypedResults.Ok(users);
}


// ---------------------------------------------------------
// GET all users for one application
// Example:
// GET /applicationusers/application/5
// ---------------------------------------------------------
static async Task<IResult> GetApplicationUsersByApplication(
    int applicationId,
    AppDbContext db)
{
    var users = await db.ApplicationUsers
        .AsNoTracking()
        .Where(x => x.ApplicationID == applicationId)
        .ToListAsync();

    return TypedResults.Ok(users);
}


// ---------------------------------------------------------
// GET one user for one application
// Example:
// GET /applicationusers/application/5/user/10
// ---------------------------------------------------------
static async Task<IResult> GetApplicationUser(
    int applicationId,
    int userId,
    AppDbContext db)
{
    var user = await db.ApplicationUsers
        .AsNoTracking()
        .FirstOrDefaultAsync(x =>
            x.ApplicationID == applicationId &&
            x.UserID == userId);

    return user is not null
        ? TypedResults.Ok(user)
        : TypedResults.NotFound();
}


// ---------------------------------------------------------
// GET all application/role assignments for one user
// Example:
// GET /applicationusers/user/10
//
// A single user may belong to multiple applications
// or have multiple role records, so this returns a list.
// ---------------------------------------------------------
static async Task<IResult> GetApplicationUserByUserId(
    int userId,
    AppDbContext db)
{
    var users = await db.ApplicationUsers
        .AsNoTracking()
        .Where(x => x.UserID == userId)
        .ToListAsync();

    return TypedResults.Ok(users);
}

// ---------------------------------------------------------
// GET all users
// ---------------------------------------------------------
static async Task<IResult> GetUsers(AppDbContext db)
{
    var users = await db.Users
        .AsNoTracking()
        .OrderBy(x => x.LastName)
        .ThenBy(x => x.FirstName)
        .ThenBy(x => x.UserName)
        .ToListAsync();

    return TypedResults.Ok(users);
}

// ---------------------------------------------------------
// POST create a global user
// Calls Security.usp_CreateUser
// Example:
// POST /users
// ---------------------------------------------------------
static async Task<IResult> CreateUser(
    CreateUserRequest request,
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    AppDbContext db,
    CancellationToken cancellationToken)
{
    var currentUser = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    if (currentUser is null)
    {
        return Results.Problem(
            detail: "The authenticated user is not registered in the User Management system.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "Security.usp_CreateUser";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(new SqlParameter(
            "@UserName",
            SqlDbType.NVarChar,
            150)
        {
            Value = request.UserName
        });

        command.Parameters.Add(new SqlParameter(
            "@Email",
            SqlDbType.NVarChar,
            255)
        {
            Value = request.Email
        });

        command.Parameters.Add(new SqlParameter(
            "@PasswordHash",
            SqlDbType.NVarChar,
            100)
        {
            Value = (object?)request.PasswordHash ?? DBNull.Value
        });

        command.Parameters.Add(new SqlParameter(
            "@FirstName",
            SqlDbType.NVarChar,
            100)
        {
            Value = request.FirstName
        });

        command.Parameters.Add(new SqlParameter(
            "@LastName",
            SqlDbType.NVarChar,
            100)
        {
            Value = request.LastName
        });

        command.Parameters.Add(new SqlParameter(
            "@CreatedBy",
            SqlDbType.Int)
        {
            Value = currentUser.UserID
        });

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return Results.Problem(
                "Security.usp_CreateUser did not return a result.");
        }

        var returnCode =
            reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        int? userId = null;

        var userIdOrdinal =
            reader.GetOrdinal("UserID");

        if (!reader.IsDBNull(userIdOrdinal))
        {
            userId =
                reader.GetInt32(userIdOrdinal);
        }

        return returnCode switch
        {
            0 => Results.Created(
                $"/users/{userId}",
                new
                {
                    ReturnCode = returnCode,
                    UserID = userId,
                    Status = "Created",
                    Message = "User successfully created.",
                    RecommendedAction = "AddToApplication"
                }),

            1 => Results.Conflict(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Status = "ActiveUserExists",
                Message = "UserName already exists and the user is active.",
                RecommendedAction = "ConfirmAddToApplication"
            }),

            2 => Results.Conflict(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Status = "InactiveUserExists",
                Message = "UserName already exists and the user is inactive.",
                RecommendedAction = "ReactivateUser"
            }),

            3 => Results.Conflict(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Status = "ActiveUserExists",
                Message = "Email already exists and the user is active.",
                RecommendedAction = "ConfirmAddToApplication"
            }),

            4 => Results.Conflict(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Status = "InactiveUserExists",
                Message = "Email already exists and the user is inactive.",
                RecommendedAction = "ReactivateUser"
            }),

            5 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Status = "ValidationFailed",
                Message = "User name, email, first name, and last name are required.",
                RecommendedAction = "CorrectInput"
            }),

            -1 => Results.Problem(
                detail:
                    "An unexpected SQL Server error occurred while creating the user.",
                statusCode:
                    StatusCodes.Status500InternalServerError),

            _ => Results.Problem(
                $"Unexpected return code from Security.usp_CreateUser: {returnCode}")
        };
    }
    finally
    {
        await connection.CloseAsync();
    }
}


// ---------------------------------------------------------
// POST reactivate an existing global user
// Calls Security.usp_ReactivateUser
// Example:
// POST /users/10/reactivate
// ---------------------------------------------------------
static async Task<IResult> ReactivateUser(
    int userId,
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    AppDbContext db,
    CancellationToken cancellationToken)
{
    var currentUser = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    if (currentUser is null)
    {
        return Results.Problem(
            detail: "The authenticated user is not registered in the User Management system.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();

        command.CommandText =
            "Security.usp_ReactivateUser";

        command.CommandType =
            CommandType.StoredProcedure;

        command.Parameters.Add(
            new SqlParameter("@UserID", SqlDbType.Int)
            {
                Value = userId
            });

        command.Parameters.Add(
            new SqlParameter("@ModifiedBy", SqlDbType.Int)
            {
                Value = currentUser.UserID
            });

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return Results.Problem(
                "Security.usp_ReactivateUser did not return a result.");
        }

        var returnCode =
            reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        return returnCode switch
        {
            0 => Results.Ok(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Message = "User successfully reactivated.",
                RecommendedAction = "AddToApplication"
            }),

            1 => Results.Conflict(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Message = "User is already active."
            }),

            2 => Results.NotFound(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Message = "User does not exist."
            }),

            3 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Message = "ModifiedBy user does not exist."
            }),

            4 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Message = "ModifiedBy user is inactive."
            }),

            -1 => Results.Problem(
                detail:
                    "An unexpected SQL Server error occurred while reactivating the user.",
                statusCode:
                    StatusCodes.Status500InternalServerError),

            _ => Results.Problem(
                $"Unexpected return code from Security.usp_ReactivateUser: {returnCode}")
        };
    }
    finally
    {
        await connection.CloseAsync();
    }
}


// ---------------------------------------------------------
// POST grant a user access to an application
// Calls Security.usp_AddUserToApplication
// Example:
// POST /users/10/applications
// ---------------------------------------------------------
static async Task<IResult> AddUserToApplication(
    int userId,
    AddUserToApplicationRequest request,
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    AppDbContext db,
    CancellationToken cancellationToken)
{
    var currentUser = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    if (currentUser is null)
    {
        return Results.Problem(
            detail: "The authenticated user is not registered in the User Management system.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();

        command.CommandText =
            "Security.usp_AddUserToApplication";

        command.CommandType =
            CommandType.StoredProcedure;

        command.Parameters.Add(
            new SqlParameter("@UserID", SqlDbType.Int)
            {
                Value = userId
            });

        command.Parameters.Add(
            new SqlParameter("@ApplicationID", SqlDbType.Int)
            {
                Value = request.ApplicationID
            });

        command.Parameters.Add(
            new SqlParameter("@RoleID", SqlDbType.Int)
            {
                Value = request.RoleID
            });

        command.Parameters.Add(
            new SqlParameter("@CreatedBy", SqlDbType.Int)
            {
                Value = currentUser.UserID
            });

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return Results.Problem(
                "Security.usp_AddUserToApplication did not return a result.");
        }

        var returnCode =
            reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        return returnCode switch
        {
            0 => Results.Ok(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = request.ApplicationID,
                RoleID = request.RoleID,
                Message =
                    "User successfully added to the application."
            }),

            1 => Results.Conflict(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = request.ApplicationID,
                Message =
                    "User already has active access to the application."
            }),

            2 => Results.Ok(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = request.ApplicationID,
                RoleID = request.RoleID,
                Message =
                    "User application access was reactivated successfully."
            }),

            3 => Results.NotFound(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Message = "User does not exist."
            }),

            4 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Message =
                    "User is inactive and cannot be added to an application."
            }),

            5 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                ApplicationID = request.ApplicationID,
                Message =
                    "Application does not exist or is inactive."
            }),

            6 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                ApplicationID = request.ApplicationID,
                RoleID = request.RoleID,
                Message =
                    "Role does not exist for this application."
            }),

            7 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                Message = "CreatedBy user does not exist."
            }),

            8 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                Message = "CreatedBy user is inactive."
            }),

            -1 => Results.Problem(
                detail:
                    "An unexpected SQL Server error occurred while adding the user to the application.",
                statusCode:
                    StatusCodes.Status500InternalServerError),

            _ => Results.Problem(
                $"Unexpected return code from Security.usp_AddUserToApplication: {returnCode}")
        };
    }
    finally
    {
        await connection.CloseAsync();
    }
}


// ---------------------------------------------------------
// POST deactivate a user's access to an application
// Calls Security.usp_DeactivateUserFromApplication
// Example:
// POST /users/10/applications/5/deactivate
// ---------------------------------------------------------
static async Task<IResult> DeactivateUserFromApplication(
    int userId,
    int applicationId,
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    AppDbContext db,
    CancellationToken cancellationToken)
{
    var currentUser = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    if (currentUser is null)
    {
        return Results.Problem(
            detail: "The authenticated user is not registered in the User Management system.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            "Security.usp_DeactivateUserFromApplication";

        command.CommandType =
            CommandType.StoredProcedure;

        command.Parameters.Add(
            new SqlParameter("@UserID", SqlDbType.Int)
            {
                Value = userId
            });

        command.Parameters.Add(
            new SqlParameter("@ApplicationID", SqlDbType.Int)
            {
                Value = applicationId
            });

        command.Parameters.Add(
            new SqlParameter("@ModifiedBy", SqlDbType.Int)
            {
                Value = currentUser.UserID
            });

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return Results.Problem(
                "Security.usp_DeactivateUserFromApplication did not return a result.");
        }

        var returnCode =
            reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        var messageOrdinal =
            reader.GetOrdinal("Message");

        var message =
            reader.IsDBNull(messageOrdinal)
                ? string.Empty
                : reader.GetString(messageOrdinal);

        return returnCode switch
        {
            0 => Results.Ok(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = applicationId,
                Message = message
            }),

            1 => Results.NotFound(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = applicationId,
                Message = message
            }),

            2 => Results.Conflict(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = applicationId,
                Message = message
            }),

            3 => Results.NotFound(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Message = message
            }),

            4 => Results.Problem(
                detail: message,
                statusCode:
                    StatusCodes.Status500InternalServerError),

            _ => Results.Problem(
                $"Unexpected return code from Security.usp_DeactivateUserFromApplication: {returnCode}")
        };
    }
    finally
    {
        await connection.CloseAsync();
    }
}


// ---------------------------------------------------------
// POST reactivate a user's access to an application
// Calls Security.usp_ReactivateUserFromApplication
// Example:
// POST /users/10/applications/5/reactivate
// ---------------------------------------------------------
static async Task<IResult> ReactivateUserFromApplication(
    int userId,
    int applicationId,
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    AppDbContext db,
    CancellationToken cancellationToken)
{
    var currentUser = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    if (currentUser is null)
    {
        return Results.Problem(
            detail: "The authenticated user is not registered in the User Management system.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    var connection =
        db.Database.GetDbConnection();

    await connection.OpenAsync();

    try
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            "Security.usp_ReactivateUserFromApplication";

        command.CommandType =
            CommandType.StoredProcedure;

        command.Parameters.Add(
            new SqlParameter("@UserID", SqlDbType.Int)
            {
                Value = userId
            });

        command.Parameters.Add(
            new SqlParameter("@ApplicationID", SqlDbType.Int)
            {
                Value = applicationId
            });

        command.Parameters.Add(
            new SqlParameter("@ModifiedBy", SqlDbType.Int)
            {
                Value = currentUser.UserID
            });

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return Results.Problem(
                "Security.usp_ReactivateUserFromApplication did not return a result.");
        }

        var returnCode =
            reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        var messageOrdinal =
            reader.GetOrdinal("Message");

        var message =
            reader.IsDBNull(messageOrdinal)
                ? string.Empty
                : reader.GetString(messageOrdinal);

        return returnCode switch
        {
            0 => Results.Ok(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = applicationId,
                Message = message
            }),

            1 => Results.NotFound(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = applicationId,
                Message = message
            }),

            2 => Results.Conflict(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = applicationId,
                Message = message
            }),

            3 => Results.NotFound(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Message = message
            }),

            4 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Message = message
            }),

            5 => Results.Problem(
                detail: message,
                statusCode:
                    StatusCodes.Status500InternalServerError),

            _ => Results.Problem(
                $"Unexpected return code from Security.usp_ReactivateUserFromApplication: {returnCode}")
        };
    }
    finally
    {
        await connection.CloseAsync();
    }
}


// ---------------------------------------------------------
// PUT change a user's role within an application
// Calls Security.usp_ChangeUserApplicationRole
// Example:
// PUT /users/10/applications/5/role
// ---------------------------------------------------------
static async Task<IResult> ChangeUserApplicationRole(
    int userId,
    int applicationId,
    ChangeUserApplicationRoleRequest request,
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    AppDbContext db,
    CancellationToken cancellationToken)
{
    var currentUser = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    if (currentUser is null)
    {
        return Results.Problem(
            detail: "The authenticated user is not registered in the User Management system.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    var connection =
        db.Database.GetDbConnection();

    await connection.OpenAsync();

    try
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            "Security.usp_ChangeUserApplicationRole";

        command.CommandType =
            CommandType.StoredProcedure;

        command.Parameters.Add(
            new SqlParameter("@UserID", SqlDbType.Int)
            {
                Value = userId
            });

        command.Parameters.Add(
            new SqlParameter("@ApplicationID", SqlDbType.Int)
            {
                Value = applicationId
            });

        command.Parameters.Add(
            new SqlParameter("@RoleID", SqlDbType.Int)
            {
                Value = request.RoleID
            });

        command.Parameters.Add(
            new SqlParameter("@ModifiedBy", SqlDbType.Int)
            {
                Value = currentUser.UserID
            });

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return Results.Problem(
                "Security.usp_ChangeUserApplicationRole did not return a result.");
        }

        var returnCode =
            reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        var messageOrdinal =
            reader.GetOrdinal("Message");

        var message =
            reader.IsDBNull(messageOrdinal)
                ? string.Empty
                : reader.GetString(messageOrdinal);

        return returnCode switch
        {
            0 => Results.Ok(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = applicationId,
                RoleID = request.RoleID,
                Message = message
            }),

            1 => Results.NotFound(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Message = message
            }),

            2 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                Message = message
            }),

            3 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                ApplicationID = applicationId,
                Message = message
            }),

            4 => Results.NotFound(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = applicationId,
                Message = message
            }),

            5 => Results.Conflict(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = applicationId,
                Message = message
            }),

            6 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                ApplicationID = applicationId,
                RoleID = request.RoleID,
                Message = message
            }),

            7 => Results.Conflict(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = applicationId,
                RoleID = request.RoleID,
                Message = message
            }),

            8 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                Message = message
            }),

            9 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                Message = message
            }),

            -1 => Results.Problem(
                detail: message,
                statusCode:
                    StatusCodes.Status500InternalServerError),

            _ => Results.Problem(
                $"Unexpected return code from Security.usp_ChangeUserApplicationRole: {returnCode}")
        };
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task<IResult> CreateApplicationRole(
    int applicationId,
    CreateApplicationRoleRequest request,
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    AppDbContext db,
    CancellationToken cancellationToken)
{
    var currentUser = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    if (currentUser is null)
    {
        return Results.Problem(
            detail:
                "The authenticated user is not registered in the User Management system.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    var connection = db.Database.GetDbConnection();

    await connection.OpenAsync(cancellationToken);

    try
    {
        await using var command = connection.CreateCommand();

        command.CommandText = "Security.usp_CreateApplicationRole";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(
            new SqlParameter("@ApplicationID", SqlDbType.Int)
            {
                Value = applicationId
            });

        command.Parameters.Add(
            new SqlParameter("@RoleName", SqlDbType.NVarChar, -1)
            {
                Value = (object?)request.RoleName ?? DBNull.Value
            });

        command.Parameters.Add(
            new SqlParameter("@Description", SqlDbType.NVarChar, -1)
            {
                Value = (object?)request.Description ?? DBNull.Value
            });

        // Audit identity comes from Windows authentication.
        command.Parameters.Add(
            new SqlParameter("@CreatedBy", SqlDbType.Int)
            {
                Value = currentUser.UserID
            });

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return Results.Problem(
                "Security.usp_CreateApplicationRole did not return a result.");
        }

        var returnCode =
            reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        var roleIdOrdinal = reader.GetOrdinal("RoleID");

        int? roleId = reader.IsDBNull(roleIdOrdinal)
            ? null
            : reader.GetInt32(roleIdOrdinal);

        var message =
            reader.GetString(reader.GetOrdinal("Message"));

        var result = new
        {
            ReturnCode = returnCode,
            RoleID = roleId,
            ApplicationID = applicationId,
            Message = message
        };

        return returnCode switch
        {
            0 => Results.Created(
                $"/applications/{applicationId}/roles",
                result),

            1 => Results.BadRequest(result),
            2 => Results.BadRequest(result),
            3 => Results.BadRequest(result),
            4 => Results.BadRequest(result),
            5 => Results.Conflict(result),

            6 => Results.Problem(
                detail: message,
                statusCode: StatusCodes.Status403Forbidden),

            _ => Results.Problem(
                detail: "Unable to create the application role.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task<IResult> UpdateApplicationRoleDescription(
    int applicationId,
    int roleId,
    UpdateApplicationRoleDescriptionRequest request,
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    AppDbContext db,
    CancellationToken cancellationToken)
{
    var currentUser = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    if (currentUser is null)
    {
        return Results.Problem(
            detail:
                "The authenticated user is not registered in the User Management system.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    var connection = db.Database.GetDbConnection();

    await connection.OpenAsync(cancellationToken);

    try
    {
        await using var command = connection.CreateCommand();

        command.CommandText =
            "Security.usp_UpdateApplicationRoleDescription";

        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(
            new SqlParameter("@ApplicationID", SqlDbType.Int)
            {
                Value = applicationId
            });

        command.Parameters.Add(
            new SqlParameter("@RoleID", SqlDbType.Int)
            {
                Value = roleId
            });

        command.Parameters.Add(
            new SqlParameter("@Description", SqlDbType.NVarChar, -1)
            {
                Value = (object?)request.Description ?? DBNull.Value
            });

        // Audit identity comes from Windows authentication.
        command.Parameters.Add(
            new SqlParameter("@ModifiedBy", SqlDbType.Int)
            {
                Value = currentUser.UserID
            });

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return Results.Problem(
                "Security.usp_UpdateApplicationRoleDescription did not return a result.");
        }

        var returnCode =
            reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        var message =
            reader.GetString(reader.GetOrdinal("Message"));

        var result = new
        {
            ReturnCode = returnCode,
            RoleID = roleId,
            ApplicationID = applicationId,
            Message = message
        };

        return returnCode switch
        {
            0 => Results.Ok(result),
            1 => Results.NotFound(result),
            2 => Results.BadRequest(result),

            3 => Results.Problem(
                detail: message,
                statusCode: StatusCodes.Status403Forbidden),

            // Saving an unchanged description is a successful no-op.
            4 => Results.Ok(result),

            _ => Results.Problem(
                detail: "Unable to update the role description.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task<IResult> UpdateApplicationDescription(
    int applicationId,
    UpdateApplicationDescriptionRequest request,
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    AppDbContext db,
    CancellationToken cancellationToken)
{
    var currentUser = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    if (currentUser is null)
    {
        return Results.Problem(
            detail:
                "The authenticated user is not registered in the User Management system.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    var connection = db.Database.GetDbConnection();

    await connection.OpenAsync(cancellationToken);

    try
    {
        await using var command = connection.CreateCommand();

        command.CommandText =
            "Security.usp_UpdateApplicationDescription";

        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(
            new SqlParameter("@ApplicationID", SqlDbType.Int)
            {
                Value = applicationId
            });

        command.Parameters.Add(
            new SqlParameter("@Description", SqlDbType.NVarChar, -1)
            {
                Value = (object?)request.Description ?? DBNull.Value
            });

        command.Parameters.Add(
            new SqlParameter("@ModifiedBy", SqlDbType.Int)
            {
                Value = currentUser.UserID
            });

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return Results.Problem(
                "Security.usp_UpdateApplicationDescription did not return a result.");
        }

        var returnCode =
            reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        var message =
            reader.GetString(reader.GetOrdinal("Message"));

        var result = new
        {
            ReturnCode = returnCode,
            ApplicationID = applicationId,
            Message = message
        };

        return returnCode switch
        {
            0 => Results.Ok(result),
            1 => Results.NotFound(result),
            2 => Results.BadRequest(result),

            3 => Results.Problem(
                detail: message,
                statusCode: StatusCodes.Status403Forbidden),

            4 => Results.Ok(result),

            _ => Results.Problem(
                detail: "Unable to update the application description.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task<IResult> CreateApplication(
    CreateApplicationRequest request,
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    AppDbContext db,
    CancellationToken cancellationToken)
{
    var currentUser = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    if (currentUser is null)
    {
        return Results.Problem(
            detail:
                "The authenticated user is not registered in the User Management system.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    var connection = db.Database.GetDbConnection();

    await connection.OpenAsync(cancellationToken);

    try
    {
        await using var command = connection.CreateCommand();

        command.CommandText = "Security.usp_CreateApplication";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(
            new SqlParameter("@ApplicationCode", SqlDbType.NVarChar, -1)
            {
                Value = (object?)request.ApplicationCode ?? DBNull.Value
            });

        command.Parameters.Add(
            new SqlParameter("@ApplicationName", SqlDbType.NVarChar, -1)
            {
                Value = (object?)request.ApplicationName ?? DBNull.Value
            });

        command.Parameters.Add(
            new SqlParameter("@Description", SqlDbType.NVarChar, -1)
            {
                Value = (object?)request.Description ?? DBNull.Value
            });

        // Audit identity comes from Windows authentication.
        command.Parameters.Add(
            new SqlParameter("@CreatedBy", SqlDbType.Int)
            {
                Value = currentUser.UserID
            });

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return Results.Problem(
                "Security.usp_CreateApplication did not return a result.");
        }

        var returnCode =
            reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        var applicationIdOrdinal =
            reader.GetOrdinal("ApplicationID");

        int? applicationId = reader.IsDBNull(applicationIdOrdinal)
            ? null
            : reader.GetInt32(applicationIdOrdinal);

        var message =
            reader.GetString(reader.GetOrdinal("Message"));

        var result = new
        {
            ReturnCode = returnCode,
            ApplicationID = applicationId,
            Message = message
        };

        return returnCode switch
        {
            0 => Results.Created("/applications", result),

            1 => Results.BadRequest(result),
            2 => Results.BadRequest(result),
            3 => Results.BadRequest(result),
            4 => Results.BadRequest(result),
            5 => Results.BadRequest(result),

            6 => Results.Conflict(result),
            7 => Results.Conflict(result),

            8 => Results.Problem(
                detail: message,
                statusCode: StatusCodes.Status403Forbidden),

            _ => Results.Problem(
                detail: "Unable to create the application.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
    finally
    {
        await connection.CloseAsync();
    }
}

static async Task<IResult> UpdateUserProfile(
    int userId,
    UpdateUserProfileRequest request,
    HttpContext context,
    ICurrentUserResolver currentUserResolver,
    AppDbContext db,
    CancellationToken cancellationToken)
{
    var currentUser = await currentUserResolver.GetCurrentUserAsync(
        context.User,
        cancellationToken);

    if (currentUser is null)
    {
        return Results.Problem(
            detail:
                "The authenticated user is not registered in the User Management system.",
            statusCode: StatusCodes.Status403Forbidden);
    }

    var email = request.Email?.Trim();

    // Required-field and length validation remain in the procedure.
    // The API additionally validates the format of a supplied email.
    if (!string.IsNullOrWhiteSpace(email))
    {
        var emailValidator =
            new System.ComponentModel.DataAnnotations.EmailAddressAttribute();

        if (!emailValidator.IsValid(email))
        {
            return Results.BadRequest(new
            {
                ReturnCode = 11,
                UserID = userId,
                Message = "Enter a valid email address."
            });
        }
    }

    var connection = db.Database.GetDbConnection();

    await connection.OpenAsync(cancellationToken);

    try
    {
        await using var command = connection.CreateCommand();

        command.CommandText = "Security.usp_UpdateUserProfile";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(
            new SqlParameter("@UserID", SqlDbType.Int)
            {
                Value = userId
            });

        command.Parameters.Add(
            new SqlParameter("@FirstName", SqlDbType.NVarChar, -1)
            {
                Value = (object?)request.FirstName ?? DBNull.Value
            });

        command.Parameters.Add(
            new SqlParameter("@LastName", SqlDbType.NVarChar, -1)
            {
                Value = (object?)request.LastName ?? DBNull.Value
            });

        command.Parameters.Add(
            new SqlParameter("@Email", SqlDbType.NVarChar, -1)
            {
                Value = (object?)email ?? DBNull.Value
            });

        command.Parameters.Add(
            new SqlParameter("@ModifiedBy", SqlDbType.Int)
            {
                Value = currentUser.UserID
            });

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return Results.Problem(
                "Security.usp_UpdateUserProfile did not return a result.");
        }

        var returnCode =
            reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        var message =
            reader.GetString(reader.GetOrdinal("Message"));

        var result = new
        {
            ReturnCode = returnCode,
            UserID = userId,
            Message = message
        };

        return returnCode switch
        {
            0 => Results.Ok(result),
            1 => Results.NotFound(result),

            2 => Results.BadRequest(result),
            3 => Results.BadRequest(result),
            4 => Results.BadRequest(result),
            5 => Results.BadRequest(result),
            6 => Results.BadRequest(result),
            7 => Results.BadRequest(result),

            8 => Results.Conflict(result),

            9 => Results.Problem(
                detail: message,
                statusCode: StatusCodes.Status403Forbidden),

            10 => Results.Ok(result),

            _ => Results.Problem(
                detail: "Unable to update the user profile.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
    finally
    {
        await connection.CloseAsync();
    }
}