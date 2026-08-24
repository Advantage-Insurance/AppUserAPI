using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Connect to SQL Server instead of the in-memory Todo database.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AVI_COMMON_Database")));

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Create a route group for application users.
var applicationUsers = app.MapGroup("/applicationusers");

// GET /applicationusers
applicationUsers.MapGet("/", GetAllApplicationUsers);

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

// POST /users
app.MapPost("/users", CreateUser);

// POST /users/10/reactivate
app.MapPost("/users/{userId}/reactivate", ReactivateUser);

// POST /users/10/applications
app.MapPost("/users/{userId}/applications", AddUserToApplication);

// POST /users/10/applications/5/deactivate
app.MapPost(
    "/users/{userId}/applications/{applicationId}/deactivate",
    DeactivateUserFromApplication);

// POST /users/10/applications/5/reactivate
app.MapPost(
    "/users/{userId}/applications/{applicationId}/reactivate",
    ReactivateUserFromApplication);

// PUT /users/10/applications/5/role
app.MapPut(
    "/users/{userId}/applications/{applicationId}/role",
    ChangeUserApplicationRole);

app.Run();


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
// POST create a global user
// Calls Security.usp_CreateUser
// Example:
// POST /users
// ---------------------------------------------------------
static async Task<IResult> CreateUser(
    CreateUserRequest request,
    AppDbContext db)
{
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "Security.usp_CreateUser";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(new SqlParameter("@UserName", SqlDbType.NVarChar, 150)
        {
            Value = request.UserName
        });

        command.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 255)
        {
            Value = request.Email
        });

        command.Parameters.Add(new SqlParameter("@PasswordHash", SqlDbType.NVarChar, 100)
        {
            Value = (object?)request.PasswordHash ?? DBNull.Value
        });

        command.Parameters.Add(new SqlParameter("@FirstName", SqlDbType.NVarChar, 100)
        {
            Value = request.FirstName
        });

        command.Parameters.Add(new SqlParameter("@LastName", SqlDbType.NVarChar, 100)
        {
            Value = request.LastName
        });

        command.Parameters.Add(new SqlParameter("@CreatedBy", SqlDbType.Int)
        {
            Value = (object?)request.CreatedBy ?? DBNull.Value
        });

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return Results.Problem(
                "Security.usp_CreateUser did not return a result.");
        }

        var returnCode = reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        int? userId = null;
        var userIdOrdinal = reader.GetOrdinal("UserID");

        if (!reader.IsDBNull(userIdOrdinal))
        {
            userId = reader.GetInt32(userIdOrdinal);
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

            -1 => Results.Problem(
                detail: "An unexpected SQL Server error occurred while creating the user.",
                statusCode: StatusCodes.Status500InternalServerError),

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
    ReactivateUserRequest request,
    AppDbContext db)
{
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "Security.usp_ReactivateUser";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(new SqlParameter("@UserID", SqlDbType.Int)
        {
            Value = userId
        });

        command.Parameters.Add(new SqlParameter("@ModifiedBy", SqlDbType.Int)
        {
            Value = request.ModifiedBy
        });

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return Results.Problem(
                "Security.usp_ReactivateUser did not return a result.");
        }

        var returnCode = reader.GetInt32(reader.GetOrdinal("ReturnCode"));

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
                detail: "An unexpected SQL Server error occurred while reactivating the user.",
                statusCode: StatusCodes.Status500InternalServerError),

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
    AppDbContext db)
{
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "Security.usp_AddUserToApplication";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(new SqlParameter("@UserID", SqlDbType.Int)
        {
            Value = userId
        });

        command.Parameters.Add(new SqlParameter("@ApplicationID", SqlDbType.Int)
        {
            Value = request.ApplicationID
        });

        command.Parameters.Add(new SqlParameter("@RoleID", SqlDbType.Int)
        {
            Value = request.RoleID
        });

        command.Parameters.Add(new SqlParameter("@CreatedBy", SqlDbType.Int)
        {
            Value = request.CreatedBy
        });

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return Results.Problem(
                "Security.usp_AddUserToApplication did not return a result.");
        }

        var returnCode = reader.GetInt32(reader.GetOrdinal("ReturnCode"));

        return returnCode switch
        {
            0 => Results.Ok(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = request.ApplicationID,
                RoleID = request.RoleID,
                Message = "User successfully added to the application."
            }),

            1 => Results.Conflict(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = request.ApplicationID,
                Message = "User already has active access to the application."
            }),

            2 => Results.Ok(new
            {
                ReturnCode = returnCode,
                UserID = userId,
                ApplicationID = request.ApplicationID,
                RoleID = request.RoleID,
                Message = "User application access was reactivated successfully."
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
                Message = "User is inactive and cannot be added to an application."
            }),

            5 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                ApplicationID = request.ApplicationID,
                Message = "Application does not exist or is inactive."
            }),

            6 => Results.BadRequest(new
            {
                ReturnCode = returnCode,
                ApplicationID = request.ApplicationID,
                RoleID = request.RoleID,
                Message = "Role does not exist for this application."
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
                detail: "An unexpected SQL Server error occurred while adding the user to the application.",
                statusCode: StatusCodes.Status500InternalServerError),

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
    ModifyApplicationAccessRequest request,
    AppDbContext db)
{
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "Security.usp_DeactivateUserFromApplication";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(new SqlParameter("@UserID", SqlDbType.Int)
        {
            Value = userId
        });

        command.Parameters.Add(new SqlParameter("@ApplicationID", SqlDbType.Int)
        {
            Value = applicationId
        });

        command.Parameters.Add(new SqlParameter("@ModifiedBy", SqlDbType.Int)
        {
            Value = (object?)request.ModifiedBy ?? DBNull.Value
        });

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return Results.Problem(
                "Security.usp_DeactivateUserFromApplication did not return a result.");
        }

        var returnCode = reader.GetInt32(reader.GetOrdinal("ReturnCode"));
        var messageOrdinal = reader.GetOrdinal("Message");
        var message = reader.IsDBNull(messageOrdinal)
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
                statusCode: StatusCodes.Status500InternalServerError),

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
    ModifyApplicationAccessRequest request,
    AppDbContext db)
{
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "Security.usp_ReactivateUserFromApplication";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(new SqlParameter("@UserID", SqlDbType.Int)
        {
            Value = userId
        });

        command.Parameters.Add(new SqlParameter("@ApplicationID", SqlDbType.Int)
        {
            Value = applicationId
        });

        command.Parameters.Add(new SqlParameter("@ModifiedBy", SqlDbType.Int)
        {
            Value = (object?)request.ModifiedBy ?? DBNull.Value
        });

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return Results.Problem(
                "Security.usp_ReactivateUserFromApplication did not return a result.");
        }

        var returnCode = reader.GetInt32(reader.GetOrdinal("ReturnCode"));
        var messageOrdinal = reader.GetOrdinal("Message");
        var message = reader.IsDBNull(messageOrdinal)
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
                statusCode: StatusCodes.Status500InternalServerError),

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
    AppDbContext db)
{
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "Security.usp_ChangeUserApplicationRole";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.Add(new SqlParameter("@UserID", SqlDbType.Int)
        {
            Value = userId
        });

        command.Parameters.Add(new SqlParameter("@ApplicationID", SqlDbType.Int)
        {
            Value = applicationId
        });

        command.Parameters.Add(new SqlParameter("@RoleID", SqlDbType.Int)
        {
            Value = request.RoleID
        });

        command.Parameters.Add(new SqlParameter("@ModifiedBy", SqlDbType.Int)
        {
            Value = (object?)request.ModifiedBy ?? DBNull.Value
        });

        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return Results.Problem(
                "Security.usp_ChangeUserApplicationRole did not return a result.");
        }

        var returnCode = reader.GetInt32(reader.GetOrdinal("ReturnCode"));
        var messageOrdinal = reader.GetOrdinal("Message");
        var message = reader.IsDBNull(messageOrdinal)
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
                statusCode: StatusCodes.Status500InternalServerError),

            _ => Results.Problem(
                $"Unexpected return code from Security.usp_ChangeUserApplicationRole: {returnCode}")
        };
    }
    finally
    {
        await connection.CloseAsync();
    }
}