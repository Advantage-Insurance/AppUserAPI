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

            response.EnsureSuccessStatusCode();

            return await response.Content
                .ReadFromJsonAsync<CreateUserResponse>();
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

        public async Task<ApplicationAccessResponse?> DeactivateApplicationAccessAsync( int userId, int applicationId, ModifyApplicationAccessRequest request)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            var response = await client.PostAsJsonAsync(
                $"/users/{userId}/applications/{applicationId}/deactivate",
                request);

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

        public async Task<ApplicationAccessResponse?> ReactivateApplicationAccessAsync( int userId, int applicationId, ModifyApplicationAccessRequest request)
        {
            var client = _httpClientFactory.CreateClient("AppUserAPI");

            var response = await client.PostAsJsonAsync(
                $"/users/{userId}/applications/{applicationId}/reactivate",
                request);

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

    }
}