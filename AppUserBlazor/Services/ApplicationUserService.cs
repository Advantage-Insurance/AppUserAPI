using AppUserBlazor.Models;
using System.Net.Http.Json;

namespace AppUserBlazor.Services
{
    public class ApplicationUserService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ApplicationUserService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<CurrentSecurityUser?> GetCurrentUserAsync()
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            return await client.GetFromJsonAsync<CurrentSecurityUser>(
                "/auth/currentuser");
        }

        public async Task<List<ApplicationLookup>?> GetApplicationsAsync()
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            return await client.GetFromJsonAsync<List<ApplicationLookup>>(
                "/applications");
        }

        public async Task<List<SecurityUser>?> GetUsersAsync()
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            return await client.GetFromJsonAsync<List<SecurityUser>>(
                "/users");
        }

        public async Task<List<RoleLookup>?> GetApplicationRolesAsync(int applicationId)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            return await client.GetFromJsonAsync<List<RoleLookup>>(
                $"/applications/{applicationId}/roles");
        }

        public async Task<List<ApplicationUser>?> GetAllAsync()
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            return await client.GetFromJsonAsync<List<ApplicationUser>>(
                "/applicationusers");
        }

        public async Task<List<ApplicationUser>?> GetByApplicationAsync(int applicationId)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            return await client.GetFromJsonAsync<List<ApplicationUser>>(
                $"/applicationusers/application/{applicationId}");
        }

        public async Task<ApplicationUser?> GetByApplicationAndUserAsync( int applicationId, int userId)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            var response = await client.GetAsync(
                $"/applicationusers/application/{applicationId}/user/{userId}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content
                .ReadFromJsonAsync<ApplicationUser>();
        }

        public async Task<List<ApplicationUser>?> GetByUserAsync(int userId)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            return await client.GetFromJsonAsync<List<ApplicationUser>>(
                $"/applicationusers/user/{userId}");
        }

        public async Task<CreateUserResponse?> CreateUserAsync(CreateUserRequest request)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            var response = await client.PostAsJsonAsync(
                "/users",
                request);

            var result = await response.Content
                .ReadFromJsonAsync<CreateUserResponse>();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    result?.Message ?? "Unable to create user.");
            }

            return result;
        }

        public async Task<AddUserToApplicationResponse?> AddUserToApplicationAsync(int userId, AddUserToApplicationRequest request)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            var response = await client.PostAsJsonAsync(
                $"/users/{userId}/applications",
                request);

            response.EnsureSuccessStatusCode();

            return await response.Content
                .ReadFromJsonAsync<AddUserToApplicationResponse>();
        }

        public async Task<ChangeUserRoleResponse?> ChangeUserRoleAsync(int userId, int applicationId, ChangeUserRoleRequest request)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            var response = await client.PutAsJsonAsync(
                $"/users/{userId}/applications/{applicationId}/role",
                request);

            response.EnsureSuccessStatusCode();

            return await response.Content
                .ReadFromJsonAsync<ChangeUserRoleResponse>();
        }

        public async Task<ApplicationAccessResponse?> DeactivateApplicationAccessAsync(int userId, int applicationId)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            var response = await client.PostAsync(
                $"/users/{userId}/applications/{applicationId}/deactivate",
                null);

            var result = await response.Content
                .ReadFromJsonAsync<ApplicationAccessResponse>();

            if (response.IsSuccessStatusCode ||
                response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                return result;
            }

            response.EnsureSuccessStatusCode();

            return result;
        }

        public async Task<ApplicationAccessResponse?> ReactivateApplicationAccessAsync(int userId, int applicationId)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            var response = await client.PostAsync(
                $"/users/{userId}/applications/{applicationId}/reactivate",
                null);

            var result = await response.Content
                .ReadFromJsonAsync<ApplicationAccessResponse>();

            if (response.IsSuccessStatusCode ||
                response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                return result;
            }

            response.EnsureSuccessStatusCode();

            return result;
        }

        public async Task<CurrentUserApplicationAccess?>GetCurrentUserApplicationAsync(int applicationId)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            return await client.GetFromJsonAsync<CurrentUserApplicationAccess>(
                $"/me/applications/{applicationId}");
        }

        public async Task<CreateApplicationRoleResponse?> CreateApplicationRoleAsync(int applicationId, CreateApplicationRoleRequest request)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            using var response = await client.PostAsJsonAsync(
                $"/applications/{applicationId}/roles",
                request);

            if (!response.IsSuccessStatusCode)
            {
                var message = "Unable to create the application role.";

                try
                {
                    using var document =
                        System.Text.Json.JsonDocument.Parse(
                            await response.Content.ReadAsStringAsync());

                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if ((property.Name.Equals(
                                "message", StringComparison.OrdinalIgnoreCase)
                             || property.Name.Equals(
                                "detail", StringComparison.OrdinalIgnoreCase))
                            && property.Value.ValueKind ==
                                System.Text.Json.JsonValueKind.String)
                        {
                            message = property.Value.GetString() ?? message;
                            break;
                        }
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // Use the default message for a non-JSON error response.
                }

                throw new InvalidOperationException(message);
            }

            return await response.Content
                .ReadFromJsonAsync<CreateApplicationRoleResponse>();
        }

        public async Task<UpdateApplicationRoleDescriptionResponse?>
    UpdateApplicationRoleDescriptionAsync(
        int applicationId,
        int roleId,
        UpdateApplicationRoleDescriptionRequest request)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            using var response = await client.PutAsJsonAsync(
                $"/applications/{applicationId}/roles/{roleId}/description",
                request);

            if (!response.IsSuccessStatusCode)
            {
                var message = "Unable to update the role description.";

                try
                {
                    using var document =
                        System.Text.Json.JsonDocument.Parse(
                            await response.Content.ReadAsStringAsync());

                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if ((property.Name.Equals(
                                "message", StringComparison.OrdinalIgnoreCase)
                             || property.Name.Equals(
                                "detail", StringComparison.OrdinalIgnoreCase))
                            && property.Value.ValueKind ==
                                System.Text.Json.JsonValueKind.String)
                        {
                            message = property.Value.GetString() ?? message;
                            break;
                        }
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // Use the default message for a non-JSON response.
                }

                throw new InvalidOperationException(message);
            }

            return await response.Content
                .ReadFromJsonAsync<UpdateApplicationRoleDescriptionResponse>();
        }

        public async Task<UpdateApplicationDescriptionResponse?>
    UpdateApplicationDescriptionAsync(
        int applicationId,
        UpdateApplicationDescriptionRequest request)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            using var response = await client.PutAsJsonAsync(
                $"/applications/{applicationId}/description",
                request);

            if (!response.IsSuccessStatusCode)
            {
                var message = "Unable to update the application description.";

                try
                {
                    using var document =
                        System.Text.Json.JsonDocument.Parse(
                            await response.Content.ReadAsStringAsync());

                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if ((property.Name.Equals(
                                "message", StringComparison.OrdinalIgnoreCase)
                             || property.Name.Equals(
                                "detail", StringComparison.OrdinalIgnoreCase))
                            && property.Value.ValueKind ==
                                System.Text.Json.JsonValueKind.String)
                        {
                            message = property.Value.GetString() ?? message;
                            break;
                        }
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // Use the default message for a non-JSON response.
                }

                throw new InvalidOperationException(message);
            }

            return await response.Content
                .ReadFromJsonAsync<UpdateApplicationDescriptionResponse>();
        }

        public async Task<CreateApplicationResponse?> CreateApplicationAsync(
    CreateApplicationRequest request)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            using var response = await client.PostAsJsonAsync(
                "/applications", request);

            if (!response.IsSuccessStatusCode)
            {
                var message = "Unable to create the application.";

                try
                {
                    using var document =
                        System.Text.Json.JsonDocument.Parse(
                            await response.Content.ReadAsStringAsync());

                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if ((property.Name.Equals(
                                "message", StringComparison.OrdinalIgnoreCase)
                             || property.Name.Equals(
                                "detail", StringComparison.OrdinalIgnoreCase))
                            && property.Value.ValueKind ==
                                System.Text.Json.JsonValueKind.String)
                        {
                            message = property.Value.GetString() ?? message;
                            break;
                        }
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // Use the default message for a non-JSON response.
                }

                throw new InvalidOperationException(message);
            }

            return await response.Content
                .ReadFromJsonAsync<CreateApplicationResponse>();
        }

    }
}