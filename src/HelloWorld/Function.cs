using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloWorld
{
    public class Function
    {
        private readonly AmazonCognitoIdentityProviderClient _cognitoClient;
        private readonly string _userPoolId;

        public Function()
        {
            _cognitoClient = new AmazonCognitoIdentityProviderClient();
            _userPoolId = Environment.GetEnvironmentVariable("USER_POOL_ID")
                          ?? throw new ArgumentNullException("USER_POOL_ID");
        }

        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                context.Logger.LogInformation($"Request recebido: {JsonSerializer.Serialize(request)}");
                if (string.IsNullOrEmpty(request.Body))
                {
                    context.Logger.LogWarning("body vazio.");
                    return CreateResponse(HttpStatusCode.BadRequest, new { message = "'body' vazio." });
                }

                var requestBody = JsonSerializer.Deserialize<RequestBody>(request.Body);
                if (requestBody == null || string.IsNullOrWhiteSpace(requestBody.Cpf))
                {
                    context.Logger.LogWarning("CPF vazio.");
                    return CreateResponse(HttpStatusCode.BadRequest, new { message = "'Cpf' vazio." });
                }

                var cpf = requestBody.Cpf;
                context.Logger.LogInformation($"CPF recebido: {cpf}");

                var users = await GetUsersAsync(context);
                var cpfExists = users.Any(user => user.Attributes
                                                    .Any(attr => attr.Name == "custom:Cpf" && attr.Value == cpf));

                context.Logger.LogInformation($"CPF existe? {cpfExists}");

                return CreateResponse(HttpStatusCode.OK, new { message = "success", status = cpfExists });
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error processing request: {ex.Message}");
                return CreateResponse(HttpStatusCode.InternalServerError, new { message = "Internal server error" });
            }
        }

        private async Task<List<UserType>> GetUsersAsync(ILambdaContext context)
        {
            var allUsers = new List<UserType>();
            string paginationToken = null;

            do
            {
                var request = new ListUsersRequest
                {
                    UserPoolId = _userPoolId,
                    PaginationToken = paginationToken
                };

                var response = await _cognitoClient.ListUsersAsync(request);
                allUsers.AddRange(response.Users);
                paginationToken = response.PaginationToken;

                context.Logger.LogInformation($"Fetched {response.Users.Count} users. Next token: {paginationToken}");
            } while (!string.IsNullOrEmpty(paginationToken));

            return allUsers;
        }

        private APIGatewayProxyResponse CreateResponse(HttpStatusCode statusCode, object body)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)statusCode,
                Body = JsonSerializer.Serialize(body),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
                IsBase64Encoded = false
            };
        }

        public class RequestBody
        {
            public string Cpf { get; set; }
        }
    }
}