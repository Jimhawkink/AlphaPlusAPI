using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Data.SqlClient;  // ← Change this line
using Microsoft.IdentityModel.Tokens;
using AlphaPlusAPI.DTOs;

namespace AlphaPlusAPI.Services
{
    public class AuthService
    {
        private readonly DatabaseService _db;
        private readonly IConfiguration _configuration;

        public AuthService(DatabaseService db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        public async Task<LoginResponse> AuthenticateAsync(LoginRequest request)
        {
            try
            {
                var query = @"SELECT UserID, Password, Name, UserType, EmailID, ContactNo, Active 
                            FROM Registration 
                            WHERE UserID = @UserID";

                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@UserID", request.UserID)
                };

                var result = await _db.ExecuteQueryAsync(query, parameters);

                if (result.Rows.Count == 0)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "User not found"
                    };
                }

                var row = result.Rows[0];
                var storedPassword = row["Password"].ToString()?.Trim() ?? "";
                var activeValue = row["Active"].ToString()?.Trim() ?? "0";
                var isActive = activeValue == "1" || activeValue.Equals("true", StringComparison.OrdinalIgnoreCase);

                if (!isActive)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "User account is inactive"
                    };
                }

                // Simple password check (in production, use BCrypt)
                if (storedPassword != request.Password)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Invalid password"
                    };
                }

                // Get user rights
                var rights = await GetUserRightsAsync(request.UserID);

                var userData = new UserData
                {
                    UserID = row["UserID"].ToString() ?? "",
                    Name = row["Name"].ToString() ?? "",
                    UserType = row["UserType"].ToString() ?? "",
                    EmailID = row["EmailID"]?.ToString(),
                    ContactNo = row["ContactNo"]?.ToString(),
                    Active = isActive,
                    Rights = rights
                };

                var token = GenerateJwtToken(userData);

                return new LoginResponse
                {
                    Success = true,
                    Message = "Login successful",
                    Token = token,
                    User = userData
                };
            }
            catch (Exception ex)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        private async Task<List<UserRightsData>> GetUserRightsAsync(string userId)
        {
            var query = @"SELECT ModuleName, UR_Save, UR_Update, UR_Delete, UR_View 
                        FROM UserRights 
                        WHERE UserID = @UserID";

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@UserID", userId)
            };

            var result = await _db.ExecuteQueryAsync(query, parameters);
            var rights = new List<UserRightsData>();

            foreach (DataRow row in result.Rows)
            {
                rights.Add(new UserRightsData
                {
                    ModuleName = row["ModuleName"].ToString() ?? "",
                    CanSave = Convert.ToBoolean(row["UR_Save"]),
                    CanUpdate = Convert.ToBoolean(row["UR_Update"]),
                    CanDelete = Convert.ToBoolean(row["UR_Delete"]),
                    CanView = Convert.ToBoolean(row["UR_View"])
                });
            }

            return rights;
        }

        private string GenerateJwtToken(UserData user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyForAlphaPlusApp2025!@#$%";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.UserType),
                new Claim("email", user.EmailID ?? ""),
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"] ?? "AlphaPlusAPI",
                audience: jwtSettings["Audience"] ?? "AlphaPlusApp",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToInt32(jwtSettings["ExpirationMinutes"] ?? "1440")),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<ApiResponse<bool>> CreateUserAsync(CreateUserRequest request)
        {
            try
            {
                var query = @"INSERT INTO Registration 
                            (UserID, Password, Name, UserType, ContactNo, EmailID, SSN, JoiningDate, Active, PayrollType)
                            VALUES 
                            (@UserID, @Password, @Name, @UserType, @ContactNo, @EmailID, @SSN, @JoiningDate, @Active, @PayrollType)";

                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@UserID", request.UserID),
                    new SqlParameter("@Password", request.Password),
                    new SqlParameter("@Name", request.Name),
                    new SqlParameter("@UserType", request.UserType),
                    new SqlParameter("@ContactNo", (object?)request.ContactNo ?? DBNull.Value),
                    new SqlParameter("@EmailID", (object?)request.EmailID ?? DBNull.Value),
                    new SqlParameter("@SSN", (object?)request.SSN ?? DBNull.Value),
                    new SqlParameter("@JoiningDate", DateTime.Now),
                    new SqlParameter("@Active", true),
                    new SqlParameter("@PayrollType", (object?)request.PayrollType ?? DBNull.Value)
                };

                await _db.ExecuteNonQueryAsync(query, parameters);

                return new ApiResponse<bool>
                {
                    Success = true,
                    Message = "User created successfully",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Error creating user: {ex.Message}",
                    Data = false
                };
            }
        }
    }
}